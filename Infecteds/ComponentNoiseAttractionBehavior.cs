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
	///
	/// Si llega un ruido NUEVO (en una posición distinta) mientras estamos en
	/// NoiseAttraction o NoiseInvestigation, se vuelve a NoiseAttraction para ir al
	/// nuevo origen. Un ruido en la misma posición solo refresca la investigación.
	/// </summary>
	public class ComponentNoiseAttractionBehavior : ComponentBehavior, INoiseAttraction, IUpdateable
	{
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentRider m_componentRider;

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
		/// Umbral cuadrado de distancia para considerar un ruido como "nuevo".
		/// Un ruido en la misma posición (dentro de este umbral) no reinicia la
		/// atracción, solo refresca la investigación.
		/// </summary>
		public float NewNoiseDistanceSq = 1f;

		/// <summary>
		/// Contador que se incrementa cada vez que llega un ruido NUEVO (posición
		/// distinta). Se usa para que los estados detecten ruidos nuevos y
		/// reaccionen sin necesidad de polling.
		/// </summary>
		private long m_noiseCounter;

		/// <summary>
		/// Valor del contador al entrar en el estado actual. Si en el update
		/// detectamos m_noiseCounter != m_stateEnterNoiseCounter, es que llegó
		/// un ruido nuevo mientras estábamos en ese estado.
		/// </summary>
		private long m_stateEnterNoiseCounter;

		/// <summary>true mientras estamos en el estado NoiseAttraction.</summary>
		private bool m_isAttracting;

		/// <summary>Indica si hemos tomado control de la montura y debemos soltarlo.</summary>
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
			m_componentRider = Entity.FindComponent<ComponentRider>(false);

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
				m_stateEnterNoiseCounter = m_noiseCounter;

				// Solo usamos pathfinding si NO estamos montados.
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

				// NUEVO: si llegó otro ruido (posición distinta) mientras íbamos
				// al anterior, redirigir el pathfinding al nuevo origen.
				if (m_noiseCounter != m_stateEnterNoiseCounter)
				{
					m_stateEnterNoiseCounter = m_noiseCounter;

					if (!IsMounted() && m_noisePosition != null)
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
					// En montados, ControlMountTowardNoise ya lee m_noisePosition
					// cada frame en Update(), así que no hay que hacer nada extra.
				}

				Vector3 destination = m_noisePosition.Value;
				m_componentCreatureModel.LookAtOrder = destination;

				Vector3 currentPosition = GetEffectivePosition();
				float distSq = Vector3.DistanceSquared(currentPosition, destination);

				if (distSq <= ArrivalDistanceSq)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
					return;
				}

				if (!IsMounted() && m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
				}
			}, delegate
			{
				m_isAttracting = false;
				m_componentPathfinding.Stop();
				StopMountMovement();
			});

			// -------------------- Estado: NoiseInvestigation --------------------
			m_stateMachine.AddState("NoiseInvestigation", delegate
			{
				m_stateEnterNoiseCounter = m_noiseCounter;
				m_componentPathfinding.Stop();
				StopMountMovement();
				m_investigationStartTime = m_subsystemTime.GameTime;
			}, delegate
			{
				// NUEVO: si llegó un ruido nuevo (posición distinta) mientras
				// investigábamos, ir hacia el nuevo origen.
				if (m_noiseCounter != m_stateEnterNoiseCounter)
				{
					m_stateMachine.TransitionTo("NoiseAttraction");
					return;
				}

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
			// ¿Es un ruido en una posición distinta al actual? Solo en ese caso
			// reiniciamos la atracción/investigación hacia el nuevo origen.
			bool isNewNoise = m_noisePosition == null
				|| Vector3.DistanceSquared(m_noisePosition.Value, sourcePosition) > NewNoiseDistanceSq;

			m_noisePosition = sourcePosition;
			m_noiseLoudness = loudness;
			m_nextUpdateTime = 0.0;

			if (isNewNoise)
			{
				m_noiseCounter++;

				// Un ruido nuevo siempre reinicia la investigación si ya estábamos
				// investigando, para que el temporizador cuente desde cero.
				m_investigationStartTime = m_subsystemTime.GameTime;
			}
			else
			{
				// Misma posición: solo refrescamos el temporizador de investigación
				// para que la criatura siga atenta mientras el sonido continúe.
				m_investigationStartTime = m_subsystemTime.GameTime;
			}
		}

		public void Update(float dt)
		{
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
		// Control de la montura
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
					speedOrder = -1;
				}
			}

			ComponentLocomotion mountLocomotion = mount.Entity.FindComponent<ComponentLocomotion>();
			bool isFlying = mountLocomotion != null && mountLocomotion.FlySpeed > 0f;

			if (isFlying)
			{
				ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
				if (pilot != null)
				{
					Vector3 pilotDest = destination;
					pilotDest.Y = MathUtils.Max(destination.Y, myPos.Y + 1f);

					pilot.SetDestination(pilotDest, 1f, 1f, false, false, true, null);
				}
			}
			else
			{
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
