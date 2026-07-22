using System;
using System.Collections.Generic;
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

		private float m_healingRadius = 50f;

		private const float DyingThreshold = 0.2f;

		private const float HealingDuration = 3f;

		private SubsystemTime m_subsystemTime;

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

			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= m_dt;
				}
				else if (m_componentHealth.Health > 0f)
				{
					ComponentCreature target = FindDyingCreature();
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

				if (m_healerParticleSystem != null)
				{
					m_healerParticleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
				}
				if (m_targetParticleSystem != null && m_healingTarget != null)
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

		private ComponentCreature FindDyingCreature()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			float radiusSquared = m_healingRadius * m_healingRadius;

			if (m_doesHealSelf && m_componentHealth.Health > 0f && m_componentHealth.Health <= DyingThreshold)
			{
				return m_componentCreature;
			}

			if (m_doesHealAllies)
			{
				if (m_subsystemPlayers != null)
				{
					foreach (PlayerData playerData in m_subsystemPlayers.PlayersData)
					{
						if (playerData.ComponentPlayer != null)
						{
							ComponentCreature playerCreature = playerData.ComponentPlayer;
							if (playerCreature.ComponentHealth.Health > 0f && playerCreature.ComponentHealth.Health <= DyingThreshold && Vector3.DistanceSquared(position, playerCreature.ComponentBody.Position) < radiusSquared)
							{
								return playerCreature;
							}
						}
					}
				}

				ComponentNewHerdBehavior herdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
				{
					foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature != m_componentCreature && creature.ComponentHealth.Health > 0f && creature.ComponentHealth.Health <= DyingThreshold && Vector3.DistanceSquared(position, creature.ComponentBody.Position) < radiusSquared)
						{
							ComponentNewHerdBehavior otherHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							if (otherHerd != null && otherHerd.HerdName == herdBehavior.HerdName)
							{
								return creature;
							}
						}
					}
				}
				else
				{
					foreach (ComponentCreature creature2 in m_subsystemCreatureSpawn.Creatures)
					{
						if (creature2 != m_componentCreature && creature2.ComponentHealth.Health > 0f && creature2.ComponentHealth.Health <= DyingThreshold && Vector3.DistanceSquared(position, creature2.ComponentBody.Position) < radiusSquared)
						{
							return creature2;
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
			}

			if (m_healingTarget != null && m_healingTarget != m_componentCreature && m_targetParticleSystem == null)
			{
				m_targetParticleSystem = new HealingParticleSystem();
				m_targetParticleSystem.BoundingBox = m_healingTarget.ComponentBody.BoundingBox;
				m_subsystemParticles.AddParticleSystem(m_targetParticleSystem, false);
			}

			Vector3 audioPosition = (m_healingTarget != null) ? m_healingTarget.ComponentBody.Position : m_componentCreature.ComponentBody.Position;
			m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, audioPosition, 3f, true);
		}

		private void StopHealingEffects()
		{
			m_componentCreatureModel.AimHandAngleOrder = 0f;

			if (m_healerParticleSystem != null)
			{
				m_healerParticleSystem.Stopped = true;
				m_healerParticleSystem = null;
			}

			if (m_targetParticleSystem != null)
			{
				m_targetParticleSystem.Stopped = true;
				m_targetParticleSystem = null;
			}

			m_healingTarget = null;
			m_cooldownTimer = 5f;
		}

		private void PerformHealing()
		{
			if (m_healingTarget != null && m_healingTarget.ComponentHealth.Health > 0f)
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

				ComponentPlayer player = m_healingTarget as ComponentPlayer;
				if (player != null && player.ComponentGui != null)
				{
					player.ComponentGui.DisplaySmallMessage("¡" + m_componentCreature.DisplayName + " te ha restaurado la salud!", new Color (0,255,128), false, false);
					m_subsystemAudio.PlaySound("Audio/classic intro smb melee", 1f, 0f, 0f, 0f);
				}
			}
		}
	}
}
