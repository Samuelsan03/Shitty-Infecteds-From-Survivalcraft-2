using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieAI : Component, IUpdateable
	{
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentZombieChaseBehavior m_componentChaseBehavior;

		public bool CanUseInventory;

		public Vector2 DistanceRange = new Vector2(5f, 100f);
		public Vector2 DistanceRangeOfThrowable = new Vector2(5f, 15f);

		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		public float CrossbowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;

		public float BowCooldown = 0.01f;
		public float BowAimTime = 1.5f;

		public float ThrowableCooldown = 0.01f;
		public float ThrowableAimTime = 1.5f;

		public float CooldownTimer;
		public float AimTimeTimer;

		private Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(false);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(false);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);

			if (m_subsystemProjectiles != null)
			{
				m_subsystemProjectiles.ProjectileAdded += OnProjectileAdded;
			}
		}

		private void OnProjectileAdded(Projectile projectile)
		{
			if (m_componentCreature == null || m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (projectile == null || projectile.OwnerEntity == null)
				return;

			if (projectile.OwnerEntity != m_componentCreature.Entity)
				return;

			int contents = Terrain.ExtractContents(projectile.Value);
			int arrowIndex = BlocksManager.GetBlockIndex<ArrowBlock>();

			if (contents == arrowIndex)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
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

			bool hasThrowable = FindThrowableSlot(inventory) >= 0;
			bool hasRanged = FindRangedWeaponSlot(inventory) >= 0;
			bool hasMelee = FindMeleeWeaponSlot(inventory) >= 0;

			if (hasThrowable)
			{
				if (distance < DistanceRangeOfThrowable.X)
				{
					if (hasMelee)
					{
						HandleCloseRange(inventory);
					}
					else
					{
						HandleThrowableAttack(inventory, target, distance);
					}
				}
				else if (distance <= DistanceRangeOfThrowable.Y)
				{
					HandleThrowableAttack(inventory, target, distance);
				}
				else
				{
					if (hasRanged)
					{
						HandleRangedAttack(inventory, target);
					}
					else
					{
						CancelAiming();
					}
				}
			}
			else
			{
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
		}

		private void HandleThrowableAttack(IInventory inventory, ComponentCreature target, float distance)
		{
			Vector3 dirToTarget = Vector3.Normalize(target.ComponentBody.Position - m_componentBody.Position);
			float dot = Vector3.Dot(m_componentBody.Matrix.Forward, dirToTarget);

			if (dot < 0.3f)
			{
				CancelAiming();
				return;
			}

			if (!HasLineOfSight(target))
			{
				CancelAiming();
				return;
			}

			ComponentPathfinding pathfinding = Entity.FindComponent<ComponentPathfinding>(false);

			if (pathfinding != null && pathfinding.IsStuck)
			{
				CancelAiming();
				if (pathfinding.Destination == null)
				{
					Vector3 randomDir = new Vector3(m_random.Float(-1f, 1f), 0f, m_random.Float(-1f, 1f));
					if (randomDir.LengthSquared() > 0.01f)
					{
						randomDir = Vector3.Normalize(randomDir);
						pathfinding.SetDestination(m_componentBody.Position + randomDir * 3f, 1f, 1f, 0, true, false, false, null);
					}
				}
				return;
			}

			if (pathfinding != null && pathfinding.Destination != null)
			{
				pathfinding.Stop();
			}

			int activeSlot = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (!IsThrowableBlock(contents))
			{
				int throwableSlot = FindThrowableSlot(inventory);
				if (throwableSlot >= 0 && throwableSlot != activeSlot)
				{
					SwapSlots(inventory, activeSlot, throwableSlot);
					CancelAiming();
					return;
				}
				CancelAiming();
				return;
			}

			AimAndFireThrowable(target);
		}

		private bool HasLineOfSight(ComponentCreature target)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			float distanceToTarget = Vector3.Distance(eyePos, targetCenter);

			if (m_subsystemTerrain != null)
			{
				TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(eyePos, targetCenter, true, true, null);
				if (terrainHit.HasValue && terrainHit.Value.Distance < distanceToTarget - 0.5f)
				{
					return false;
				}
			}

			if (m_subsystemBodies != null)
			{
				BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(eyePos, targetCenter, 0f, (ComponentBody body, float dist) =>
					body.Entity != m_componentCreature.Entity &&
					body.Entity != target.Entity);
				if (bodyHit.HasValue && bodyHit.Value.Distance < distanceToTarget - 0.5f)
				{
					return false;
				}
			}

			return true;
		}

		private bool IsThrowableBlock(int blockIndex)
		{
			if (IsRangedWeapon(blockIndex)) return false;
			if (blockIndex <= 0 || blockIndex >= BlocksManager.Blocks.Length) return false;
			Block block = BlocksManager.Blocks[blockIndex];
			return block.IsAimable && block.GetProjectileSpeed(0) > 0f;
		}

		private int FindThrowableSlot(IInventory inventory)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) <= 0) continue;
				int value = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				if (IsThrowableBlock(contents)) return i;
			}
			return -1;
		}

		private void AimAndFireThrowable(ComponentCreature target)
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
				m_componentMiner.Aim(aim, AimState.Completed);
				CooldownTimer = ThrowableCooldown;
				AimTimeTimer = ThrowableAimTime;
			}
		}

		private void HandleCloseRange(IInventory inventory)
		{
			int activeSlot = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlot);
			int contents = Terrain.ExtractContents(slotValue);

			if (IsRangedWeapon(contents) || IsThrowableBlock(contents))
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
				EnsureRangedWeaponLoaded(inventory);
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

			EnsureRangedWeaponLoaded(inventory);
			AimAndFire(target);
		}

		private bool IsRangedWeapon(int blockIndex)
		{
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();
			return blockIndex == musketIndex || blockIndex == crossbowIndex || blockIndex == bowIndex;
		}

		private int FindRangedWeaponSlot(IInventory inventory)
		{
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) <= 0)
					continue;

				int value = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);
				if (contents == musketIndex || contents == crossbowIndex || contents == bowIndex)
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

				if (IsRangedWeapon(contents) || IsThrowableBlock(contents))
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

		private void EnsureRangedWeaponLoaded(IInventory inventory)
		{
			int slot = inventory.ActiveSlotIndex;
			int value = inventory.GetSlotValue(slot);
			int contents = Terrain.ExtractContents(value);

			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();

			if (contents == musketIndex)
			{
				EnsureMusketLoaded(inventory, slot, value);
			}
			else if (contents == crossbowIndex)
			{
				EnsureCrossbowLoaded(inventory, slot, value);
			}
			else if (contents == bowIndex)
			{
				EnsureBowLoaded(inventory, slot, value);
			}
		}

		private void EnsureMusketLoaded(IInventory inventory, int slot, int value)
		{
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int data = Terrain.ExtractData(value);

			if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
			{
				data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);

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

		private void EnsureCrossbowLoaded(IInventory inventory, int slot, int value)
		{
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int data = Terrain.ExtractData(value);
			int draw = CrossbowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);

			bool needsReload = false;

			if (draw != 15)
			{
				data = CrossbowBlock.SetDraw(data, 15);
				needsReload = true;
			}

			if (arrowType == null)
			{
				ArrowBlock.ArrowType[] supportedBolts = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.IronBolt,
					ArrowBlock.ArrowType.DiamondBolt,
					ArrowBlock.ArrowType.ExplosiveBolt
				};
				ArrowBlock.ArrowType randomBolt = supportedBolts[m_random.Int(0, 2)];
				data = CrossbowBlock.SetArrowType(data, randomBolt);
				needsReload = true;
			}

			if (needsReload)
			{
				int newValue = Terrain.MakeBlockValue(crossbowIndex, 0, data);
				inventory.RemoveSlotItems(slot, 1);
				inventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void EnsureBowLoaded(IInventory inventory, int slot, int value)
		{
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();
			int data = Terrain.ExtractData(value);
			int draw = BowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);

			bool needsReload = false;

			if (draw != 15)
			{
				data = BowBlock.SetDraw(data, 15);
				needsReload = true;
			}

			if (arrowType == null)
			{
				ArrowBlock.ArrowType[] supportedArrows = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.CopperArrow,
					ArrowBlock.ArrowType.IronArrow,
					ArrowBlock.ArrowType.DiamondArrow,
					ArrowBlock.ArrowType.FireArrow
				};
				ArrowBlock.ArrowType randomArrow = supportedArrows[m_random.Int(0, 5)];
				data = BowBlock.SetArrowType(data, randomArrow);
				needsReload = true;
			}

			if (needsReload)
			{
				int newValue = Terrain.MakeBlockValue(bowIndex, 0, data);
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
				int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
				int slotValue = m_componentMiner.Inventory.GetSlotValue(activeSlot);
				int contents = Terrain.ExtractContents(slotValue);
				int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();

				if (contents == musketIndex && m_random.Bool(0.05f))
				{
					TripleShot(aim);
				}
				else
				{
					m_componentMiner.Aim(aim, AimState.Completed);
				}

				if (contents == musketIndex)
				{
					CooldownTimer = MusketCooldown;
					AimTimeTimer = MusketAimTime;
				}
				else if (contents == BlocksManager.GetBlockIndex<CrossbowBlock>())
				{
					CooldownTimer = CrossbowCooldown;
					AimTimeTimer = CrossbowAimTime;
				}
				else
				{
					CooldownTimer = BowCooldown;
					AimTimeTimer = BowAimTime;
				}
			}
		}

		private void TripleShot(Ray3 aim)
		{
			for (int i = 0; i < 3; i++)
			{
				m_componentMiner.Aim(aim, AimState.Completed);
			}
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
