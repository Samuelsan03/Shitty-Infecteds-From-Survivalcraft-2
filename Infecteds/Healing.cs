using System;
using System.Collections.Generic;
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

		private struct HealingTargetData
		{
			public ComponentHealth Health;
			public Entity Entity;
			public ComponentBody Body;
			public bool NeedsHealthHeal;
			public bool NeedsDiseaseCure;
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f) return;

			m_dt = dt;

			bool isHealingActive = m_healingTimer > 0f || m_messageTimer > 0f;

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

					// Aplicar curación a todos los objetivos al mismo tiempo
					ApplyHealingToAllTargets();

					// Iniciar temporizador para mostrar mensaje DESPUÉS de que las partículas se disipen
					m_messageTimer = 0.5f;
				}
			}

			// 1.5 LÓGICA DEL MENSAJE (aparece después de las partículas)
			if (m_messageTimer > 0f)
			{
				m_messageTimer -= dt;
				if (m_messageTimer <= 0f)
				{
					ShowHealingMessages();
				}
			}

			// 2. LÓGICA DE PARTÍCULAS (Estructura del Shapeshifter)
			if (isHealingActive)
			{
				// Crear sistemas de partículas para cada objetivo si no existen
				if (m_particleSystems.Count == 0)
				{
					foreach (var target in m_healingTargetsList)
					{
						if (target.Body != null)
						{
							HealingParticleSystem ps = new HealingParticleSystem();
							m_subsystemParticles.AddParticleSystem(ps, false);
							m_particleSystems.Add(ps);
						}
					}
				}

				// Actualizar posición de cada sistema de partículas al cuerpo de su objetivo CADA frame
				int index = 0;
				foreach (var target in m_healingTargetsList)
				{
					if (target.Body != null && index < m_particleSystems.Count)
					{
						m_particleSystems[index].BoundingBox = target.Body.BoundingBox;
					}
					index++;
				}
			}
			else if (m_particleSystems.Count > 0)
			{
				// Detenemos y limpiamos todos los sistemas de partículas de la memoria
				foreach (HealingParticleSystem ps in m_particleSystems)
				{
					ps.Stopped = true;
				}
				m_particleSystems.Clear();
			}

			// 3. Máquina de estados bloqueada mientras se cura
			if (!isHealingActive)
			{
				m_stateMachine.Update();
			}
		}

		private void ApplyHealingToAllTargets()
		{
			foreach (var target in m_healingTargetsList)
			{
				if (target.Health == null || target.Health.Health <= 0f) continue;

				// Lógica de curación de salud
				if (target.NeedsHealthHeal)
				{
					target.Health.Heal(100f);
				}

				// Lógica de curación de enfermedades
				if (target.NeedsDiseaseCure && target.Entity != null)
				{
					CureDiseases(target.Entity);
				}
			}
		}

		private void ShowHealingMessages()
		{
			if (m_healingTargetsList.Count == 0) return;

			string healerName = m_componentCreature.DisplayName;
			bool playedSound = false;

			foreach (var target in m_healingTargetsList)
			{
				if (target.Entity == null) continue;

				ComponentPlayer targetPlayer = target.Entity.FindComponent<ComponentPlayer>();
				if (targetPlayer != null)
				{
					// Reproducir sonido solo una vez
					if (!playedSound && target.Body != null)
					{
						m_subsystemAudio.PlaySound("Audio/classic intro smb melee", 1f, 0f, target.Body.Position, 3f, true);
						playedSound = true;
					}

					if (targetPlayer.ComponentGui != null)
					{
						// Mensaje de curación de enfermedad (verde)
						if (target.NeedsDiseaseCure)
						{
							string diseaseMessage = string.Format(LanguageControl.Get(fName, 1), healerName);
							targetPlayer.ComponentGui.DisplaySmallMessage(diseaseMessage, new Color(100, 255, 150), true, false);
						}

						// Mensaje de restauración de salud (rojo claro)
						if (target.NeedsHealthHeal)
						{
							string healthMessage = string.Format(LanguageControl.Get(fName, 2), healerName);
							targetPlayer.ComponentGui.DisplaySmallMessage(healthMessage, new Color(255, 150, 150), true, false);
						}
					}
				}
			}

			// Limpiar lista después de mostrar los mensajes
			m_healingTargetsList.Clear();
		}

		private void CureDiseases(Entity targetEntity)
		{
			if (targetEntity == null) return;

			// Verificar si es un jugador
			ComponentPlayer targetPlayer = targetEntity.FindComponent<ComponentPlayer>();

			if (targetPlayer != null)
			{
				// Si es jugador en modo Creativo o con mecánicas de supervivencia deshabilitadas, no curar enfermedades
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
				m_healingTargetsList.Clear();
				FindAllCriticalAllies();

				m_importanceLevel = (m_healingTargetsList.Count > 0) ? 100f : 0f;

				if (IsActive && m_healingTargetsList.Count > 0)
				{
					m_stateMachine.TransitionTo("Healing");
				}
			}, null);

			m_stateMachine.AddState("Healing", delegate
			{
				if (m_healingTargetsList.Count == 0 || m_random.Float() > m_probabilityOfCuring)
				{
					m_importanceLevel = 0f;
					m_healingTargetsList.Clear();
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				// Iniciamos el proceso para todos los objetivos al mismo tiempo
				m_healingTimer = 1.5f;

				m_stateMachine.TransitionTo("Idle");
			}, null, null);

			m_stateMachine.TransitionTo("Idle");
		}

		private void FindAllCriticalAllies()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;

			// Verificar uno mismo - salud baja
			if (m_doesHealSelf && m_componentCreature.ComponentHealth.Health <= 0.2f && m_componentCreature.ComponentHealth.Health > 0f)
			{
				m_healingTargetsList.Add(new HealingTargetData
				{
					Health = m_componentCreature.ComponentHealth,
					Entity = Entity,
					Body = m_componentCreature.ComponentBody,
					NeedsHealthHeal = true,
					NeedsDiseaseCure = HasDisease(Entity, true)
				});
			}
			// Verificar uno mismo - enfermedades (solo si no fue añadido por salud baja)
			else if (m_canCureSelf && HasDisease(Entity, true))
			{
				m_healingTargetsList.Add(new HealingTargetData
				{
					Health = m_componentCreature.ComponentHealth,
					Entity = Entity,
					Body = m_componentCreature.ComponentBody,
					NeedsHealthHeal = false,
					NeedsDiseaseCure = true
				});
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

							if (distance < m_healingRadius && playerHealth.Health > 0f)
							{
								bool needsHealthHeal = playerHealth.Health <= 0.2f;
								bool needsDiseaseCure = HasDisease(playerData.ComponentPlayer.Entity, false);

								if (needsHealthHeal || needsDiseaseCure)
								{
									m_healingTargetsList.Add(new HealingTargetData
									{
										Health = playerHealth,
										Entity = playerData.ComponentPlayer.Entity,
										Body = playerData.ComponentPlayer.ComponentBody,
										NeedsHealthHeal = needsHealthHeal,
										NeedsDiseaseCure = needsDiseaseCure
									});
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

						if (herd != null && herd.HerdName == m_componentHerdBehavior.HerdName && creatureHealth.Health > 0f)
						{
							float distance = Vector3.Distance(position, creature.ComponentBody.Position);

							if (distance < m_healingRadius)
							{
								bool needsHealthHeal = creatureHealth.Health <= 0.2f;
								bool needsDiseaseCure = HasDisease(creature.Entity, false);

								if (needsHealthHeal || needsDiseaseCure)
								{
									m_healingTargetsList.Add(new HealingTargetData
									{
										Health = creatureHealth,
										Entity = creature.Entity,
										Body = creature.ComponentBody,
										NeedsHealthHeal = needsHealthHeal,
										NeedsDiseaseCure = needsDiseaseCure
									});
								}
							}
						}
					}
				}
			}
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

		private List<HealingTargetData> m_healingTargetsList = new List<HealingTargetData>();
		private List<HealingParticleSystem> m_particleSystems = new List<HealingParticleSystem>();
		private float m_healingTimer;
		private float m_messageTimer;
		private float m_healingRadius;
	}
}
