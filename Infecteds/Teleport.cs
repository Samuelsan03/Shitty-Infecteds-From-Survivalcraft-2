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

		private SubsystemTime m_subsystemTime;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;

		// NUEVO: Referencia para verificar si está montado
		private ComponentRider m_componentRider;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = m_componentCreature.ComponentBody;
			m_componentNewChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>(true);
			m_componentRider = Entity.FindComponent<ComponentRider>(false); // NUEVO

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

		private void HandleIdleState()
		{
			// No hacer nada si estamos muertos
			if (m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			// No hacer nada si estamos en cooldown
			if (m_teleportCooldownTimer > 0f)
				return;

			// NUEVO: Si está montado, cancelar el teletransporte y esperar a que se desmonte
			if (m_componentRider != null && m_componentRider.Mount != null)
				return;

			// OBTENER EL TARGET ACTUAL DEL CHASE BEHAVIOR
			ComponentCreature chaseTarget = m_componentNewChaseBehavior.Target;

			// Si NO hay target (está inactivo), NO teletransportarse
			if (chaseTarget == null)
				return;

			// Verificar que el target siga vivo
			if (chaseTarget.ComponentHealth == null || chaseTarget.ComponentHealth.Health <= 0f)
				return;

			// Calcular distancia al target que estamos persiguiendo
			float distanceToTarget = Vector3.Distance(m_componentBody.Position, chaseTarget.ComponentBody.Position);

			// Si el target está DENTRO del rango, NO teletransportarse
			if (distanceToTarget <= m_teleportationRange)
				return;

			// Si llegamos aquí: está persiguiendo + el target está lejos
			// Solo falta verificar la probabilidad
			if (m_random.Float(0f, 1f) < m_probabilityOfTeleporting)
			{
				m_teleportTarget = chaseTarget;
				StartDisappearing(chaseTarget.ComponentBody.Position);
			}
		}

		private void StartDisappearing(Vector3 targetPosition)
		{
			m_currentState = TeleportState.Disappearing;

			Vector3 particlePosition = m_componentBody.Position + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentBody.BoxSize.X;

			m_subsystemParticles.AddParticleSystem(new TeleportParticleSystem(m_subsystemTerrain, particlePosition, size), false);
			m_subsystemAudio.PlaySound("Audio/teleport 1", 1f, 0f, particlePosition, 4f, true);

			// Calcular posición de aparición cerca del target
			Vector2 randomDirection = m_random.Vector2();
			float randomDistance = m_random.Float(2f, 4f);

			m_realTargetPosition = new Vector3(
				targetPosition.X + randomDirection.X * randomDistance,
				targetPosition.Y,
				targetPosition.Z + randomDirection.Y * randomDistance
			);

			// Esconder en el cielo mientras espera
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
			// NUEVO: Prevención de bugs. Si por alguna razón se monta mientras desaparece, cancelar y reaparecer inmediatamente
			if (m_componentRider != null && m_componentRider.Mount != null)
			{
				m_componentBody.Position = m_realTargetPosition;
				m_componentBody.Velocity = Vector3.Zero;
				m_componentBody.IsGravityEnabled = true;
				m_componentBody.TerrainCollidable = true;
				m_componentBody.BodyCollidable = true;
				m_currentState = TeleportState.Idle;
				return;
			}

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

			// Re-enganchar el chase
			if (m_teleportTarget != null && m_teleportTarget.ComponentHealth != null && m_teleportTarget.ComponentHealth.Health > 0f)
			{
				m_componentNewChaseBehavior.Attack(m_teleportTarget, m_teleportationRange * 2f, 10f, false);
			}

			m_currentState = TeleportState.Idle;
			m_teleportCooldownTimer = m_timeToTeleportAgain;
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
