using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Healing : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return m_importanceLevel; }
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f) return;

			m_dt = dt;

			// 1. LÓGICA DEL TEMPORIZADOR Y ANIMACIÓN
			if (m_healingTimer > 0f)
			{
				m_healingTimer -= dt;

				// IMITA AimState.InProgress: Levantamos el brazo
				if (m_componentCreature.ComponentCreatureModel != null)
				{
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 3.2f;
				}

				if (m_healingTimer <= 0f)
				{
					// IMITA AimState.Completed: Bajamos el brazo
					if (m_componentCreature.ComponentCreatureModel != null)
					{
						m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
					}

					// Lógica de curación
					if (m_healingTarget != null && m_healingTarget.Health > 0f)
					{
						m_healingTarget.Heal(100f);
					}

					if (m_healingTargetBody != null)
					{
						ComponentPlayer targetPlayer = m_healingTargetBody.Entity.FindComponent<ComponentPlayer>();
						if (targetPlayer != null)
						{
							m_subsystemAudio.PlaySound("Audio/classic intro smb melee", 1f, 0f, m_healingTargetBody.Position, 3f, true);
						}

						m_healingTargetBody = null;
						m_healingTarget = null;
					}

					// Detenemos y LIMPIAMOS las partículas de la memoria
					if (m_particleSystem != null)
					{
						m_particleSystem.Stopped = true;
						m_particleSystem = null; // <--- ¡FUNDAMENTAL! Sin esto, el segundo intento crashea.
					}
				}
			}

			// 2. LÓGICA DE PARTÍCULAS (Estructura del Shapeshifter)
			if (m_healingTargetBody != null)
			{
				if (m_particleSystem == null)
				{
					m_particleSystem = new HealingParticleSystem();
					m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
				}
				// Actualizamos la posición de las partículas al cuerpo del objetivo CADA frame
				m_particleSystem.BoundingBox = m_healingTargetBody.BoundingBox;
			}

			// 3. Máquina de estados bloqueada mientras se cura
			if (m_healingTargetBody == null)
			{
				m_stateMachine.Update();
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>(true);

			m_probabilityOfCuring = valuesDictionary.GetValue<float>("ProbabilityOfCuring");
			m_doesHealAllies = valuesDictionary.GetValue<bool>("DoesHealAllies");
			m_doesHealSelf = valuesDictionary.GetValue<bool>("DoesHealSelf");

			m_healingRadius = 50f;

			m_stateMachine.AddState("Idle", null, delegate
			{
				m_healingTarget = FindCriticalAlly();
				m_importanceLevel = (m_healingTarget != null) ? 100f : 0f;

				if (IsActive && m_healingTarget != null)
				{
					m_stateMachine.TransitionTo("Healing");
				}
			}, null);

			m_stateMachine.AddState("Healing", delegate
			{
				m_healingTarget = FindCriticalAlly();

				if (m_healingTarget == null || m_random.Float() > m_probabilityOfCuring)
				{
					m_importanceLevel = 0f;
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				// Iniciamos el proceso.
				m_healingTargetBody = m_healingTarget.Entity.FindComponent<ComponentBody>(true);
				m_healingTimer = 1.5f;

				// ELIMINADO: m_importanceLevel = 0f; 
				// Si lo pones en 0, el motor mata el Update y las partículas nunca se dibujan.
				// Al no tocarlo, se mantiene en 100f, el Update sigue corriendo, y el if(m_healingTargetBody == null) 
				// de más abajo evita que la máquina de estados se reinicie sola.

				m_stateMachine.TransitionTo("Idle");
			}, null, null);

			m_stateMachine.TransitionTo("Idle");
		}

		private ComponentHealth FindCriticalAlly()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;

			if (m_doesHealSelf && m_componentCreature.ComponentHealth.Health <= 0.2f && m_componentCreature.ComponentHealth.Health > 0f)
			{
				return m_componentCreature.ComponentHealth;
			}

			if (m_doesHealAllies && !string.IsNullOrEmpty(m_componentHerdBehavior.HerdName))
			{
				if (m_componentHerdBehavior.HerdName == "player")
				{
					foreach (PlayerData playerData in m_subsystemPlayers.PlayersData)
					{
						if (playerData.ComponentPlayer != null)
						{
							ComponentHealth playerHealth = playerData.ComponentPlayer.ComponentHealth;
							if (playerHealth.Health <= 0.2f && playerHealth.Health > 0f)
							{
								if (Vector3.Distance(position, playerData.ComponentPlayer.ComponentBody.Position) < m_healingRadius)
								{
									return playerHealth;
								}
							}
						}
					}
				}

				foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
				{
					if (creature != m_componentCreature)
					{
						ComponentHealth creatureHealth = creature.ComponentHealth;
						if (creatureHealth.Health <= 0.2f && creatureHealth.Health > 0f)
						{
							ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (herd != null && herd.HerdName == m_componentHerdBehavior.HerdName)
							{
								if (Vector3.Distance(position, creature.ComponentBody.Position) < m_healingRadius)
								{
									return creatureHealth;
								}
							}
						}
					}
				}
			}

			return null;
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreature m_componentCreature;
		private ComponentNewHerdBehavior m_componentHerdBehavior;

		private StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private Random m_random = new Random();

		private float m_probabilityOfCuring;
		private bool m_doesHealAllies;
		private bool m_doesHealSelf;
		private ComponentHealth m_healingTarget;
		private HealingParticleSystem m_particleSystem;
		private ComponentBody m_healingTargetBody;
		private float m_healingTimer;
		private float m_healingRadius;
	}
}
