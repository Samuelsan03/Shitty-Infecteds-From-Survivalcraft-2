using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Gravity : ComponentBehavior, IUpdateable
	{
		public float PushProbability { get; set; }
		public float GravityForce { get; set; }

		public override float ImportanceLevel => m_importanceLevel;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private StateMachine m_stateMachine = new StateMachine();
		private Random m_random = new Random();

		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;

		private ComponentCreature m_target;
		private float m_lastTargetHealth;
		private float m_hitCooldown;
		private float m_importanceLevel;
		private float m_dt;

		private const float HitCooldownTime = 0.7f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = m_componentCreature.ComponentBody;

			PushProbability = valuesDictionary.GetValue<float>("PushProbability", 0.5f);
			GravityForce = valuesDictionary.GetValue<float>("Gravity", 13f);

			m_stateMachine.AddState("Idle", delegate
			{
				m_importanceLevel = 0f;
				m_target = null;
			}, delegate
			{
				m_hitCooldown -= m_dt;
				ComponentCreature target = GetChaseTarget();

				if (target != null && target.ComponentHealth != null && target.ComponentHealth.Health > 0f)
				{
					m_target = target;
					m_lastTargetHealth = target.ComponentHealth.Health;
					m_importanceLevel = 50f;
					IsActive = true;
					m_stateMachine.TransitionTo("Chasing");
				}
			}, null);

			m_stateMachine.AddState("Chasing", delegate
			{
				m_hitCooldown = 0f;
				if (m_target != null && m_target.ComponentHealth != null)
				{
					m_lastTargetHealth = m_target.ComponentHealth.Health;
				}
			}, delegate
			{
				m_hitCooldown -= m_dt;

				ComponentCreature newTarget = GetChaseTarget();

				if (newTarget != m_target)
				{
					if (newTarget != null)
					{
						m_target = newTarget;
						m_lastTargetHealth = newTarget.ComponentHealth?.Health ?? 0f;
					}
					else
					{
						m_target = null;
						m_importanceLevel = 0f;
						IsActive = false;
						m_stateMachine.TransitionTo("Idle");
						return;
					}
				}

				if (m_target == null || m_target.ComponentHealth == null || m_target.ComponentHealth.Health <= 0f)
				{
					m_target = null;
					m_importanceLevel = 0f;
					IsActive = false;
					m_stateMachine.TransitionTo("Idle");
					return;
				}

				float currentHealth = m_target.ComponentHealth.Health;
				if (currentHealth < m_lastTargetHealth && m_hitCooldown <= 0f)
				{
					m_stateMachine.TransitionTo("Hitting");
				}

				m_lastTargetHealth = currentHealth;
			}, null);

			m_stateMachine.AddState("Hitting", delegate
			{
				ApplyGravityPush(m_target);
				m_hitCooldown = HitCooldownTime;
			}, delegate
			{
				m_hitCooldown -= m_dt;

				if (m_hitCooldown <= 0f)
				{
					ComponentCreature target = GetChaseTarget();
					if (target != null)
					{
						m_target = target;
						m_lastTargetHealth = target.ComponentHealth?.Health ?? 0f;
						m_stateMachine.TransitionTo("Chasing");
					}
					else
					{
						m_target = null;
						m_importanceLevel = 0f;
						IsActive = false;
						m_stateMachine.TransitionTo("Idle");
					}
				}
			}, null);

			m_stateMachine.TransitionTo("Idle");
		}

		private ComponentCreature GetChaseTarget()
		{
			ComponentZombieChaseBehavior zombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zombieChase != null && zombieChase.IsActive && zombieChase.Target != null)
			{
				return zombieChase.Target;
			}

			ComponentNewChaseBehavior newChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase != null && newChase.IsActive && newChase.Target != null)
			{
				return newChase.Target;
			}

			return null;
		}

		private void ApplyGravityPush(ComponentCreature target)
		{
			if (target == null || target.ComponentBody == null)
				return;

			if (m_random.Float(0f, 1f) > PushProbability)
				return;

			Vector3 direction = target.ComponentBody.Position - m_componentBody.Position;
			float horizontalDistance = new Vector2(direction.X, direction.Z).Length();

			Vector3 pushDirection;
			if (horizontalDistance > 0.01f)
			{
				pushDirection = Vector3.Normalize(new Vector3(direction.X, 0f, direction.Z));
			}
			else
			{
				pushDirection = m_componentBody.Matrix.Forward;
				pushDirection.Y = 0f;
				if (pushDirection.LengthSquared() > 0.01f)
				{
					pushDirection = Vector3.Normalize(pushDirection);
				}
				else
				{
					pushDirection = Vector3.UnitZ;
				}
			}

			Vector3 finalDirection = Vector3.Normalize(pushDirection + Vector3.UnitY * 0.5f);
			target.ComponentBody.ApplyImpulse(finalDirection * 1e+9f * GravityForce);
		}

		public void Update(float dt)
		{
			m_dt = dt;
			m_stateMachine.Update();
		}
	}
}
