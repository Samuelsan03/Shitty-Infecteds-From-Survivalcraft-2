using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMonsterSkills : Component, IUpdateable
	{
		public enum VomitType
		{
			Fire,
			Poison,
			Freezing,
			Blood
		}

		public VomitType m_vomitType { get; set; }

		public bool CanVomitFire { get; set; }

		public bool CanVomitShit { get; set; }

		public bool CanVomitBlood { get; set; }

		public bool CanVomitFreezingCold { get; set; }

		public float TimeToVomitAgain { get; set; }

		public Vector2 DistanceToVomit { get; set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsVomiting => m_isVomiting;

		public float VomitCooldownRemaining => MathUtils.Max(m_vomitCooldownTimer, 0f);

		public float VomitDurationRemaining => MathUtils.Max(m_vomitDurationTimer, 0f);

		public float DurationOfVomiting { get; set; } = 5f;

		private bool CanVomit
		{
			get
			{
				if (m_vomitType == VomitType.Fire) return CanVomitFire;
				if (m_vomitType == VomitType.Poison) return CanVomitShit;
				if (m_vomitType == VomitType.Freezing) return CanVomitFreezingCold;
				if (m_vomitType == VomitType.Blood) return CanVomitBlood;
				return false;
			}
		}

		public ComponentCreature GetChaseTarget()
		{
			if (m_chaseBehavior == null) return null;

			if (m_chaseBehaviorType == typeof(ComponentChaseBehavior))
				return ((ComponentChaseBehavior)m_chaseBehavior).Target;
			if (m_chaseBehaviorType == typeof(ComponentNewChaseBehavior))
				return ((ComponentNewChaseBehavior)m_chaseBehavior).Target;
			if (m_chaseBehaviorType == typeof(ComponentZombieChaseBehavior))
				return ((ComponentZombieChaseBehavior)m_chaseBehavior).Target;

			return null;
		}

		public bool IsChasingActive()
		{
			if (m_chaseBehavior == null) return false;

			if (m_chaseBehaviorType == typeof(ComponentChaseBehavior))
				return ((ComponentChaseBehavior)m_chaseBehavior).IsActive;
			if (m_chaseBehaviorType == typeof(ComponentNewChaseBehavior))
				return ((ComponentNewChaseBehavior)m_chaseBehavior).IsActive;
			if (m_chaseBehaviorType == typeof(ComponentZombieChaseBehavior))
				return ((ComponentZombieChaseBehavior)m_chaseBehavior).IsActive;

			return false;
		}

		public bool IsTargetAlive(ComponentCreature target)
		{
			if (target == null) return false;

			ComponentHealth targetHealth = target.Entity.FindComponent<ComponentHealth>(false);
			if (targetHealth == null) return false;

			return targetHealth.Health > 0f;
		}

		private bool IsTargetInSight(ComponentCreature target)
		{
			if (target == null || target.ComponentBody == null) return false;

			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 monsterCenter = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 toTarget = targetCenter - monsterCenter;
			float distance = toTarget.Length();

			if (distance < 0.01f) return true;

			Vector3 directionToTarget = toTarget / distance;
			float dot = Vector3.Dot(forward, directionToTarget);

			return dot > 0f;
		}

		public void ForceStartVomiting()
		{
			if (!CanVomit) return;
			StartVomiting();
		}

		public void ForceStopVomiting()
		{
			StopVomiting();
		}

		public void Update(float dt)
		{
			if (!CanVomit || m_componentHealth.Health <= 0f)
			{
				if (m_isVomiting) StopVomiting();
				return;
			}

			if (!m_isVomiting && m_vomitCooldownTimer > 0f)
			{
				m_vomitCooldownTimer -= dt;
			}

			ComponentCreature target = GetChaseTarget();
			float distanceToTarget = float.MaxValue;

			bool targetIsDead = target != null && !IsTargetAlive(target);

			if (targetIsDead)
			{
				target = null;
			}

			if (target != null && target.ComponentBody != null)
			{
				distanceToTarget = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position);
			}

			if (m_isVomiting)
			{
				m_vomitDurationTimer -= dt;

				bool shouldStop = false;

				if (m_vomitDurationTimer <= 0f)
				{
					shouldStop = true;
				}
				else if (target == null || !IsChasingActive())
				{
					shouldStop = true;
				}
				else if (!IsTargetInSight(target))
				{
					shouldStop = true;
				}
				else if (distanceToTarget < DistanceToVomit.X)
				{
					shouldStop = true;
				}
				else if (distanceToTarget > DistanceToVomit.Y + 2f)
				{
					shouldStop = true;
				}

				if (shouldStop)
				{
					StopVomiting();
				}
				else
				{
					UpdateVomitTransform(target);
				}
			}
			else
			{
				if (m_vomitCooldownTimer <= 0f && target != null && !targetIsDead && IsChasingActive())
				{
					if (distanceToTarget >= DistanceToVomit.X && distanceToTarget <= DistanceToVomit.Y)
					{
						if (IsTargetInSight(target))
						{
							if (m_random.Float(0f, 1f) < 0.15f * dt)
							{
								StartVomiting();
							}
						}
					}
				}
			}
		}

		private void StartVomiting()
		{
			if (!CanVomit || m_isVomiting) return;

			ComponentCreature target = GetChaseTarget();

			if (target == null || !IsTargetAlive(target)) return;

			if (!IsTargetInSight(target)) return;

			GetNextVomitType();

			m_isVomiting = true;
			m_vomitDurationTimer = DurationOfVomiting;

			if (m_vomitType == VomitType.Fire)
			{
				if (m_fireVomitParticleSystem == null || m_fireVomitParticleSystem.IsStopped)
				{
					m_fireVomitParticleSystem = new FireVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemTime);
					m_fireVomitParticleSystem.OwnerBody = m_componentCreature.ComponentBody;
					m_fireVomitParticleSystem.Attacker = m_componentCreature;
					m_subsystemParticles.AddParticleSystem(m_fireVomitParticleSystem, false);
				}
				else
				{
					m_fireVomitParticleSystem.IsStopped = false;
				}
			}
			else if (m_vomitType == VomitType.Poison)
			{
				if (m_poisonVomitParticleSystem == null || m_poisonVomitParticleSystem.IsStopped)
				{
					m_poisonVomitParticleSystem = new PoisonVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemTime);
					m_poisonVomitParticleSystem.OwnerBody = m_componentCreature.ComponentBody;
					m_poisonVomitParticleSystem.Attacker = m_componentCreature;
					m_subsystemParticles.AddParticleSystem(m_poisonVomitParticleSystem, false);
				}
				else
				{
					m_poisonVomitParticleSystem.IsStopped = false;
				}
			}
			else if (m_vomitType == VomitType.Freezing)
			{
				if (m_freezingVomitParticleSystem == null || m_freezingVomitParticleSystem.IsStopped)
				{
					m_freezingVomitParticleSystem = new FreezingVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemTime);
					m_freezingVomitParticleSystem.OwnerBody = m_componentCreature.ComponentBody;
					m_freezingVomitParticleSystem.Attacker = m_componentCreature;
					m_subsystemParticles.AddParticleSystem(m_freezingVomitParticleSystem, false);
				}
				else
				{
					m_freezingVomitParticleSystem.IsStopped = false;
				}
			}
			else if (m_vomitType == VomitType.Blood)
			{
				if (m_bloodVomitParticleSystem == null || m_bloodVomitParticleSystem.IsStopped)
				{
					m_bloodVomitParticleSystem = new BloodVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemTime);
					m_bloodVomitParticleSystem.OwnerBody = m_componentCreature.ComponentBody;
					m_bloodVomitParticleSystem.Attacker = m_componentCreature;
					m_subsystemParticles.AddParticleSystem(m_bloodVomitParticleSystem, false);
				}
				else
				{
					m_bloodVomitParticleSystem.IsStopped = false;
				}
			}

			UpdateVomitTransform(target);
		}

		private void GetNextVomitType()
		{
			if (m_vomitQueue.Count == 0)
			{
				List<VomitType> types = new List<VomitType>();

				if (CanVomitFire) types.Add(VomitType.Fire);
				if (CanVomitShit) types.Add(VomitType.Poison);
				if (CanVomitBlood) types.Add(VomitType.Blood);
				if (CanVomitFreezingCold) types.Add(VomitType.Freezing);

				for (int i = 0; i < types.Count; i++)
				{
					int j = m_random.Int(i, types.Count - 1);
					VomitType temp = types[i];
					types[i] = types[j];
					types[j] = temp;
				}

				foreach (VomitType type in types)
				{
					m_vomitQueue.Enqueue(type);
				}
			}

			m_vomitType = m_vomitQueue.Dequeue();
		}

		private void StopVomiting()
		{
			if (!m_isVomiting) return;

			m_isVomiting = false;
			m_vomitCooldownTimer = TimeToVomitAgain;

			if (m_fireVomitParticleSystem != null)
			{
				m_fireVomitParticleSystem.IsStopped = true;
				m_fireVomitParticleSystem = null;
			}

			if (m_poisonVomitParticleSystem != null)
			{
				m_poisonVomitParticleSystem.IsStopped = true;
				m_poisonVomitParticleSystem = null;
			}

			if (m_freezingVomitParticleSystem != null)
			{
				m_freezingVomitParticleSystem.IsStopped = true;
				m_freezingVomitParticleSystem = null;
			}

			if (m_bloodVomitParticleSystem != null)
			{
				m_bloodVomitParticleSystem.IsStopped = true;
				m_bloodVomitParticleSystem = null;
			}
		}

		private void UpdateVomitTransform(ComponentCreature target)
		{
			if (m_componentCreatureModel == null) return;
			if (!IsTargetAlive(target)) return;

			Vector3 upVector = m_componentCreatureModel.EyeRotation.GetUpVector();
			Vector3 forwardVector = m_componentCreatureModel.EyeRotation.GetForwardVector();

			Vector3 mouthPos = m_componentCreatureModel.EyePosition - 0.08f * upVector + 0.3f * forwardVector;

			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 toTarget = targetCenter - mouthPos;
			float distance = toTarget.Length();

			Vector3 direction;
			if (distance > 0.01f)
			{
				direction = Vector3.Normalize(toTarget);
			}
			else
			{
				direction = Vector3.Normalize(forwardVector + 0.5f * upVector);
			}

			if (m_vomitType == VomitType.Fire && m_fireVomitParticleSystem != null)
			{
				m_fireVomitParticleSystem.Position = mouthPos;
				m_fireVomitParticleSystem.Direction = direction;
			}
			else if (m_vomitType == VomitType.Poison && m_poisonVomitParticleSystem != null)
			{
				m_poisonVomitParticleSystem.Position = mouthPos;
				m_poisonVomitParticleSystem.Direction = direction;
			}
			else if (m_vomitType == VomitType.Freezing && m_freezingVomitParticleSystem != null)
			{
				m_freezingVomitParticleSystem.Position = mouthPos;
				m_freezingVomitParticleSystem.Direction = direction;
			}
			else if (m_vomitType == VomitType.Blood && m_bloodVomitParticleSystem != null)
			{
				m_bloodVomitParticleSystem.Position = mouthPos;
				m_bloodVomitParticleSystem.Direction = direction;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);

			CanVomitFire = valuesDictionary.GetValue<bool>("CanVomitFire", false);
			CanVomitShit = valuesDictionary.GetValue<bool>("CanVomitShit", false);
			CanVomitBlood = valuesDictionary.GetValue<bool>("CanVomitBlood", false);
			CanVomitFreezingCold = valuesDictionary.GetValue<bool>("CanVomitFreezingCold", false);
			TimeToVomitAgain = valuesDictionary.GetValue<float>("TimeToVomitAgain", 5f);
			DistanceToVomit = valuesDictionary.GetValue<Vector2>("DistanceToVomit", new Vector2(3f, 10f));

			if (CanVomitFreezingCold)
			{
				m_vomitType = VomitType.Freezing;
			}
			else if (CanVomitBlood)
			{
				m_vomitType = VomitType.Blood;
			}
			else if (CanVomitShit)
			{
				m_vomitType = VomitType.Poison;
			}
			else
			{
				m_vomitType = VomitType.Fire;
			}

			ComponentChaseBehavior chase1 = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			ComponentNewChaseBehavior chase2 = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			ComponentZombieChaseBehavior chase3 = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			if (chase1 != null)
			{
				m_chaseBehavior = chase1;
				m_chaseBehaviorType = typeof(ComponentChaseBehavior);
			}
			else if (chase2 != null)
			{
				m_chaseBehavior = chase2;
				m_chaseBehaviorType = typeof(ComponentNewChaseBehavior);
			}
			else if (chase3 != null)
			{
				m_chaseBehavior = chase3;
				m_chaseBehaviorType = typeof(ComponentZombieChaseBehavior);
			}

			m_vomitCooldownTimer = TimeToVomitAgain * 0.3f;
		}

		public override void OnEntityRemoved()
		{
			if (m_isVomiting)
			{
				StopVomiting();
			}
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentHealth m_componentHealth;
		public float m_vomitCooldownTimer;
		public float m_vomitDurationTimer;
		public FireVomitParticleSystem m_fireVomitParticleSystem;
		public PoisonVomitParticleSystem m_poisonVomitParticleSystem;
		public FreezingVomitParticleSystem m_freezingVomitParticleSystem;
		public BloodVomitParticleSystem m_bloodVomitParticleSystem;
		public bool m_isVomiting;
		public object m_chaseBehavior;
		public Type m_chaseBehaviorType;
		public Random m_random = new Random();
		public Queue<VomitType> m_vomitQueue = new Queue<VomitType>();
	}
}
