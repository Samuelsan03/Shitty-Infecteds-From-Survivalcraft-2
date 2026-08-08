using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAutoShooter : Component, IUpdateable
	{
		public enum ShooterState
		{
			Inactive,
			Shooting
		}

		private Dictionary<string, int> m_blocksToShoot = new Dictionary<string, int>();
		private float m_timeToRelaunch = 1.0f;
		private Vector2 m_minimumDistanceToAvoid = new Vector2(5f, 0f);
		private double m_nextFireTime = 0.0;  // Tiempo absoluto para el próximo disparo
		private Random m_random = new Random();

		private ShooterState m_state = ShooterState.Inactive;

		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private ComponentBody m_componentBody;
		private ComponentCreature m_componentCreature;
		private ComponentHealth m_componentHealth;

		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public ShooterState State => m_state;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(false);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(false);

			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>(false);
			m_componentZombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentNewChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>(false);

			string blocksString = valuesDictionary.GetValue<string>("BlocksToShoot", "");
			if (!string.IsNullOrEmpty(blocksString))
			{
				string[] blockNames = blocksString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string blockName in blockNames)
				{
					string trimmedName = blockName.Trim();
					if (!string.IsNullOrEmpty(trimmedName))
					{
						m_blocksToShoot[trimmedName] = 1;
					}
				}
			}

			m_timeToRelaunch = valuesDictionary.GetValue<float>("TimeToRelaunch");
			if (m_timeToRelaunch < 0f) m_timeToRelaunch = 0f;

			// Inicializamos el próximo disparo para que pueda ocurrir inmediatamente
			m_nextFireTime = 0.0;
		}

		public void Update(float dt)
		{
			// Salud del dueño
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				m_state = ShooterState.Inactive;
				return;
			}

			ComponentBody target = GetChaseTarget();
			if (target == null || m_blocksToShoot.Count == 0)
			{
				m_state = ShooterState.Inactive;
				return;
			}

			// Distancia mínima (no disparar si está muy cerca)
			float distance = Vector3.Distance(m_componentBody.Position, target.Position);
			if (distance <= m_minimumDistanceToAvoid.X)
			{
				m_state = ShooterState.Inactive;
				return;
			}

			// Cooldown: solo disparar si ha pasado suficiente tiempo
			if (m_subsystemTime.GameTime < m_nextFireTime)
			{
				m_state = ShooterState.Inactive;
				return;
			}

			// Si llegamos aquí, podemos disparar
			m_state = ShooterState.Shooting;
			ShootAtTarget(target);

			// Programar el próximo disparo
			m_nextFireTime = m_subsystemTime.GameTime + m_timeToRelaunch;
		}

		private ComponentBody GetChaseTarget()
		{
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive && m_componentNewChaseBehavior.Target != null)
			{
				ComponentCreature targetCreature = m_componentNewChaseBehavior.Target;
				if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health > 0f)
					return targetCreature.ComponentBody;
			}

			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.IsActive && m_componentZombieChaseBehavior.Target != null)
			{
				ComponentCreature targetCreature = m_componentZombieChaseBehavior.Target;
				if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health > 0f)
					return targetCreature.ComponentBody;
			}

			if (m_componentChaseBehavior != null && m_componentChaseBehavior.IsActive && m_componentChaseBehavior.Target != null)
			{
				ComponentCreature targetCreature = m_componentChaseBehavior.Target;
				if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health > 0f)
					return targetCreature.ComponentBody;
			}

			return null;
		}

		private void ShootAtTarget(ComponentBody target)
		{
			// Animación de lanzamiento (similar al código DayZ)
			if (m_componentCreature != null)
			{
				ComponentHumanModel componentHumanModel = m_componentCreature.ComponentCreatureModel as ComponentHumanModel;
				if (componentHumanModel != null)
				{
					componentHumanModel.m_handAngles2 = new Vector2(4f, -5f);
					componentHumanModel.m_handAngles1 = new Vector2(4f, 3f);
				}
			}

			string blockName = GetRandomBlockName();
			if (string.IsNullOrEmpty(blockName)) return;

			int blockIndex = BlocksManager.GetBlockIndex(blockName);
			if (blockIndex < 0) return;

			int value = blockIndex;

			Vector3 myCenter = m_componentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Vector3 direction = targetCenter - myCenter;
			float dirLength = direction.Length();
			if (dirLength < 0.001f) return;
			direction /= dirLength;

			BoundingBox myBox = m_componentBody.BoundingBox;
			Vector3 halfSize = 0.5f * (myBox.Max - myBox.Min);

			float safeDistance = MathF.Abs(halfSize.X * direction.X) +
								 MathF.Abs(halfSize.Y * direction.Y) +
								 MathF.Abs(halfSize.Z * direction.Z) +
								 0.15f;
			safeDistance = MathUtils.Max(safeDistance, 1.0f);

			Vector3 shootPos = myCenter + safeDistance * direction;

			Vector3 firePosition;
			if (!m_subsystemProjectiles.CanFireProjectile(value, shootPos, direction, m_componentCreature, out firePosition))
				return;

			if (m_componentBody.BoundingBox.Contains(firePosition))
				return;

			float speed = m_random.Float(39f, 41f);
			Vector3 velocity = speed * (direction + m_random.Vector3(0.025f) + new Vector3(0f, 0.05f, 0f));
			Projectile projectile = m_subsystemProjectiles.CreateProjectile(value, firePosition, velocity, Vector3.Zero, null);
			projectile.OwnerEntity = Entity;
			m_subsystemProjectiles.FireProjectileFast(projectile);
			m_subsystemAudio.PlaySound("Audio/Throw", 1f, 0f, new Vector3(shootPos.X, shootPos.Y, shootPos.Z), 4f, true);
		}

		private string GetRandomBlockName()
		{
			if (m_blocksToShoot.Count == 0) return null;

			int index = m_random.Int(0, m_blocksToShoot.Count);
			int i = 0;
			foreach (var kvp in m_blocksToShoot)
			{
				if (i == index) return kvp.Key;
				i++;
			}
			return null;
		}
	}
}
