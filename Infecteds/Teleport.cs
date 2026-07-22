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

		/// <summary>
		/// Busca un target válido que esté FUERA del rango de teletransporte.
		/// Retorna null si no hay ningún target fuera del rango.
		/// </summary>
		private ComponentCreature FindTeleportTarget()
		{
			Vector3 myPosition = m_componentBody.Position;
			float searchRadius = m_teleportationRange * 3f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(myPosition.X, myPosition.Z), searchRadius, m_componentBodies);

			ComponentCreature bestTarget = null;
			float bestDistance = float.MaxValue;

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();

				// Filtros básicos
				if (creature == null)
					continue;
				if (creature == m_componentCreature)
					continue;
				if (!creature.Entity.IsAddedToProject)
					continue;
				if (creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f)
					continue;

				float distance = Vector3.Distance(myPosition, creature.ComponentBody.Position);

				// FILTRO PRINCIPAL: Solo nos interesan los que están FUERA del rango
				// Si distancia es 14 y rango es 15 -> NO es candidato (14 no es > 15)
				// Si distancia es 16 y rango es 15 -> ES candidato (16 es > 15)
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
			// No hacer nada si estamos muertos o en cooldown
			if (m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;
			if (m_teleportCooldownTimer > 0f)
				return;

			// Buscar un target que esté FUERA del rango
			// Si todos los targets están dentro del rango, esto retorna null
			ComponentCreature target = FindTeleportTarget();
			if (target == null)
				return;

			// Si llegamos aquí, el target está garantizado a estar fuera del rango
			// Solo falta verificar la probabilidad
			if (m_random.Float(0f, 1f) < m_probabilityOfTeleporting)
			{
				m_teleportTarget = target;
				StartDisappearing(target.ComponentBody.Position);
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
			float randomDistance = m_random.Float(2f, 4f); // Aparecer cerca, no a distancia variable

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
