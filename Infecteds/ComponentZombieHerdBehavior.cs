using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieHerdBehavior : ComponentBehavior, IUpdateable
	{
		// NUEVOS PARÁMETROS
		public string HerdName { get; set; }
		public float HerdingRange { get; set; }
		public float ImportanceLevelForHerd { get; set; }
		public float MinDistanceToHerd { get; set; }
		public bool AutoCallNearbyHelp { get; set; }
		public float HelpCallRange { get; set; }
		public float HelpChaseTime { get; set; }

		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private StateMachine m_stateMachine = new StateMachine();
		private float m_importanceLevel;
		private Random m_random = new Random();
		private Vector2 m_look;
		private float m_dt;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (!AutoCallNearbyHelp || target == null) return;

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, creature.ComponentBody.Position) < HelpCallRange * HelpCallRange)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == this.HerdName)
					{
						ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
						if (chase != null && chase.Target == null)
						{
							chase.Attack(target, HelpCallRange, HelpChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(HerdName)) return null;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			int count = 0;
			Vector3 center = Vector3.Zero;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior herd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (herd != null && herd.HerdName == this.HerdName)
					{
						Vector3 creaturePos = creature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, creaturePos) < HerdingRange * HerdingRange)
						{
							center += creaturePos;
							count++;
						}
					}
				}
			}

			return count > 0 ? center / (float)count : (Vector3?)null;
		}

		public void Update(float dt)
		{
			if (string.IsNullOrEmpty(m_stateMachine.CurrentState) || !IsActive)
				m_stateMachine.TransitionTo("Inactive");

			m_dt = dt;
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			// CARGAR NUEVOS PARÁMETROS DESDE XDB
			HerdName = valuesDictionary.GetValue<string>("HerdName");
			HerdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			ImportanceLevelForHerd = valuesDictionary.GetValue<float>("ImportanceLevelForHerd");
			MinDistanceToHerd = valuesDictionary.GetValue<float>("MinDistanceToHerd");
			AutoCallNearbyHelp = valuesDictionary.GetValue<bool>("AutoCallNearbyHelp");
			HelpCallRange = valuesDictionary.GetValue<float>("HelpCallRange");
			HelpChaseTime = valuesDictionary.GetValue<float>("HelpChaseTime");

			// SUSCRIBIRSE AL EVENTO DE DAÑO PARA LLAMAR AYUDA
			ComponentHealth health = m_componentCreature.ComponentHealth;
			health.Injured += delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (attacker != null && AutoCallNearbyHelp)
					CallNearbyCreaturesHelp(attacker, HelpCallRange, HelpChaseTime, false);
			};

			// ESTADO INACTIVO
			m_stateMachine.AddState("Inactive", null, delegate
			{
				if (m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(GetHashCode() % 256) / 256f))))
				{
					Vector3? center = FindHerdCenter();
					if (center != null)
					{
						float distance = Vector3.Distance(center.Value, m_componentCreature.ComponentBody.Position);
						if (distance > MinDistanceToHerd)
							m_importanceLevel = ImportanceLevelForHerd;
						else
							m_importanceLevel = 0f;
					}
				}

				if (IsActive && m_importanceLevel > 0f)
					m_stateMachine.TransitionTo("Herd");
			}, null);

			// ESTADO STUCK
			m_stateMachine.AddState("Stuck", delegate
			{
				m_stateMachine.TransitionTo("Herd");
				if (m_random.Bool(0.5f))
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			}, null, null);

			// ESTADO HERD
			m_stateMachine.AddState("Herd", delegate
			{
				Vector3? center = FindHerdCenter();
				if (center != null && Vector3.Distance(m_componentCreature.ComponentBody.Position, center.Value) > MinDistanceToHerd)
				{
					float speed = (m_importanceLevel > 10f) ? m_random.Float(0.9f, 1f) : m_random.Float(0.25f, 0.35f);
					int maxPathfinding = (m_importanceLevel > 200f) ? 100 : 0;
					m_componentPathfinding.SetDestination(center, speed, 7f, maxPathfinding, false, true, false, null);
				}
				else
				{
					m_importanceLevel = 0f;
				}
			}, delegate
			{
				m_componentCreature.ComponentLocomotion.LookOrder = m_look - m_componentCreature.ComponentLocomotion.LookAngles;

				if (m_componentPathfinding.IsStuck)
					m_stateMachine.TransitionTo("Stuck");

				if (m_componentPathfinding.Destination == null)
					m_importanceLevel = 0f;

				if (m_random.Float(0f, 1f) < 0.05f * m_dt)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);

				if (m_random.Float(0f, 1f) < 1.5f * m_dt)
					m_look = new Vector2(MathUtils.DegToRad(45f) * m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * m_random.Float(-1f, 1f));
			}, null);
		}
	}
}
