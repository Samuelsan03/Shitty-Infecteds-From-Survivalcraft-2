using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de atracción de ruido. Implementa INoiseAttraction para recibir
	/// notificaciones de ruido emitidas por SubsystemNoiseAttraction y reaccionar
	/// mediante una máquina de estados con tres estados: Idle, NoiseAttraction y
	/// NoiseInvestigation.
	///
	/// Extendido para criaturas montadas: si el jinete está montado, la orden de
	/// desplazamiento se envía a la montura (SteedBehavior / SteedBehaviorImproved /
	/// Pilot) en lugar de al pathfinding del jinete.
	/// </summary>
	public class ComponentNoiseAttractionBehavior : ComponentBehavior, INoiseAttraction, IUpdateable
	{
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentRider m_componentRider; // NUEVO

		public StateMachine m_stateMachine = new StateMachine();
		public Random m_random = new Random();

		/// <summary>Posición del último ruido escuchado.</summary>
		public Vector3? m_noisePosition;

		/// <summary>Intensidad del último ruido escuchado.</summary>
		public float m_noiseLoudness;

		/// <summary>Momento en que comenzó la investigación del ruido.</summary>
		public double m_investigationStartTime;

		/// <summary>Próximo tiempo permitido de actualización (para throttling).</summary>
		public double m_nextUpdateTime;

		/// <summary>Duración de la fase de investigación antes de volver a Idle.</summary>
		public float InvestigationDuration = 6f;

		/// <summary>Distancia (al cuadrado) considerada como "llegada" al destino de ruido.</summary>
		public float ArrivalDistanceSq = 2.5f;

		/// <summary>
		/// NUEVO: true mientras estamos en el estado NoiseAttraction. Se usa para
		/// emitir órdenes de movimiento a la montura en cada frame (no en el update
		/// "throttled" de la máquina de estados).
		/// </summary>
		private bool m_isAttracting;

		/// <summary>NUEVO: indica si hemos tomado control de la montura y debemos soltarlo.</summary>
		private bool m_controllingMount;

		public override float ImportanceLevel
		{
			get
			{
				if (m_noisePosition != null)
				{
					return 2f + m_noiseLoudness;
				}
				return 0f;
			}
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentRider = Entity.FindComponent<ComponentRider>(false); // NUEVO

			// -------------------- Estado: Idle (no hace nada) --------------------
			m_stateMachine.AddState("Idle", null, delegate
			{
				if (m_noisePosition != null)
				{
					m_stateMachine.TransitionTo("NoiseAttraction");
				}
			}, null);

			// -------------------- Estado: NoiseAttraction (ir al destino) --------------------
			m_stateMachine.AddState("NoiseAttraction", delegate
			{
				m_isAttracting = true;

				// Solo usamos pathfinding si NO estamos montados. Si estamos montados,
				// las órdenes se envían a la montura cada frame desde Update().
				if (m_noisePosition != null && !IsMounted())
				{
					m_componentPathfinding.SetDestination(
						m_noisePosition.Value,
						1f,
						1f,
						0,
						false,
						true,
						false,
						null);
				}
			}, delegate
			{
				if (m_noisePosition == null)
				{
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				Vector3 destination = m_noisePosition.Value;
				m_componentCreatureModel.LookAtOrder = destination;

				// NUEVO: medir la distancia desde la posición efectiva del conjunto
				// (montura si está montado, jinete si no).
				Vector3 currentPosition = GetEffectivePosition();

				float distSq = Vector3.DistanceSquared(currentPosition, destination);

				// Llegamos lo suficientemente cerca: pasar a investigar.
				if (distSq <= ArrivalDistanceSq)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
					return;
				}

				// El pathfinding se atascó (solo aplica si no estamos montados):
				if (!IsMounted() && m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
				}
			}, delegate
			{
				m_isAttracting = false;
				m_componentPathfinding.Stop();
				StopMountMovement(); // NUEVO
			});

			// -------------------- Estado: NoiseInvestigation --------------------
			m_stateMachine.AddState("NoiseInvestigation", delegate
			{
				m_componentPathfinding.Stop();
				StopMountMovement(); // NUEVO: no dejar a la montura corriendo
				m_investigationStartTime = m_subsystemTime.GameTime;
			}, delegate
			{
				if (m_noisePosition != null)
				{
					m_componentCreatureModel.LookAtOrder = m_noisePosition.Value;
				}

				if (m_subsystemTime.GameTime - m_investigationStartTime >= InvestigationDuration)
				{
					m_noisePosition = null;
					m_noiseLoudness = 0f;
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			m_stateMachine.TransitionTo("Idle");
		}

		/// <summary>
		/// Llamado por SubsystemNoiseAttraction cuando una fuente emite ruido.
		/// </summary>
		public void AttractNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
			m_noisePosition = sourcePosition;
			m_noiseLoudness = loudness;
			m_nextUpdateTime = 0.0;
		}

		public void Update(float dt)
		{
			// NUEVO: si estamos en fase de atracción y montados, emitir órdenes
			// de movimiento a la montura CADA FRAME (el steed resetea las órdenes
			// al final de su Update, por eso no podemos hacerlo desde el update
			// throttled de la máquina de estados).
			if (m_isAttracting && m_noisePosition != null && IsMounted())
			{
				ControlMountTowardNoise(m_noisePosition.Value);
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				float num = m_random.Float(0.08f, 0.12f);
				m_nextUpdateTime = m_subsystemTime.GameTime + num;
				m_stateMachine.Update();
			}
		}

		// =====================================================================
		// NUEVOS MÉTODOS: control de la montura cuando el jinete oye un ruido
		// =====================================================================

		private bool IsMounted()
		{
			return m_componentRider != null && m_componentRider.Mount != null;
		}

		private Vector3 GetEffectivePosition()
		{
			if (IsMounted())
			{
				ComponentBody mountBody = m_componentRider.Mount.ComponentBody;
				if (mountBody != null)
				{
					return mountBody.Position;
				}
			}
			return m_componentCreature.ComponentBody.Position;
		}

		/// <summary>
		/// Ordena a la montura actual que se dirija hacia la posición del ruido.
		/// Reutiliza el patrón de ComponentZombieAI.PilotMount:
		///   - Monturas voladoras: ComponentPilot.SetDestination (leído por
		///     ComponentSteedBehaviorImproved.ProcessAIFlightControls).
		///   - Monturas terrestres: TurnOrder/SpeedOrder en SteedBehavior(Improved).
		/// </summary>
		private void ControlMountTowardNoise(Vector3 destination)
		{
			ComponentMount mount = m_componentRider?.Mount;
			if (mount == null || mount.ComponentBody == null)
			{
				return;
			}

			ComponentBody mountBody = mount.ComponentBody;
			Vector3 myPos = mountBody.Position;
			Vector3 delta = destination - myPos;
			delta.Y = 0f;

			float horizontalDistance = delta.Length();
			if (horizontalDistance < 0.01f)
			{
				// Ya estamos encima horizontalmente; que se detenga.
				StopMountMovement();
				return;
			}

			Vector3 dirToTarget = delta / horizontalDistance;

			Vector3 forward = mountBody.Matrix.Forward;
			forward.Y = 0f;
			if (forward.LengthSquared() < 0.001f)
			{
				forward = Vector3.UnitZ;
			}
			forward = Vector3.Normalize(forward);

			float cross = forward.X * dirToTarget.Z - forward.Z * dirToTarget.X;
			float dot = Vector3.Dot(forward, dirToTarget);

			// Mismo factor que usa la IA al pilotar monturas.
			float turnOrder = MathUtils.Clamp(cross * 2f, -0.5f, 0.5f);

			float stopDistance = MathF.Sqrt(ArrivalDistanceSq);
			int speedOrder = 0;
			if (horizontalDistance > stopDistance)
			{
				if (dot > 0.2f)
				{
					speedOrder = 1;
				}
				else if (dot < -0.5f)
				{
					// Casi de espaldas: retroceder suavemente en lugar de girar en seco.
					speedOrder = -1;
				}
			}

			// Detectar montura voladora
			ComponentLocomotion mountLocomotion = mount.Entity.FindComponent<ComponentLocomotion>();
			bool isFlying = mountLocomotion != null && mountLocomotion.FlySpeed > 0f;

			if (isFlying)
			{
				// El pilot vive en la entidad del jinete (ver ZombieAI.PilotMount).
				ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
				if (pilot != null)
				{
					// Apuntar un poco por encima del origen del ruido para que la
					// montura no intente aterrizar exactamente sobre el bloque.
					Vector3 pilotDest = destination;
					pilotDest.Y = MathUtils.Max(destination.Y, myPos.Y + 1f);

					pilot.SetDestination(pilotDest, 1f, 1f, false, false, true, null);
				}
			}
			else
			{
				// En terrestres nos aseguramos de que el pilot no interfiera.
				ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
				if (pilot != null && pilot.Destination != null)
				{
					pilot.Stop();
				}
			}

			ComponentSteedBehaviorImproved steedImproved = mount.Entity.FindComponent<ComponentSteedBehaviorImproved>();
			if (steedImproved != null)
			{
				steedImproved.TurnOrder = turnOrder;
				steedImproved.SpeedOrder = speedOrder;
				steedImproved.JumpOrder = 0f;
				m_controllingMount = true;
				return;
			}

			ComponentSteedBehavior steed = mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steed != null)
			{
				steed.TurnOrder = turnOrder;
				steed.SpeedOrder = speedOrder;
				steed.JumpOrder = 0f;
				m_controllingMount = true;
			}
		}

		/// <summary>
		/// Detiene cualquier orden de movimiento que hayamos dado a la montura.
		/// </summary>
		private void StopMountMovement()
		{
			if (!m_controllingMount)
			{
				return;
			}
			m_controllingMount = false;

			ComponentMount mount = m_componentRider?.Mount;
			if (mount == null)
			{
				return;
			}

			ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
			if (pilot != null && pilot.Destination != null)
			{
				pilot.Stop();
			}

			ComponentSteedBehaviorImproved steedImproved = mount.Entity.FindComponent<ComponentSteedBehaviorImproved>();
			if (steedImproved != null)
			{
				steedImproved.TurnOrder = 0f;
				steedImproved.SpeedOrder = 0;
				steedImproved.JumpOrder = 0f;
			}

			ComponentSteedBehavior steed = mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steed != null)
			{
				steed.TurnOrder = 0f;
				steed.SpeedOrder = 0;
				steed.JumpOrder = 0f;
			}
		}
	}
}
