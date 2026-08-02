using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAutoShooter : Component, IUpdateable
	{
		private Dictionary<string, int> m_blocksToShoot = new Dictionary<string, int>();
		private float m_timeToRelaunch = 1.0f;
		private Vector2 m_minimumDistanceToAvoid = new Vector2(5f, 0f);
		private float m_lastFireTime;
		private Random m_random = new Random();

		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private ComponentBody m_componentBody;
		private ComponentCreature m_componentCreature;

		// Componentes de chase para obtener el target correcto
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(false);

			// Buscar componentes de chase (puede tener uno o ninguno)
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
		}

		public void Update(float dt)
		{
			if (m_subsystemTime.GameTime - m_lastFireTime < m_timeToRelaunch)
				return;

			// CORREGIDO: Solo obtener target si hay un chase activo
			// Eliminado el fallback a FindNearestTarget()
			ComponentBody target = GetChaseTarget();

			if (target == null)
				return;

			float distance = Vector3.Distance(m_componentBody.Position, target.Position);
			if (distance <= m_minimumDistanceToAvoid.X)
				return;

			if (m_blocksToShoot.Count > 0)
			{
				ShootAtTarget(target);
				m_lastFireTime = (float)m_subsystemTime.GameTime;
			}
		}

		/// <summary>
		/// Obtiene el target SOLO si hay un componente de chase activo persiguiendo a alguien
		/// </summary>
		private ComponentBody GetChaseTarget()
		{
			// Prioridad 1: ComponentNewChaseBehavior (el más personalizado)
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive && m_componentNewChaseBehavior.Target != null)
			{
				ComponentCreature targetCreature = m_componentNewChaseBehavior.Target;
				if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health > 0f)
					return targetCreature.ComponentBody;
			}

			// Prioridad 2: ComponentZombieChaseBehavior
			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.IsActive && m_componentZombieChaseBehavior.Target != null)
			{
				ComponentCreature targetCreature = m_componentZombieChaseBehavior.Target;
				if (targetCreature.ComponentHealth != null && targetCreature.ComponentHealth.Health > 0f)
					return targetCreature.ComponentBody;
			}

			// Prioridad 3: ComponentChaseBehavior (original del juego)
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

			// Calcular distancia segura basada en el bounding box
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
