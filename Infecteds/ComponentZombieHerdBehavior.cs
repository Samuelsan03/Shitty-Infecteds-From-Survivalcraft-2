using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieHerdBehavior : ComponentBehavior, IUpdateable
	{
		public string HerdName { get; set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (!m_autoNearbyCreaturesHelp || target == null)
			{
				return;
			}

			// CONDICIÓN CLAVE: No llamar zombies aliados (HerdName = "Zombie") para que ataquen a otro zombie
			ComponentZombieHerdBehavior targetHerd = target.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (targetHerd != null && targetHerd.HerdName == "Zombie")
			{
				return;
			}

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position) < 256f)
				{
					ComponentZombieHerdBehavior componentZombieHerdBehavior = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName) && componentZombieHerdBehavior.HerdName == HerdName && componentZombieHerdBehavior.m_autoNearbyCreaturesHelp)
					{
						ComponentZombieChaseBehavior componentZombieChaseBehavior = componentCreature.Entity.FindComponent<ComponentZombieChaseBehavior>();
						if (componentZombieChaseBehavior != null && componentZombieChaseBehavior.Target == null)
						{
							componentZombieChaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName))
			{
				return null;
			}
			Vector3 position = m_componentCreature.ComponentBody.Position;
			int num = 0;
			Vector3 vector = Vector3.Zero;
			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior componentZombieHerdBehavior = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (componentZombieHerdBehavior != null && componentZombieHerdBehavior.HerdName == HerdName)
					{
						Vector3 position2 = componentCreature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, position2) < m_herdingRange * m_herdingRange)
						{
							vector += position2;
							num++;
						}
					}
				}
			}
			if (num > 0)
			{
				return new Vector3?(vector / (float)num);
			}
			return null;
		}

		public virtual void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
			m_dt = dt;
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			HerdName = valuesDictionary.GetValue<string>("HerdName");
			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
			}));
			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(GetHashCode() % 256) / 256f))))
				{
					Vector3? vector = FindHerdCenter();
					if (vector != null)
					{
						float num = Vector3.Distance(vector.Value, m_componentCreature.ComponentBody.Position);
						if (num > 10f)
						{
							m_importanceLevel = 1f;
						}
						if (num > 12f)
						{
							m_importanceLevel = 3f;
						}
						if (num > 16f)
						{
							m_importanceLevel = 50f;
						}
						if (num > 20f)
						{
							m_importanceLevel = 250f;
						}
					}
				}
				if (IsActive)
				{
					m_stateMachine.TransitionTo("Herd");
				}
			}, null);
			m_stateMachine.AddState("Stuck", delegate
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					m_importanceLevel = 0f;
				}
			}, null, null);
			m_stateMachine.AddState("Herd", delegate
			{
				Vector3? vector = FindHerdCenter();
				if (vector != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, vector.Value) > 6f)
				{
					float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
					int maxPathfindingPositions = (m_importanceLevel > 200f) ? 100 : 0;
					m_componentPathfinding.SetDestination(new Vector3?(vector.Value), speed, 7f, maxPathfindingPositions, false, true, false, null);
					return;
				}
				m_importanceLevel = 0f;
			}, delegate
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;
				if (m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("Stuck");
				}
				if (m_componentPathfinding.Destination == null)
				{
					m_importanceLevel = 0f;
				}
				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
				{
					m_look = new Vector2(MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f));
				}
			}, null);
		}

		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public StateMachine m_stateMachine = new StateMachine();
		public float m_dt;
		public float m_importanceLevel;
		public Random m_random = new Random();
		public Vector2 m_look;
		public float m_herdingRange;
		public bool m_autoNearbyCreaturesHelp;
	}
}
