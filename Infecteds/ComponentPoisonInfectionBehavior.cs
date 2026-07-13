using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPoisonInfectionBehavior : ComponentBehavior, IUpdateable
	{
		private const float MaxAttackRange = 1.75f;

		public float m_poisonIntensity;
		public float m_probabilityOfPoisoning;

		public SubsystemTime m_subsystemTime;
		public SubsystemParticles m_subsystemParticles;
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
		private bool m_poisonAppliedThisSwing;

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
				&& !m_poisonAppliedThisSwing)
			{
				Vector3 hitPoint;
				ComponentBody hitBody = null;

				// Detecta cuál de los 2 chases posee y obtiene su target
				ComponentCreature currentTarget = null;
				if (m_componentNewChase != null)
				{
					currentTarget = m_componentNewChase.Target;
				}
				else if (m_componentZombieChase != null)
				{
					currentTarget = m_componentZombieChase.Target;
				}

				// Si tiene un target válido en su chase, usa el cálculo exacto de los chases
				if (currentTarget != null && currentTarget.ComponentBody != null)
				{
					hitBody = GetHitBody(currentTarget.ComponentBody, out hitPoint);
				}
				else
				{
					// Fallback directo al frente si el target es nulo en ese frame
					hitBody = GetHitBodyInAttackRange(out hitPoint);
				}

				if (hitBody != null && hitBody.Entity != null && hitBody.Entity != Entity)
				{
					m_pendingHitBody = hitBody;
					m_hasPendingHit = true;
					m_poisonAppliedThisSwing = true;
				}
			}

			if (m_componentCreatureModel != null && !m_componentCreatureModel.IsAttackHitMoment)
			{
				m_poisonAppliedThisSwing = false;
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

		public bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bb2 = target.BoundingBox;
			Vector3 c1 = 0.5f * (bb1.Min + bb1.Max);
			Vector3 c2 = 0.5f * (bb2.Min + bb2.Max) - c1;
			float len = c2.Length();
			if (len == 0f) return false;
			Vector3 dir = c2 / len;
			float width = 0.5f * (bb1.Max.X - bb1.Min.X + bb2.Max.X - bb2.Min.X);
			float height = 0.5f * (bb1.Max.Y - bb1.Min.Y + bb2.Max.Y - bb2.Min.Y);

			if (MathF.Abs(c2.Y) < height * 0.99f)
			{
				if (len < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (len < height + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		private void ApplyPoison(ComponentBody targetBody)
		{
			if (targetBody == null || targetBody.Entity == null)
				return;

			ComponentPlayer player = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (player != null && player.ComponentSickness != null)
			{
				player.ComponentSickness.StartSickness();
				return;
			}

			ComponentInfectedWithPoison infection = targetBody.Entity.FindComponent<ComponentInfectedWithPoison>();
			if (infection != null)
			{
				infection.TryInfect(m_poisonIntensity);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			// Busca cuál de los 2 chases posee la criatura (no requiere tener ambos)
			m_componentNewChase = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>();

			// Solo estos 2 parámetros
			m_poisonIntensity = valuesDictionary.GetValue<float>("PoisonIntensity");
			m_probabilityOfPoisoning = valuesDictionary.GetValue<float>("ProbabilityOfPoisoning");

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
					if (m_random.Float(0f, 1f) < m_probabilityOfPoisoning)
					{
						ApplyPoison(m_pendingHitBody);
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
