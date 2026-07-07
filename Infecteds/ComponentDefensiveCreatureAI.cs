using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool CanUseInventory
		{
			get
			{
				return m_canUseInventory;
			}
			set
			{
				m_canUseInventory = value;
			}
		}

		public Vector2 AttackDistanceRange = new Vector2(5f, 100f);
		public float MusketCooldown = 0.5f;
		public float MusketAimTime = 1.5f;

		private bool m_canUseInventory;
		private float m_aimTimer;
		private float m_cooldownTimer;
		private bool m_isAiming;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private SubsystemTime m_subsystemTime;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
		}

		public void Update(float dt)
		{
			if (!m_canUseInventory || m_componentMiner.Inventory == null) return;

			ComponentNewChaseBehavior chaseBehavior = m_componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior == null || !chaseBehavior.IsActive || chaseBehavior.Target == null) return;

			ComponentCreature target = chaseBehavior.Target;
			if (target.ComponentHealth.Health <= 0f) return;

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			if (distance <= AttackDistanceRange.Y)
			{
				if (distance < AttackDistanceRange.X)
				{
					int meleeSlot = FindMeleeWeaponSlot();
					if (meleeSlot >= 0)
					{
						CancelAim();
						m_componentMiner.Inventory.ActiveSlotIndex = meleeSlot;
					}
					else
					{
						HandleRangedAttack(target);
					}
				}
				else
				{
					HandleRangedAttack(target);
				}
			}
			else
			{
				CancelAim();
			}
		}

		private void HandleRangedAttack(ComponentCreature target)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			int musketSlot = FindMusketSlot();
			if (musketSlot < 0) return;

			m_componentMiner.Inventory.ActiveSlotIndex = musketSlot;
			EnsureMusketLoaded(musketSlot);

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, direction);

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimTimer = 0f;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
			}
			else
			{
				m_aimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (m_aimTimer >= MusketAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = MusketCooldown;
					m_aimTimer = 0f;
				}
			}
		}

		private void CancelAim()
		{
			if (m_isAiming)
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 direction = m_componentCreature.ComponentBody.Matrix.Forward;
				Ray3 aimRay = new Ray3(eyePos, direction);
				m_componentMiner.Aim(aimRay, AimState.Cancelled);
				m_isAiming = false;
				m_aimTimer = 0f;
			}
		}

		private int FindMeleeWeaponSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0)
				{
					int value = m_componentMiner.Inventory.GetSlotValue(i);
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
					if (block.GetMeleePower(value) > 1f && !block.IsAimable_(value))
					{
						return i;
					}
				}
			}
			return -1;
		}

		private int FindMusketSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == MusketBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private void EnsureMusketLoaded(int slotIndex)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
			{
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
				data = MusketBlock.SetBulletType(data, BulletBlock.BulletType.MusketBall);
				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(MusketBlock.Index, 0, data), 1);
			}
		}
	}
}
