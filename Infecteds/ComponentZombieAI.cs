using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieAI : Component, IUpdateable
	{
		private SubsystemTime m_subsystemTime;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentZombieChaseBehavior m_componentChaseBehavior;

		public bool CanUseInventory;

		public Vector2 DistanceRange = new Vector2(5f, 100f);

		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		public float CooldownTimer;
		public float AimTimeTimer;

		private Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
		}

		public virtual void Update(float dt)
		{
			if (!CanUseInventory)
				return;

			if (m_componentCreature?.ComponentHealth?.Health <= 0f)
				return;

			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
				return;

			if (m_componentChaseBehavior?.Target == null)
			{
				CancelAiming();
				return;
			}

			ComponentCreature target = m_componentChaseBehavior.Target;
			float distance = Vector3.Distance(m_componentBody.Position, target.ComponentBody.Position);

			if (distance < DistanceRange.X)
			{
				HandleCloseRange(inventory);
			}
			else if (distance <= DistanceRange.Y)
			{
				HandleRangedAttack(inventory, target);
			}
			else
			{
				CancelAiming();
			}
		}

		private void HandleCloseRange(IInventory inventory)
		{
			int activeSlot = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (IsRangedWeapon(contents))
			{
				int meleeSlot = FindMeleeWeaponSlot(inventory);
				if (meleeSlot >= 0)
				{
					SwapSlots(inventory, activeSlot, meleeSlot);
					CancelAiming();
					return;
				}
			}

			if (IsRangedWeapon(contents))
			{
				EnsureMusketLoaded(inventory);
				AimAndFire(m_componentChaseBehavior.Target);
			}
			else
			{
				CancelAiming();
			}
		}

		private void HandleRangedAttack(IInventory inventory, ComponentCreature target)
		{
			int activeSlot = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (!IsRangedWeapon(contents))
			{
				int rangedSlot = FindRangedWeaponSlot(inventory);
				if (rangedSlot >= 0 && rangedSlot != activeSlot)
				{
					SwapSlots(inventory, activeSlot, rangedSlot);
					CancelAiming();
					return;
				}
				CancelAiming();
				return;
			}

			EnsureMusketLoaded(inventory);
			AimAndFire(target);
		}

		private bool IsRangedWeapon(int blockIndex)
		{
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			return blockIndex == musketIndex;
		}

		private int FindRangedWeaponSlot(IInventory inventory)
		{
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) <= 0)
					continue;

				int value = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(value) == musketIndex)
					return i;
			}
			return -1;
		}

		private int FindMeleeWeaponSlot(IInventory inventory)
		{
			int bestSlot = -1;
			float bestPower = 0f;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) <= 0)
					continue;

				int value = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);

				if (IsRangedWeapon(contents))
					continue;

				Block block = BlocksManager.Blocks[contents];
				float power = block.GetMeleePower(value);

				if (power > bestPower)
				{
					bestPower = power;
					bestSlot = i;
				}
			}
			return bestSlot;
		}

		private void EnsureMusketLoaded(IInventory inventory)
		{
			int slot = inventory.ActiveSlotIndex;
			int value = inventory.GetSlotValue(slot);

			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();

			if (Terrain.ExtractContents(value) != musketIndex)
				return;

			int data = Terrain.ExtractData(value);

			if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
			{
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);

				// Variación aleatoria de perdigones
				BulletBlock.BulletType[] bulletTypes = new BulletBlock.BulletType[]
				{
					BulletBlock.BulletType.MusketBall,
					BulletBlock.BulletType.Buckshot,
					BulletBlock.BulletType.BuckshotBall
				};
				BulletBlock.BulletType randomBullet = bulletTypes[m_random.Int(0, 2)];
				data = MusketBlock.SetBulletType(data, randomBullet);

				int newValue = Terrain.MakeBlockValue(musketIndex, 0, data);

				inventory.RemoveSlotItems(slot, 1);
				inventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void AimAndFire(ComponentCreature target)
		{
			CooldownTimer -= m_subsystemTime.GameTimeDelta;

			if (CooldownTimer > 0f)
			{
				CancelAiming();
				return;
			}

			Vector3 eyePosition = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 direction = targetCenter - eyePosition;

			Ray3 aim = new Ray3(eyePosition, direction);

			if (AimTimeTimer > 0f)
			{
				m_componentMiner.Aim(aim, AimState.InProgress);
				AimTimeTimer -= m_subsystemTime.GameTimeDelta;
			}
			else
			{
				// 5% de probabilidad para el triple disparo
				if (m_random.Bool(0.05f))
				{
					TripleShot(aim);
				}
				else
				{
					m_componentMiner.Aim(aim, AimState.Completed);
					CooldownTimer = MusketCooldown;
					AimTimeTimer = MusketAimTime;
				}
			}
		}

		private void TripleShot(Ray3 aim)
		{
			for (int i = 0; i < 3; i++)
			{
				m_componentMiner.Aim(aim, AimState.Completed);
			}
			CooldownTimer = MusketCooldown;
			AimTimeTimer = MusketAimTime;
		}

		private void CancelAiming()
		{
			AimTimeTimer = MusketAimTime;
			CooldownTimer = 0f;
			Ray3 emptyAim = new Ray3(Vector3.Zero, Vector3.UnitZ);
			m_componentMiner.Aim(emptyAim, AimState.Cancelled);
		}

		private void SwapSlots(IInventory inventory, int slotA, int slotB)
		{
			if (slotA == slotB)
				return;

			int valueA = inventory.GetSlotValue(slotA);
			int countA = inventory.GetSlotCount(slotA);
			int valueB = inventory.GetSlotValue(slotB);
			int countB = inventory.GetSlotCount(slotB);

			inventory.RemoveSlotItems(slotA, countA);
			inventory.RemoveSlotItems(slotB, countB);
			inventory.AddSlotItems(slotA, valueB, countB);
			inventory.AddSlotItems(slotB, valueA, countA);
		}
	}
}
