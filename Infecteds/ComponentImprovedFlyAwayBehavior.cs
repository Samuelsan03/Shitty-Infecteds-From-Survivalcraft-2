using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentImprovedFlyAwayBehavior : ComponentBehavior, IUpdateable, INoiseListener, IComponentEscapeBehavior
	{
		public float LowHealthToEscape { get; set; }

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

		public override bool IsActive
		{
			set
			{
				base.IsActive = value;
				if (this.IsActive)
				{
					this.m_nextUpdateTime = 0.0;
				}
			}
		}

		public virtual void Update(float dt)
		{
		}

		public virtual void HearNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape", 0.33f);
			this.AffectedByNoise = valuesDictionary.GetValue<bool>("AffectedByNoise", true);
			this.FanSound = valuesDictionary.GetValue<bool>("FanSound", true);
			this.m_stateMachine.AddState("Idle", null, null, null);
			this.m_stateMachine.TransitionTo("Idle");
		}

		public virtual bool ScanForDanger()
		{
			return false;
		}

		public virtual Vector3 FindSafePlace()
		{
			return this.m_componentCreature.ComponentBody.Position;
		}

		public virtual float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? lookDirection)
		{
			return float.MaxValue;
		}

		public virtual bool IsPredator(Entity entity)
		{
			return false;
		}

		public SubsystemTerrain m_subsystemTerrain;

		public SubsystemBodies m_subsystemBodies;

		public SubsystemAudio m_subsystemAudio;

		public SubsystemTime m_subsystemTime;

		public SubsystemNoise m_subsystemNoise;

		public ComponentCreature m_componentCreature;

		public ComponentPathfinding m_componentPathfinding;

		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		public Random m_random = new Random();

		public StateMachine m_stateMachine = new StateMachine();

		public float m_importanceLevel;

		public double m_nextUpdateTime;

		public bool AffectedByNoise;

		public bool FanSound;
	}
}
