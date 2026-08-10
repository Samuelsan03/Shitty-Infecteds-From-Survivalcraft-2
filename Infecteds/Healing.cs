using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Healing : ComponentBehavior, IUpdateable
	{
		private float m_probabilityOfCuring;

		private bool m_doesHealAllies;

		private bool m_doesHealSelf;

		private bool m_canCureOtherCreatures;

		private bool m_canCureSelf;

		private float m_healingRadius = 50f;

		private const float DyingThreshold = 0.2f;

		private const float HealingDuration = 3f;

		private SubsystemTime m_subsystemTime;

		private SubsystemGameInfo m_subsystemGameInfo; // Añadido para checkear modo de juego

		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;

		private SubsystemParticles m_subsystemParticles;

		private SubsystemAudio m_subsystemAudio;

		private SubsystemPlayers m_subsystemPlayers;

		private ComponentCreature m_componentCreature;

		private ComponentCreatureModel m_componentCreatureModel;

		private ComponentHealth m_componentHealth;

		private StateMachine m_stateMachine = new StateMachine();

		private float m_importanceLevel;

		private float m_dt;

		private HealingParticleSystem m_healerParticleSystem;

		private HealingParticleSystem m_targetParticleSystem;

		private ComponentCreature m_healingTarget;

		private float m_healingDurationTimer;

		private float m_cooldownTimer;

		private Random m_random = new Random();

		private bool m_targetNeedsDiseaseCure;

		private bool m_targetNeedsHealthRestore;

		public static string fName = "Healing";

		public override float ImportanceLevel
		{
			get
			{
				return m_importanceLevel;
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
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true); // Cargar subsistema
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_probabilityOfCuring = valuesDictionary.GetValue<float>("ProbabilityOfCuring");
			m_doesHealAllies = valuesDictionary.GetValue<bool>("DoesHealAllies");
			m_doesHealSelf = valuesDictionary.GetValue<bool>("DoesHealSelf");
			m_canCureOtherCreatures = valuesDictionary.GetValue<bool>("CanCureOtherCreatures");
			m_canCureSelf = valuesDictionary.GetValue<bool>("CanCureSelf");

			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= m_dt;
				}
				else if (m_componentHealth.Health > 0f)
				{
					ComponentCreature target = FindCreatureNeedingHelp();
					if (target != null)
					{
						m_healingTarget = target;
						m_importanceLevel = 10f;
						m_stateMachine.TransitionTo("Healing");
					}
				}
			}, null);

			m_stateMachine.AddState("Healing", delegate
			{
				StartHealingEffects();
			}, delegate
			{
				m_componentCreatureModel.AimHandAngleOrder = 3.2f;

				if (m_healerParticleSystem != null && !m_healerParticleSystem.Stopped)
				{
					m_healerParticleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				}
				if (m_targetParticleSystem != null && !m_targetParticleSystem.Stopped && m_healingTarget != null)
				{
					m_targetParticleSystem.BoundingBox = m_healingTarget.ComponentBody.BoundingBox;
				}

				m_healingDurationTimer -= m_dt;

				if (m_healingDurationTimer <= 0f)
				{
					if (m_random.Float(0f, 1f) < m_probabilityOfCuring)
					{
						PerformHealing();
					}
					StopHealingEffects();
					m_importanceLevel = 0f;
					m_stateMachine.TransitionTo("Inactive");
					return;
				}

				if (m_healingTarget == null || m_healingTarget.ComponentHealth.Health <= 0f)
				{
					StopHealingEffects();
					m_importanceLevel = 0f;
					m_stateMachine.TransitionTo("Inactive");
				}
			}, delegate
			{
				StopHealingEffects();
			});
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
		}

		public void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
			m_dt = dt;
			m_stateMachine.Update();
		}

		private bool IsCreatureSick(ComponentCreature creature)
		{
			if (creature == null) return false;

			ComponentInfectedWithPoison poison = creature.Entity.FindComponent<ComponentInfectedWithPoison>();
			if (poison != null && poison.IsInfected) return true;

			ComponentCreatureFlu creatureFlu = creature.Entity.FindComponent<ComponentCreatureFlu>();
			if (creatureFlu != null && creatureFlu.HasFlu) return true;

			ComponentFlu playerFlu = creature.Entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu) return true;

			ComponentSickness sickness = creature.Entity.FindComponent<ComponentSickness>();
			if (sickness != null && sickness.IsSick) return true;

			return false;
		}

		private bool IsCreatureDying(ComponentCreature creature)
		{
			if (creature == null) return false;
			return creature.ComponentHealth.Health > 0f && creature.ComponentHealth.Health <= DyingThreshold;
		}

		private ComponentCreature FindCreatureNeedingHelp()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			float radiusSquared = m_healingRadius * m_healingRadius;

			if (m_componentHealth.Health > 0f)
			{
				if (m_canCureSelf && IsCreatureSick(m_componentCreature))
				{
					m_targetNeedsDiseaseCure = true;
					m_targetNeedsHealthRestore = false;
					return m_componentCreature;
				}
				if (m_doesHealSelf && IsCreatureDying(m_componentCreature))
				{
					m_targetNeedsDiseaseCure = false;
					m_targetNeedsHealthRestore = true;
					return m_componentCreature;
				}
			}

			if (m_doesHealAllies || m_canCureOtherCreatures)
			{
				if (m_subsystemPlayers != null)
				{
					foreach (PlayerData playerData in m_subsystemPlayers.PlayersData)
					{
						if (playerData.ComponentPlayer != null)
						{
							ComponentCreature playerCreature = playerData.ComponentPlayer;
							if (playerCreature.ComponentHealth.Health > 0f && Vector3.DistanceSquared(position, playerCreature.ComponentBody.Position) < radiusSquared)
							{
								if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || !m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
								{
									// En creativo solo curar salud, NO enfermedades
									if (m_doesHealAllies && IsCreatureDying(playerCreature))
									{
										m_targetNeedsDiseaseCure = false;
										m_targetNeedsHealthRestore = true;
										return playerCreature;
									}
									continue;
								}
								// ----------------------------------------------------------------------------------------

								if (m_canCureOtherCreatures && IsCreatureSick(playerCreature))
								{
									m_targetNeedsDiseaseCure = true;
									m_targetNeedsHealthRestore = false;
									return playerCreature;
								}
								if (m_doesHealAllies && IsCreatureDying(playerCreature))
								{
									m_targetNeedsDiseaseCure = false;
									m_targetNeedsHealthRestore = true;
									return playerCreature;
								}
							}
						}
					}
				}

				ComponentNewHerdBehavior herdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
				{
					foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature != m_componentCreature && creature.ComponentHealth.Health > 0f && Vector3.DistanceSquared(position, creature.ComponentBody.Position) < radiusSquared)
						{
							ComponentNewHerdBehavior otherHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (otherHerd != null && otherHerd.HerdName == herdBehavior.HerdName)
							{
								if (m_canCureOtherCreatures && IsCreatureSick(creature))
								{
									m_targetNeedsDiseaseCure = true;
									m_targetNeedsHealthRestore = false;
									return creature;
								}
								if (m_doesHealAllies && IsCreatureDying(creature))
								{
									m_targetNeedsDiseaseCure = false;
									m_targetNeedsHealthRestore = true;
									return creature;
								}
							}
						}
					}
				}
				else
				{
					foreach (ComponentCreature creature2 in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature2 != m_componentCreature && creature2.ComponentHealth.Health > 0f && Vector3.DistanceSquared(position, creature2.ComponentBody.Position) < radiusSquared)
						{
							if (m_canCureOtherCreatures && IsCreatureSick(creature2))
							{
								m_targetNeedsDiseaseCure = true;
								m_targetNeedsHealthRestore = false;
								return creature2;
							}
							if (m_doesHealAllies && IsCreatureDying(creature2))
							{
								m_targetNeedsDiseaseCure = false;
								m_targetNeedsHealthRestore = true;
								return creature2;
							}
						}
					}
				}
			}

			return null;
		}

		private void StartHealingEffects()
		{
			m_componentCreatureModel.AimHandAngleOrder = 3.2f;
			m_healingDurationTimer = HealingDuration;

			if (m_healerParticleSystem == null)
			{
				m_healerParticleSystem = new HealingParticleSystem();
				m_healerParticleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_subsystemParticles.AddParticleSystem(m_healerParticleSystem, false);

				Vector3 audioPosition = (m_healingTarget != null) ? m_healingTarget.ComponentBody.Position : m_componentCreature.ComponentBody.Position;
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, audioPosition, 3f, false);
			}
			else if (m_healerParticleSystem.Stopped)
			{
				m_healerParticleSystem = new HealingParticleSystem();
				m_healerParticleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				m_subsystemParticles.AddParticleSystem(m_healerParticleSystem, false);
			}

			if (m_healingTarget != null && m_healingTarget != m_componentCreature && (m_targetParticleSystem == null || m_targetParticleSystem.Stopped))
			{
				m_targetParticleSystem = new HealingParticleSystem();
				m_targetParticleSystem.BoundingBox = m_healingTarget.ComponentBody.BoundingBox;
				m_subsystemParticles.AddParticleSystem(m_targetParticleSystem, false);
			}
		}

		private void StopHealingEffects()
		{
			m_componentCreatureModel.AimHandAngleOrder = 0f;

			if (m_healerParticleSystem != null)
			{
				m_healerParticleSystem.Stopped = true;
			}

			if (m_targetParticleSystem != null)
			{
				m_targetParticleSystem.Stopped = true;
				m_targetParticleSystem = null;
			}

			m_healingTarget = null;
			m_targetNeedsDiseaseCure = false;
			m_targetNeedsHealthRestore = false;
			m_cooldownTimer = 0f;
		}

		private void CureAllDiseases(ComponentCreature target)
		{
			if (target == null) return;

			ComponentInfectedWithPoison poison = target.Entity.FindComponent<ComponentInfectedWithPoison>();
			if (poison != null && poison.IsInfected) CureInfectedWithPoison(poison);

			ComponentCreatureFlu creatureFlu = target.Entity.FindComponent<ComponentCreatureFlu>();
			if (creatureFlu != null && creatureFlu.HasFlu) CureCreatureFlu(creatureFlu);

			ComponentFlu playerFlu = target.Entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu) CurePlayerFlu(playerFlu);

			ComponentSickness sickness = target.Entity.FindComponent<ComponentSickness>();
			if (sickness != null && sickness.IsSick) CureSickness(sickness);
		}

		private void CureInfectedWithPoison(ComponentInfectedWithPoison poison)
		{
			try
			{
				Type type = typeof(ComponentInfectedWithPoison);
				BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

				MethodInfo clearMethod = type.GetMethod("ClearAllEffects", flags);
				if (clearMethod != null)
				{
					clearMethod.Invoke(poison, null);
					return;
				}

				type.GetField("m_infectionDuration", flags)?.SetValue(poison, 0f);
				type.GetField("m_poisonIntensity", flags)?.SetValue(poison, 0f);
				type.GetField("m_greenoutDuration", flags)?.SetValue(poison, 0f);
				type.GetField("m_greenoutFactor", flags)?.SetValue(poison, 0f);
				type.GetField("m_pukeParticleSystem", flags)?.SetValue(poison, null);
				type.GetField("m_lastNauseaTime", flags)?.SetValue(poison, null);
				type.GetField("m_lastMoanTime", flags)?.SetValue(poison, null);
				type.GetField("m_firstVomitQueued", flags)?.SetValue(poison, false);
				type.GetField("m_firstVomitTimer", flags)?.SetValue(poison, -1f);

				FieldInfo speedsStoredField = type.GetField("m_speedsStored", flags);
				if (speedsStoredField != null && (bool)speedsStoredField.GetValue(poison))
				{
					MethodInfo restoreMethod = type.GetMethod("RestoreOriginalSpeeds", flags);
					restoreMethod?.Invoke(poison, null);
				}
			}
			catch (Exception) { }
		}

		private void CureCreatureFlu(ComponentCreatureFlu flu)
		{
			try
			{
				Type type = typeof(ComponentCreatureFlu);
				BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

				type.GetField("m_fluDuration", flags)?.SetValue(flu, 0f);
				type.GetField("m_coughDuration", flags)?.SetValue(flu, 0f);
				type.GetField("m_sneezeDuration", flags)?.SetValue(flu, 0f);
				type.GetField("m_lastEffectTime", flags)?.SetValue(flu, -1000.0);
				type.GetField("m_lastCoughTime", flags)?.SetValue(flu, -1000.0);
			}
			catch (Exception) { }
		}

		private void CurePlayerFlu(ComponentFlu flu)
		{
			flu.m_fluDuration = 0f;
			flu.m_fluOnset = 0f;
			flu.m_coughDuration = 0f;
			flu.m_sneezeDuration = 0f;
			flu.m_blackoutDuration = 0f;
			flu.m_blackoutFactor = 0f;
			flu.m_lastEffectTime = -1000.0;
			flu.m_lastCoughTime = -1000.0;

			if (flu.m_componentPlayer != null && flu.m_componentPlayer.ComponentScreenOverlays != null)
			{
				flu.m_componentPlayer.ComponentScreenOverlays.BlackoutFactor = 0f;
			}
		}

		private void CureSickness(ComponentSickness sickness)
		{
			sickness.m_sicknessDuration = 0f;
			sickness.m_greenoutDuration = 0f;
			sickness.m_greenoutFactor = 0f;
			sickness.m_lastNauseaTime = null;
			sickness.m_pukeParticleSystem = null;
			sickness.m_lastMessageTime = null;
			sickness.m_lastPukeTime = null;

			if (sickness.m_componentPlayer != null && sickness.m_componentPlayer.ComponentScreenOverlays != null)
			{
				sickness.m_componentPlayer.ComponentScreenOverlays.GreenoutFactor = 0f;
			}
		}

		private void PerformHealing()
		{
			if (m_healingTarget == null || m_healingTarget.ComponentHealth.Health <= 0f)
				return;

			bool diseaseCured = false;
			bool healthRestored = false;

			// Si cura la enfermedad, prohíbe explícitamente restaurar la salud en este ciclo
			if (m_targetNeedsDiseaseCure)
			{
				CureAllDiseases(m_healingTarget);
				diseaseCured = true;
				m_targetNeedsHealthRestore = false;
			}

			// Solo restaura la salud si NO fue una cura de enfermedad
			if (m_targetNeedsHealthRestore)
			{
				float neededHealth = 1f - m_healingTarget.ComponentHealth.Health;
				if (neededHealth > 0f && m_healingTarget.ComponentHealth.HealFactor > 0f)
				{
					m_healingTarget.ComponentHealth.Heal(neededHealth / m_healingTarget.ComponentHealth.HealFactor);
				}
				else
				{
					m_healingTarget.ComponentHealth.Health = 1f;
				}
				healthRestored = true;
			}

			ComponentPlayer player = m_healingTarget as ComponentPlayer;
			if (player != null && player.ComponentGui != null)
			{
				string message = null;
				if (diseaseCured)
				{
					message = string.Format(LanguageControl.Get(fName, 1), m_componentCreature.DisplayName);
				}
				else if (healthRestored)
				{
					message = string.Format(LanguageControl.Get(fName, 2), m_componentCreature.DisplayName);
				}

				if (!string.IsNullOrEmpty(message))
				{
					player.ComponentGui.DisplaySmallMessage(message, new Color(0, 255, 128), false, false);
					m_subsystemAudio.PlaySound("Audio/classic intro smb melee", 1f, 0f, 0f, 0f);
				}
			}
		}
	}
}
