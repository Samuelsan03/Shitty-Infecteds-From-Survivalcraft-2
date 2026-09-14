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
	/// </summary>
	public class ComponentNoiseAttractionBehavior : ComponentBehavior, INoiseAttraction, IUpdateable
	{
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentCreatureModel m_componentCreatureModel;

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

		public override float ImportanceLevel
		{
			get
			{
				// La importancia sube mientras hay un ruido pendiente de investigar,
				// haciendo que este comportamiento gane prioridad sobre otros.
				if (m_noisePosition != null)
				{
					return 2f + m_noiseLoudness;
				}
				return 0f;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

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
				if (m_noisePosition != null)
				{
					m_componentPathfinding.SetDestination(
						new Vector3?(m_noisePosition.Value),
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
				m_componentCreatureModel.LookAtOrder = new Vector3?(destination);

				float distSq = Vector3.DistanceSquared(
					m_componentCreature.ComponentBody.Position, destination);

				// Llegamos lo suficientemente cerca: pasar a investigar.
				if (distSq <= ArrivalDistanceSq)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
					return;
				}

				// El pathfinding se atascó: investigar desde donde estamos.
				if (m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("NoiseInvestigation");
				}
			}, delegate
			{
				m_componentPathfinding.Stop();
			});

			// -------------------- Estado: NoiseInvestigation (quedarse quieto y observar) --------------------
			m_stateMachine.AddState("NoiseInvestigation", delegate
			{
				m_componentPathfinding.Stop();
				m_investigationStartTime = m_subsystemTime.GameTime;
			}, delegate
			{
				if (m_noisePosition != null)
				{
					m_componentCreatureModel.LookAtOrder = new Vector3?(m_noisePosition.Value);
				}

				if (m_subsystemTime.GameTime - m_investigationStartTime >= (double)InvestigationDuration)
				{
					// Terminó la investigación: olvidar el ruido y volver a Idle.
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
			m_noisePosition = new Vector3?(sourcePosition);
			m_noiseLoudness = loudness;
			// Fuerza una actualización inmediata de la máquina de estados.
			m_nextUpdateTime = 0.0;
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				float num = m_random.Float(0.08f, 0.12f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)num;
				m_stateMachine.Update();
			}
		}
	}
}
