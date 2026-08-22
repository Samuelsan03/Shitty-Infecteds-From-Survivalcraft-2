using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Gravity : ComponentBehavior, IUpdateable
	{
		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private StateMachine m_stateMachine = new StateMachine();

		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentBody m_componentBody;
		private ComponentMiner m_componentMiner;

		private ComponentBehavior m_chaseBehavior;

		private float m_pushProbability = 0f;
		private float m_pushForce = 0f;

		private bool m_pushAppliedThisHit = false;
		private Random m_random = new Random();

		private const float MaxAttackRange = 1.75f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);

			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			if (m_chaseBehavior == null)
				m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			if (m_chaseBehavior == null)
				m_chaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			m_pushProbability = valuesDictionary.GetValue<float>("PushProbability", 0.3f);
			m_pushForce = valuesDictionary.GetValue<float>("PushForce", 15f);

			m_stateMachine.AddState("Idle",
				null,
				() =>
				{
					if (IsChaseActive())
						m_stateMachine.TransitionTo("Chasing");
				},
				null
			);

			m_stateMachine.AddState("Chasing",
				() => { m_pushAppliedThisHit = false; },
				() =>
				{
					if (!IsChaseActive())
					{
						m_stateMachine.TransitionTo("Idle");
						return;
					}

					if (m_componentCreatureModel.IsAttackHitMoment && !m_pushAppliedThisHit)
					{
						ComponentCreature target = GetTargetFromChase();
						if (target != null && target.ComponentHealth.Health > 0f)
						{
							Vector3 hitPoint;
							ComponentBody hitBody = GetHitBody(target.ComponentBody, out hitPoint);
							if (hitBody != null)
							{
								ApplyPush(target, hitBody, hitPoint);
							}
							m_pushAppliedThisHit = true;
						}
					}

					if (!m_componentCreatureModel.IsAttackHitMoment)
						m_pushAppliedThisHit = false;
				},
				null
			);

			m_stateMachine.TransitionTo("Idle");
		}

		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		private bool IsChaseActive()
		{
			if (m_chaseBehavior == null) return false;

			if (m_chaseBehavior is ComponentChaseBehavior chase)
				return chase.IsActive && chase.Target != null;
			if (m_chaseBehavior is ComponentNewChaseBehavior newChase)
				return newChase.IsActive && newChase.Target != null;
			if (m_chaseBehavior is ComponentZombieChaseBehavior zombieChase)
				return zombieChase.IsActive && zombieChase.Target != null;

			return false;
		}

		private ComponentCreature GetTargetFromChase()
		{
			if (m_chaseBehavior == null) return null;

			if (m_chaseBehavior is ComponentChaseBehavior chase)
				return chase.Target;
			if (m_chaseBehavior is ComponentNewChaseBehavior newChase)
				return newChase.Target;
			if (m_chaseBehavior is ComponentZombieChaseBehavior zombieChase)
				return zombieChase.Target;

			return null;
		}

		private ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 vector = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < MaxAttackRange && (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}

		private void ApplyPush(ComponentCreature target, ComponentBody hitBody, Vector3 hitPoint)
		{
			if (m_pushProbability <= 0f || m_pushForce <= 0f) return;
			if (m_random.Float(0f, 1f) > m_pushProbability) return;

			Vector3 hitDirection = m_componentBody.Matrix.Forward;

			m_componentMiner.Hit(hitBody, hitPoint, hitDirection);

			Vector3 pushDir = hitDirection + Vector3.UnitY * 0.5f;
			pushDir = Vector3.Normalize(pushDir);
			hitBody.ApplyImpulse(pushDir * m_pushForce * 1e7f);
		}
	}
}
