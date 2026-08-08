using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		// NOT loaded from XML - hardcoded values
		public Vector2 RangedDistanceRange = new Vector2(5f, 100f);
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.01f;

		// Loaded from XML
		public bool CanUseInventory;

		// Private fields
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChaseBehavior;

		private double m_lastShotTime;
		private double m_aimStartTime;
		private bool m_isAiming;
		private int m_originalActiveSlot = -1;

		private Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentCreature?.ComponentBody == null ||
				m_componentCreature?.ComponentCreatureModel == null)
			{
				StopRangedCombat();
				return;
			}

			ComponentCreature target = m_componentChaseBehavior?.Target;
			bool hasValidTarget = target?.ComponentBody != null &&
								   target.ComponentHealth?.Health > 0f;

			if (!hasValidTarget)
			{
				StopRangedCombat();
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
			{
				StopRangedCombat();
				return;
			}

			int musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			int musketSlot = FindBlockSlot(musketBlockIndex);
			int meleeSlot = FindMeleeWeaponSlot();
			bool hasMeleeWeapon = meleeSlot >= 0;

			bool shouldUseRanged = musketSlot >= 0 &&
								   distance <= RangedDistanceRange.Y &&
								   (distance > RangedDistanceRange.X || !hasMeleeWeapon);

			if (!shouldUseRanged)
			{
				if (distance <= RangedDistanceRange.X && hasMeleeWeapon)
				{
					SwitchToSlot(meleeSlot);
					StopRangedCombat(false);
				}
				else
				{
					StopRangedCombat(true);
				}
				return;
			}

			if (inventory.ActiveSlotIndex != musketSlot)
			{
				SwitchToSlot(musketSlot);
			}

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 aimDir = Vector3.Normalize(targetCenter - eyePos);
			Ray3 aimRay = new Ray3(eyePos, aimDir);

			double gameTime = m_subsystemTime.GameTime;

			if (!m_isAiming)
			{
				if ((gameTime - m_lastShotTime) < MusketCooldown)
				{
					return;
				}

				m_isAiming = true;
				m_aimStartTime = gameTime;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
				return;
			}

			float aimDuration = (float)(gameTime - m_aimStartTime);
			m_componentMiner.Aim(aimRay, AimState.InProgress);

			if (aimDuration >= MusketAimTime)
			{
				FireWeapon(musketBlockIndex, aimRay);
				m_lastShotTime = gameTime;
				m_isAiming = false;
			}
		}

		private void FireWeapon(int musketBlockIndex, Ray3 aimRay)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			int slot = inventory.ActiveSlotIndex;

			bool isTripleShot = m_random.Bool(0.05f);

			if (isTripleShot)
			{
				FireSpecificBullet(musketBlockIndex, slot, BulletBlock.BulletType.MusketBall, aimRay);
				FireSpecificBullet(musketBlockIndex, slot, BulletBlock.BulletType.Buckshot, aimRay);
				FireSpecificBullet(musketBlockIndex, slot, BulletBlock.BulletType.BuckshotBall, aimRay);
			}
			else
			{
				int roll = m_random.Int(0, 2);
				BulletBlock.BulletType type = (BulletBlock.BulletType)roll;
				FireSpecificBullet(musketBlockIndex, slot, type, aimRay);
			}
		}

		private void FireSpecificBullet(int musketBlockIndex, int slot, BulletBlock.BulletType bulletType, Ray3 aimRay)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return;

			int data = MusketBlock.SetLoadState(0, MusketBlock.LoadState.Loaded);
			data = MusketBlock.SetBulletType(data, bulletType);
			data = MusketBlock.SetHammerState(data, true);

			int newValue = Terrain.MakeBlockValue(musketBlockIndex, 0, data);

			inventory.RemoveSlotItems(slot, 1);
			inventory.AddSlotItems(slot, newValue, 1);

			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private int FindBlockSlot(int blockIndex)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == blockIndex)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindMeleeWeaponSlot()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);

				if (contents == 0) continue;

				Block block = BlocksManager.Blocks[contents];
				if (block.GetMeleePower(value) > 1f)
				{
					return i;
				}
			}
			return -1;
		}

		private void SwitchToSlot(int slot)
		{
			if (m_componentMiner.Inventory == null) return;

			if (m_originalActiveSlot < 0)
			{
				m_originalActiveSlot = m_componentMiner.Inventory.ActiveSlotIndex;
			}
			m_componentMiner.Inventory.ActiveSlotIndex = slot;
		}

		private void StopRangedCombat(bool restoreSlot = true)
		{
			if (m_isAiming)
			{
				if (m_componentCreature?.ComponentCreatureModel != null)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 forward = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					Ray3 aimRay = new Ray3(eyePos, forward);

					m_componentMiner.Aim(aimRay, AimState.Cancelled);
				}
				m_isAiming = false;
			}

			if (restoreSlot && m_originalActiveSlot >= 0 && m_componentMiner.Inventory != null)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = m_originalActiveSlot;
				m_originalActiveSlot = -1;
			}
		}
	}
}
