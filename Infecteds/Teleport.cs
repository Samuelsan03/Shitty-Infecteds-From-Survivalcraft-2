using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class Teleport : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		private float m_teleportationRange;
		private float m_probabilityOfTeleporting;
		private float m_timeToTeleportAgain;
		private float m_timeMissingBeforeReappearing;

		private TeleportState m_currentState;
		private float m_teleportCooldownTimer;
		private float m_missingTimer;
		private Vector3 m_realTargetPosition;
		private ComponentCreature m_teleportTarget;
		private Random m_random;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = m_componentCreature.ComponentBody;
			m_componentNewChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>(true);

			m_teleportationRange = valuesDictionary.GetValue<float>("TeleportationRange");
			m_probabilityOfTeleporting = valuesDictionary.GetValue<float>("ProbabilityOfTeleporting");
			m_timeToTeleportAgain = valuesDictionary.GetValue<float>("TimeToTeleportAgain");
			m_timeMissingBeforeReappearing = valuesDictionary.GetValue<float>("TimeMissingBeforeReappearing");

			m_random = new Random();
			m_currentState = TeleportState.Idle;
			m_teleportCooldownTimer = 0f;
		}

		public void Update(float dt)
		{
			if (m_teleportCooldownTimer > 0f)
			{
				m_teleportCooldownTimer -= dt;
			}

			switch (m_currentState)
			{
				case TeleportState.Idle:
					HandleIdleState();
					break;
				case TeleportState.Disappearing:
					HandleDisappearingState(dt);
					break;
				case TeleportState.Appearing:
					HandleAppearingState();
					break;
			}
		}

		private ComponentCreature FindTeleportTarget()
		{
			// 1. Primero intentar usar el target actual del chase
			ComponentCreature chaseTarget = m_componentNewChaseBehavior.Target;
			if (chaseTarget != null && chaseTarget.ComponentHealth != null && chaseTarget.ComponentHealth.Health > 0f)
			{
				return chaseTarget;
			}

			// 2. Si el chase ya perdió el target, buscarlo nosotros mismos en un radio amplio
			Vector3 pos = m_componentBody.Position;
			float searchRadius = m_teleportationRange * 3f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), searchRadius, m_componentBodies);

			ComponentCreature bestTarget = null;
			float bestDistance = float.MaxValue;

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature == null || creature == m_componentCreature || !creature.Entity.IsAddedToProject)
					continue;
				if (creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f)
					continue;

				float distance = Vector3.Distance(pos, creature.ComponentBody.Position);

				// Solo nos interesan los que están FUERA del rango de teletransporte
				if (distance > m_teleportationRange && distance < bestDistance)
				{
					bestDistance = distance;
					bestTarget = creature;
				}
			}

			return bestTarget;
		}

		private void HandleIdleState()
		{
			if (m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = FindTeleportTarget();
			if (target == null)
				return;

			Vector3 myPosition = m_componentBody.Position;
			Vector3 targetPosition = target.ComponentBody.Position;

			float distanceToTarget = Vector3.Distance(myPosition, targetPosition);

			// Si está FUERA del rango configurado, permitir teletransportarse
			bool isOutOfRange = distanceToTarget > m_teleportationRange;

			if (isOutOfRange && m_teleportCooldownTimer <= 0f)
			{
				if (m_random.Float(0f, 1f) < m_probabilityOfTeleporting)
				{
					m_teleportTarget = target;
					StartDisappearing(targetPosition);
				}
			}
		}

		private void StartDisappearing(Vector3 targetPosition)
		{
			m_currentState = TeleportState.Disappearing;

			Vector3 particlePosition = m_componentBody.Position + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentBody.BoxSize.X;

			m_subsystemParticles.AddParticleSystem(new TeleportParticleSystem(m_subsystemTerrain, particlePosition, size), false);
			m_subsystemAudio.PlaySound("Audio/teleport 1", 1f, 0f, particlePosition, 4f, true);

			Vector2 randomDirection = m_random.Vector2();
			float randomDistance = m_random.Float(1.5f, m_teleportationRange);

			m_realTargetPosition = new Vector3(
				targetPosition.X + randomDirection.X * randomDistance,
				targetPosition.Y,
				targetPosition.Z + randomDirection.Y * randomDistance
			);

			// CORRECCIÓN DEL TIEMPO: Mandarlo al cielo en vez de bajo tierra.
			// Si lo mandabas bajo tierra (Y -= 100), el juego mataba a la criatura por tocar el vacío.
			// Al morir, el componente se reiniciaba y el enfriamiento volvía a 0, ignorando el XML.
			Vector3 hiddenPosition = m_realTargetPosition;
			hiddenPosition.Y += 500f;

			m_componentBody.IsGravityEnabled = false;
			m_componentBody.TerrainCollidable = false;
			m_componentBody.BodyCollidable = false;

			m_componentBody.Position = hiddenPosition;
			m_componentBody.Velocity = Vector3.Zero;

			m_missingTimer = m_timeMissingBeforeReappearing;
		}

		private void HandleDisappearingState(float dt)
		{
			m_missingTimer -= dt;
			if (m_missingTimer <= 0f)
			{
				m_currentState = TeleportState.Appearing;
			}
		}

		private void HandleAppearingState()
		{
			m_componentBody.Position = m_realTargetPosition;
			m_componentBody.Velocity = Vector3.Zero;

			m_componentBody.IsGravityEnabled = true;
			m_componentBody.TerrainCollidable = true;
			m_componentBody.BodyCollidable = true;

			Vector3 appearParticlePosition = m_realTargetPosition + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentBody.BoxSize.X;

			m_subsystemParticles.AddParticleSystem(new TeleportParticleSystem(m_subsystemTerrain, appearParticlePosition, size), false);
			m_subsystemAudio.PlaySound("Audio/teleport 2", 1f, 0f, appearParticlePosition, 4f, true);

			// RE-ENGANCHAR EL CHASE
			if (m_teleportTarget != null && m_teleportTarget.ComponentHealth != null && m_teleportTarget.ComponentHealth.Health > 0f)
			{
				m_componentNewChaseBehavior.Attack(m_teleportTarget, m_teleportationRange * 2f, 10f, false);
			}

			m_currentState = TeleportState.Idle;
			m_teleportCooldownTimer = m_timeToTeleportAgain; // Aquí se aplica estrictamente tu valor del XML
			m_teleportTarget = null;
		}

		public enum TeleportState
		{
			Idle,
			Disappearing,
			Appearing
		}
	}
}
