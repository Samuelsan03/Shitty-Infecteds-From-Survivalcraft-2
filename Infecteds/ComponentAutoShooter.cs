using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAutoShooter : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		public override bool IsActive { get; set; }

		// Campo de rango (X = Mínimo, Y = Máximo). No va al Load(), ya está inicializado aquí.
		public Vector2 ShootRangeDistance = new Vector2(5f, 100f);

		private string m_blocksToShootString;
		private float m_timeToRelaunch;
		private int[] m_blockValuesToShoot;

		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;

		private ComponentZombieChaseBehavior m_zombieChaseBehavior;
		private ComponentNewChaseBehavior m_newChaseBehavior;

		private StateMachine m_stateMachine = new StateMachine();
		private float m_importanceLevel;
		private float m_shootCooldown;
		private int m_currentBlockIndex;
		private Random m_random = new Random();

		private bool IsShooterAlive()
		{
			return m_componentCreature?.ComponentHealth != null && m_componentCreature.ComponentHealth.Health > 0f;
		}

		private bool IsTargetAlive(ComponentCreature target)
		{
			return target != null && target.ComponentHealth != null && target.ComponentHealth.Health > 0f;
		}

		private bool HasActiveTarget()
		{
			ComponentCreature target = null;
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.IsActive)
				target = m_zombieChaseBehavior.Target;
			else if (m_newChaseBehavior != null && m_newChaseBehavior.IsActive)
				target = m_newChaseBehavior.Target;

			return IsTargetAlive(target);
		}

		private ComponentCreature GetActiveTarget()
		{
			if (m_zombieChaseBehavior != null && m_zombieChaseBehavior.IsActive && IsTargetAlive(m_zombieChaseBehavior.Target))
				return m_zombieChaseBehavior.Target;

			if (m_newChaseBehavior != null && m_newChaseBehavior.IsActive && IsTargetAlive(m_newChaseBehavior.Target))
				return m_newChaseBehavior.Target;

			return null;
		}

		private bool IsTargetInValidRange()
		{
			ComponentCreature target = GetActiveTarget();
			if (target == null || m_componentCreature?.ComponentBody == null || target.ComponentBody == null)
				return false;

			float distance = Vector2.Distance(
				new Vector2(m_componentCreature.ComponentBody.Position.X, m_componentCreature.ComponentBody.Position.Z),
				new Vector2(target.ComponentBody.Position.X, target.ComponentBody.Position.Z)
			);

			return distance >= ShootRangeDistance.X && distance <= ShootRangeDistance.Y;
		}

		private void ShootProjectile()
		{
			if (!IsShooterAlive())
				return;

			ComponentCreature target = GetActiveTarget();
			if (target == null || m_componentCreature?.ComponentBody == null)
				return;

			if (m_blockValuesToShoot == null || m_blockValuesToShoot.Length == 0)
				return;

			int blockValue = m_blockValuesToShoot[m_currentBlockIndex % m_blockValuesToShoot.Length];
			m_currentBlockIndex++;

			Vector3 shootPosition = m_componentCreature.ComponentBody.Position + new Vector3(0f, m_componentCreature.ComponentBody.BoxSize.Y * 0.8f, 0f);
			Vector3 targetPosition = target.ComponentBody.Position + new Vector3(0f, target.ComponentBody.BoxSize.Y * 0.5f, 0f);
			Vector3 direction = Vector3.Normalize(targetPosition - shootPosition);

			Vector3 firePosition;
			bool canFire = m_subsystemProjectiles.CanFireProjectile(blockValue, shootPosition, direction, m_componentCreature, out firePosition);

			if (canFire)
			{
				float speed = m_random.Float(39f, 41f);
				Vector3 velocity = speed * (direction + m_random.Vector3(0.025f) + new Vector3(0f, 0.05f, 0f));

				Projectile projectile = m_subsystemProjectiles.CreateProjectile(blockValue, firePosition, velocity, Vector3.Zero, m_componentCreature);
				if (projectile != null)
				{
					projectile.OwnerEntity = Entity;
					m_subsystemProjectiles.FireProjectileFast(projectile);
					m_subsystemAudio.PlaySound("Audio/Throw", 1f, 0f, shootPosition, 4f, true);
				}
			}
		}

		private int[] ParseBlockNames(string blockNamesString)
		{
			if (string.IsNullOrEmpty(blockNamesString))
				return null;

			string[] names = blockNamesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			List<int> values = new List<int>();

			foreach (string name in names)
			{
				string trimmedName = name.Trim();
				if (!string.IsNullOrEmpty(trimmedName))
				{
					int blockIndex = BlocksManager.GetBlockIndex(trimmedName, false);
					if (blockIndex >= 0 && blockIndex < 1024)
						values.Add(blockIndex);
					else
						Log.Warning($"ComponentAutoShooter: Block '{trimmedName}' not found!");
				}
			}

			return values.Count > 0 ? values.ToArray() : null;
		}

		public void Update(float dt)
		{
			if (m_blockValuesToShoot == null || m_blockValuesToShoot.Length == 0 || !IsShooterAlive())
				return;

			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);

			m_zombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();
			m_newChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();

			m_blocksToShootString = valuesDictionary.GetValue<string>("BlocksToShoot");
			m_timeToRelaunch = valuesDictionary.GetValue<float>("TimeToRelaunch");

			m_blockValuesToShoot = ParseBlockNames(m_blocksToShootString);

			m_stateMachine.AddState("Idle", null, delegate
			{
				m_shootCooldown -= m_subsystemTime.GameTimeDelta;

				if (IsShooterAlive() && HasActiveTarget() && m_shootCooldown <= 0f && IsTargetInValidRange())
				{
					m_stateMachine.TransitionTo("Shooting");
				}
			}, null);

			m_stateMachine.AddState("Shooting", delegate
			{
				// Animación idéntica a la lógica del DayZ (生物远程攻击行为)
				// Asigna el Vector2 completo para dar el efecto de lanzamiento
				if (m_componentCreatureModel is ComponentHumanModel humanModel)
				{
					humanModel.m_handAngles1 = new Vector2(4f, 3f);
					humanModel.m_handAngles2 = new Vector2(4f, -5f);
				}

				if (IsShooterAlive() && IsTargetInValidRange() && HasActiveTarget())
				{
					ShootProjectile();
					m_shootCooldown = m_timeToRelaunch;
				}
			}, delegate
			{
				m_shootCooldown -= m_subsystemTime.GameTimeDelta;

				if (!IsShooterAlive() || !HasActiveTarget() || !IsTargetInValidRange())
				{
					m_stateMachine.TransitionTo("Idle");
				}
				else
				{
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			m_stateMachine.TransitionTo("Idle");
		}
	}
}
