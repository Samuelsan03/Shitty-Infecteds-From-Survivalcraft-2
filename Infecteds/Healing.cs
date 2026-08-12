using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Healing : ComponentBehavior, IUpdateable
	{
		public static string fName = "Healing";

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

					// Lógica de curación de salud
					if (m_needsHealthHeal && m_healingTarget != null && m_healingTarget.Health > 0f)
					{
						m_healingTarget.Heal(100f);
					}

					// Lógica de curación de enfermedades
					if (m_needsDiseaseCure && m_healingTargetEntity != null)
					{
						CureDiseases(m_healingTargetEntity);
					}

					if (m_healingTargetBody != null)
					{
						ComponentPlayer targetPlayer = m_healingTargetBody.Entity.FindComponent<ComponentPlayer>();
						if (targetPlayer != null)
						{
							m_subsystemAudio.PlaySound("Audio/classic intro smb melee", 1f, 0f, m_healingTargetBody.Position, 3f, true);

							// Mostrar mensajes de curación al jugador
							if (targetPlayer.ComponentGui != null)
							{
								string healerName = m_componentCreature.DisplayName;

								// Mensaje de curación de enfermedad (verde)
								if (m_needsDiseaseCure)
								{
									string diseaseMessage = string.Format(LanguageControl.Get(fName, 1), healerName);
									targetPlayer.ComponentGui.DisplaySmallMessage(diseaseMessage, new Color(100, 255, 150), true, false);
								}

								// Mensaje de restauración de salud (rojo claro)
								if (m_needsHealthHeal)
								{
									string healthMessage = string.Format(LanguageControl.Get(fName, 2), healerName);
									targetPlayer.ComponentGui.DisplaySmallMessage(healthMessage, new Color(255, 150, 150), true, false);
								}
							}
						}

						m_healingTargetBody = null;
						m_healingTarget = null;
						m_healingTargetEntity = null;
						m_needsHealthHeal = false;
						m_needsDiseaseCure = false;
					}

					// Detenemos y LIMPIAMOS las partículas de la memoria
					if (m_particleSystem != null)
					{
						m_particleSystem.Stopped = true;
						m_particleSystem = null;
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

		private void CureDiseases(Entity targetEntity)
		{
			if (targetEntity == null) return;

			// Verificar si es un jugador
			ComponentPlayer targetPlayer = targetEntity.FindComponent<ComponentPlayer>();

			if (targetPlayer != null)
			{
				// Si es jugador en modo Creativo o con mecánicas de supervivencia deshabilitadas, no curar enfermedades
				// ya que en esos modos no puede enfermarse
				if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
					!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				{
					return;
				}

				// Curar ComponentFlu del jugador
				if (targetPlayer.ComponentFlu != null && targetPlayer.ComponentFlu.HasFlu)
				{
					targetPlayer.ComponentFlu.m_fluDuration = 0f;
				}

				// Curar ComponentSickness del jugador
				if (targetPlayer.ComponentSickness != null && targetPlayer.ComponentSickness.IsSick)
				{
					targetPlayer.ComponentSickness.m_sicknessDuration = 0f;
				}
			}
			else
			{
				// Es una criatura, curar enfermedades de criaturas
				ComponentInfectedWithPoison poison = targetEntity.FindComponent<ComponentInfectedWithPoison>();
				if (poison != null && poison.IsInfected)
				{
					poison.Cure();
				}

				ComponentCreatureFlu flu = targetEntity.FindComponent<ComponentCreatureFlu>();
				if (flu != null && flu.HasFlu)
				{
					flu.Cure();
				}
			}
		}

		private bool HasDisease(Entity entity, bool isSelf)
		{
			if (entity == null) return false;

			ComponentPlayer player = entity.FindComponent<ComponentPlayer>();

			if (player != null)
			{
				// Para jugadores, no considerar enfermedades en Creativo o si las mecánicas de supervivencia están deshabilitadas
				if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
					!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				{
					return false;
				}

				// Para jugadores, solo curar si está habilitado curar a otras criaturas
				// (los jugadores se consideran "otros" aliados cuando están en la misma manada)
				if (!m_canCureOtherCreatures)
				{
					return false;
				}

				if (player.ComponentFlu != null && player.ComponentFlu.HasFlu) return true;
				if (player.ComponentSickness != null && player.ComponentSickness.IsSick) return true;
			}
			else
			{
				// Para criaturas
				bool canCure = isSelf ? m_canCureSelf : m_canCureOtherCreatures;

				if (!canCure)
				{
					return false;
				}

				ComponentInfectedWithPoison poison = entity.FindComponent<ComponentInfectedWithPoison>();
				if (poison != null && poison.IsInfected) return true;

				ComponentCreatureFlu flu = entity.FindComponent<ComponentCreatureFlu>();
				if (flu != null && flu.HasFlu) return true;
			}

			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>(true);

			m_probabilityOfCuring = valuesDictionary.GetValue<float>("ProbabilityOfCuring");
			m_doesHealAllies = valuesDictionary.GetValue<bool>("DoesHealAllies");
			m_doesHealSelf = valuesDictionary.GetValue<bool>("DoesHealSelf");
			m_canCureOtherCreatures = valuesDictionary.GetValue<bool>("CanCureOtherCreatures");
			m_canCureSelf = valuesDictionary.GetValue<bool>("CanCureSelf");

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
					m_needsHealthHeal = false;
					m_needsDiseaseCure = false;
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				// Iniciamos el proceso.
				m_healingTargetBody = m_healingTarget.Entity.FindComponent<ComponentBody>(true);
				m_healingTargetEntity = m_healingTarget.Entity;
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
			ComponentHealth result = null;
			bool foundLowHealth = false;
			bool foundDisease = false;

			// Resetear flags
			m_needsHealthHeal = false;
			m_needsDiseaseCure = false;

			// Verificar uno mismo - salud baja
			if (m_doesHealSelf && m_componentCreature.ComponentHealth.Health <= 0.2f && m_componentCreature.ComponentHealth.Health > 0f)
			{
				result = m_componentCreature.ComponentHealth;
				foundLowHealth = true;
				m_needsHealthHeal = true;
			}

			// Verificar uno mismo - enfermedades
			if (!foundLowHealth && m_canCureSelf && HasDisease(Entity, true))
			{
				result = m_componentCreature.ComponentHealth;
				foundDisease = true;
				m_needsDiseaseCure = true;
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
							float distance = Vector3.Distance(position, playerData.ComponentPlayer.ComponentBody.Position);

							if (distance < m_healingRadius)
							{
								// Verificar salud baja primero (prioridad)
								if (!foundLowHealth && playerHealth.Health <= 0.2f && playerHealth.Health > 0f)
								{
									result = playerHealth;
									foundLowHealth = true;
									m_needsHealthHeal = true;
									// También verificar si tiene enfermedades
									m_needsDiseaseCure = HasDisease(playerData.ComponentPlayer.Entity, false);
								}
								// Luego verificar enfermedades
								else if (!foundLowHealth && !foundDisease && HasDisease(playerData.ComponentPlayer.Entity, false))
								{
									result = playerHealth;
									foundDisease = true;
									m_needsHealthHeal = false;
									m_needsDiseaseCure = true;
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
						ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();

						if (herd != null && herd.HerdName == m_componentHerdBehavior.HerdName)
						{
							float distance = Vector3.Distance(position, creature.ComponentBody.Position);

							if (distance < m_healingRadius)
							{
								// Verificar salud baja primero (prioridad)
								if (!foundLowHealth && creatureHealth.Health <= 0.2f && creatureHealth.Health > 0f)
								{
									result = creatureHealth;
									foundLowHealth = true;
									m_needsHealthHeal = true;
									// También verificar si tiene enfermedades
									m_needsDiseaseCure = HasDisease(creature.Entity, false);
								}
								// Luego verificar enfermedades
								else if (!foundLowHealth && !foundDisease && HasDisease(creature.Entity, false))
								{
									result = creatureHealth;
									foundDisease = true;
									m_needsHealthHeal = false;
									m_needsDiseaseCure = true;
								}
							}
						}
					}
				}
			}

			return result;
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;
		private ComponentCreature m_componentCreature;
		private ComponentNewHerdBehavior m_componentHerdBehavior;

		private StateMachine m_stateMachine = new StateMachine();
		private float m_dt;
		private float m_importanceLevel;
		private Random m_random = new Random();

		private float m_probabilityOfCuring;
		private bool m_doesHealAllies;
		private bool m_doesHealSelf;
		private bool m_canCureOtherCreatures;
		private bool m_canCureSelf;

		private ComponentHealth m_healingTarget;
		private Entity m_healingTargetEntity;
		private HealingParticleSystem m_particleSystem;
		private ComponentBody m_healingTargetBody;
		private float m_healingTimer;
		private float m_healingRadius;

		private bool m_needsHealthHeal;
		private bool m_needsDiseaseCure;
	}
}
