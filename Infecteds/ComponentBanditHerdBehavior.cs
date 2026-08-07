using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditHerdBehavior : ComponentBehavior, IUpdateable
	{
		public string HerdName { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Si es bandido, verificar que el atacante no sea de la misma manada
			if (target != null && this.HerdName == "bandit")
			{
				ComponentHerdBehavior targetHerdBehavior = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerdBehavior != null && targetHerdBehavior.HerdName == "bandit")
				{
					// No llamar a la ayuda si el atacante es otro bandido de la manada
					return;
				}
			}

			if (target == null)
			{
				return;
			}

			Vector3 position = target.ComponentBody.Position;
			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position) < 256f)
				{
					ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName) && componentHerdBehavior.HerdName == this.HerdName && componentHerdBehavior.m_autoNearbyCreaturesHelp)
					{
						ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
						if (componentChaseBehavior != null && componentChaseBehavior.Target == null)
						{
							componentChaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
						}
					}
				}
			}
		}

		public Vector3? FindHerdCenter()
		{
			if (string.IsNullOrEmpty(this.HerdName))
			{
				return null;
			}

			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			int num = 0;
			Vector3 vector = Vector3.Zero;

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.ComponentHealth.Health > 0f)
				{
					ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior != null && componentHerdBehavior.HerdName == this.HerdName)
					{
						Vector3 position2 = componentCreature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, position2) < this.m_herdingRange * this.m_herdingRange)
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
			if (string.IsNullOrEmpty(this.m_stateMachine.CurrentState) || !this.IsActive)
			{
				this.m_stateMachine.TransitionTo("Inactive");
			}
			this.m_dt = dt;
			this.m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.HerdName = valuesDictionary.GetValue<string>("HerdName");
			this.m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
			this.m_autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");

			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				this.CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
			}));

			this.m_stateMachine.AddState("Inactive", null, delegate
			{
				if (this.m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)(1f * ((float)(this.GetHashCode() % 256) / 256f))))
				{
					Vector3? vector = this.FindHerdCenter();
					if (vector != null)
					{
						float num = Vector3.Distance(vector.Value, this.m_componentCreature.ComponentBody.Position);
						if (num > 10f)
						{
							this.m_importanceLevel = 1f;
						}
						if (num > 12f)
						{
							this.m_importanceLevel = 3f;
						}
						if (num > 16f)
						{
							this.m_importanceLevel = 50f;
						}
						if (num > 20f)
						{
							this.m_importanceLevel = 250f;
						}
					}
				}
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Herd");
				}
			}, null);

			this.m_stateMachine.AddState("Stuck", delegate
			{
				this.m_stateMachine.TransitionTo("Herd");
				if (this.m_random.Bool(0.5f))
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
					this.m_importanceLevel = 0f;
				}
			}, null, null);

			this.m_stateMachine.AddState("Herd", delegate
			{
				Vector3? vector = this.FindHerdCenter();
				if (vector != null && Vector3.Distance(this.m_componentCreature.ComponentBody.Position, vector.Value) > 6f)
				{
					float speed = (this.m_importanceLevel > 10f) ? this.m_random.Float(0.9f, 1f) : this.m_random.Float(0.25f, 0.35f);
					int maxPathfindingPositions = (this.m_importanceLevel > 200f) ? 100 : 0;
					this.m_componentPathfinding.SetDestination(new Vector3?(vector.Value), speed, 7f, maxPathfindingPositions, false, true, false, null);
					return;
				}
				this.m_importanceLevel = 0f;
			}, delegate
			{
				this.m_componentCreature.ComponentLocomotion.LookOrder = this.m_look - this.m_componentCreature.ComponentLocomotion.LookAngles;
				if (this.m_componentPathfinding.IsStuck)
				{
					this.m_stateMachine.TransitionTo("Stuck");
				}
				if (this.m_componentPathfinding.Destination == null)
				{
					this.m_importanceLevel = 0f;
				}
				if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				if (this.m_random.Float(0f, 1f) < 1.5f * this.m_dt)
				{
					this.m_look = new Vector2(MathUtils.DegToRad(45f) * this.m_random.Float(-1f, 1f), MathUtils.DegToRad(10f) * this.m_random.Float(-1f, 1f));
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
