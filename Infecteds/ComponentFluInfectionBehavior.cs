using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFluInfectionBehavior : ComponentBehavior, IUpdateable
	{
		private const float MaxAttackRange = 1.75f;

		public float m_fluIntensity;
		public float m_probabilityOfFluInfection;

		public SubsystemTime m_subsystemTime;
		public ComponentCreature m_componentCreature;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentMiner m_componentMiner;
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public float m_importanceLevel;
		public double m_nextUpdateTime;
		public float m_dt;

		private ComponentNewChaseBehavior m_componentNewChase;
		private ComponentZombieChaseBehavior m_componentZombieChase;

		private ComponentBody m_pendingHitBody;
		private bool m_hasPendingHit;
		private bool m_fluAppliedThisSwing;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			if (m_componentCreature == null
				|| m_componentCreature.ComponentHealth == null
				|| m_componentCreature.ComponentHealth.Health <= 0f)
			{
				m_importanceLevel = 0f;
				return;
			}

			m_importanceLevel = 10f;

			if (m_componentCreatureModel != null
				&& m_componentCreatureModel.IsAttackHitMoment
				&& !m_fluAppliedThisSwing)
			{
				Vector3 hitPoint;
				ComponentBody hitBody = null;

				ComponentCreature currentTarget = null;
				if (m_componentNewChase != null)
				{
					currentTarget = m_componentNewChase.Target;
				}
				else if (m_componentZombieChase != null)
				{
					currentTarget = m_componentZombieChase.Target;
				}

				if (currentTarget != null && currentTarget.ComponentBody != null)
				{
					hitBody = GetHitBody(currentTarget.ComponentBody, out hitPoint);
				}
				else
				{
					hitBody = GetHitBodyInAttackRange(out hitPoint);
				}

				if (hitBody != null && hitBody.Entity != null && hitBody.Entity != Entity)
				{
					m_pendingHitBody = hitBody;
					m_hasPendingHit = true;
					m_fluAppliedThisSwing = true;
				}
			}

			if (m_componentCreatureModel != null && !m_componentCreatureModel.IsAttackHitMoment)
			{
				m_fluAppliedThisSwing = false;
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = 0.1f;
				m_nextUpdateTime = m_subsystemTime.GameTime + m_dt;
				m_stateMachine.Update();
			}
		}

		public ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 start = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 end = target.BoundingBox.Center();
			Ray3 ray = new Ray3(start, Vector3.Normalize(end - start));

			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (result != null && result.Value.Distance < MaxAttackRange)
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		public ComponentBody GetHitBodyInAttackRange(out Vector3 hitPoint)
		{
			Vector3 eye = m_componentCreatureModel.EyePosition;
			Vector3 forward = m_componentCreatureModel.EyeRotation.GetForwardVector();
			Ray3 ray = new Ray3(eye, forward);

			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(
				ray, RaycastMode.Interaction, true, true, true, MaxAttackRange);

			if (result != null && result.Value.Distance < MaxAttackRange)
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		private void ApplyFlu(ComponentBody targetBody)
		{
			if (targetBody == null || targetBody.Entity == null)
				return;

			ComponentPlayer player = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (player != null && player.ComponentFlu != null)
			{
				player.ComponentFlu.StartFlu();
				return;
			}

			ComponentCreatureFlu creatureFlu = targetBody.Entity.FindComponent<ComponentCreatureFlu>();
			if (creatureFlu != null)
			{
				creatureFlu.TryInfect(m_fluIntensity);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			m_componentNewChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>();

			m_fluIntensity = valuesDictionary.GetValue<float>("FluIntensity");
			m_probabilityOfFluInfection = valuesDictionary.GetValue<float>("ProbabilityOfFluInfection");

			m_stateMachine.AddState("Idle", null, delegate
			{
				if (m_hasPendingHit && m_pendingHitBody != null)
				{
					m_stateMachine.TransitionTo("Applying");
				}
			}, null);

			m_stateMachine.AddState("Applying", delegate
			{
				if (m_pendingHitBody != null)
				{
					if (m_random.Float(0f, 1f) < m_probabilityOfFluInfection)
					{
						ApplyFlu(m_pendingHitBody);
					}
				}
				m_hasPendingHit = false;
				m_pendingHitBody = null;
			}, delegate
			{
				m_stateMachine.TransitionTo("Idle");
			}, null);

			m_stateMachine.TransitionTo("Idle");
		}
	}
}
