using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

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
		public Vector2 SafeDistanceForExplosives = new Vector2(20f, 100f);

		// Tiempos del Mosquete Mejorado
		public float ImprovedMusketCooldown = 0.01f;
		public float ImprovedMusketAimTime = 1.5f;

		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		// Tiempos del Lanzallamas
		public float FlameThrowerCooldown = 0.01f;
		public float FlameThrowerAimTime = 1.5f;

		public float CrossbowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;

		// Tiempos de la Ballesta Repetidora
		public float RepeatCrossbowCooldown = 0.01f;
		public float RepeatCrossbowAimTime = 1.5f;

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
			int repeatBoltIndex = BlocksManager.GetBlockIndex<RepeatBoltBlock>();
			int flameBulletIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>();

			// Forzar desaparición al tocar el suelo para flechas, virotes repetidores y balas de lanzallamas
			if (contents == arrowIndex || contents == repeatBoltIndex || contents == flameBulletIndex)
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
						HandleCloseRange(inventory, distance);
					}
					else
					{
						CancelAiming();
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
						HandleRangedAttack(inventory, target, distance);
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
					HandleCloseRange(inventory, distance);
				}
				else if (distance <= DistanceRange.Y)
				{
					HandleRangedAttack(inventory, target, distance);
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

		private void HandleCloseRange(IInventory inventory, float distance)
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
				EnsureRangedWeaponLoaded(inventory, distance);
				AimAndFire(m_componentChaseBehavior.Target);
				return;
			}

			if (IsThrowableBlock(contents))
			{
				int meleeSlot = FindMeleeWeaponSlot(inventory);
				if (meleeSlot >= 0)
				{
					SwapSlots(inventory, activeSlot, meleeSlot);
					CancelAiming();
					return;
				}
				CancelAiming();
				return;
			}

			CancelAiming();
		}

		private void HandleRangedAttack(IInventory inventory, ComponentCreature target, float distance)
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

			EnsureRangedWeaponLoaded(inventory, distance);
			AimAndFire(target);
		}

		private bool IsRangedWeapon(int blockIndex)
		{
			int improvedMusketIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>();
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();
			int repeatCrossbowIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>();
			int flameThrowerIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>();

			return blockIndex == improvedMusketIndex || blockIndex == musketIndex || blockIndex == crossbowIndex || blockIndex == bowIndex || blockIndex == repeatCrossbowIndex || blockIndex == flameThrowerIndex;
		}

		private int FindRangedWeaponSlot(IInventory inventory)
		{
			int improvedMusketIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>();
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();
			int repeatCrossbowIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>();
			int flameThrowerIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>();

			int bestSlot = -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) <= 0) continue;

				int value = inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(value);

				// Mayor prioridad para el mosquete mejorado
				if (contents == improvedMusketIndex) return i;

				if (contents == musketIndex || contents == crossbowIndex || contents == bowIndex || contents == repeatCrossbowIndex || contents == flameThrowerIndex)
				{
					if (bestSlot == -1) bestSlot = i;
				}
			}
			return bestSlot;
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

		private void EnsureRangedWeaponLoaded(IInventory inventory, float distance)
		{
			int slot = inventory.ActiveSlotIndex;
			int value = inventory.GetSlotValue(slot);
			int contents = Terrain.ExtractContents(value);

			int improvedMusketIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>();
			int musketIndex = BlocksManager.GetBlockIndex<MusketBlock>();
			int crossbowIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
			int bowIndex = BlocksManager.GetBlockIndex<BowBlock>();
			int repeatCrossbowIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>();
			int flameThrowerIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>();

			if (contents == improvedMusketIndex)
			{
				EnsureImprovedMusketLoaded(inventory, slot, value);
			}
			else if (contents == musketIndex)
			{
				EnsureMusketLoaded(inventory, slot, value);
			}
			else if (contents == flameThrowerIndex)
			{
				EnsureFlameThrowerLoaded(inventory, slot, value);
			}
			else if (contents == crossbowIndex)
			{
				EnsureCrossbowLoaded(inventory, slot, value, distance);
			}
			else if (contents == bowIndex)
			{
				EnsureBowLoaded(inventory, slot, value);
			}
			else if (contents == repeatCrossbowIndex)
			{
				EnsureRepeatCrossbowLoaded(inventory, slot, value, distance);
			}
		}

		private void EnsureImprovedMusketLoaded(IInventory inventory, int slot, int value)
		{
			int improvedMusketIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>();
			int data = Terrain.ExtractData(value);
			int ammoCount = ImprovedMusketBlock.GetAmmoCount(data);

			if (ammoCount == 0)
			{
				data = ImprovedMusketBlock.SetAmmoCount(data, 2);
				int newValue = Terrain.MakeBlockValue(improvedMusketIndex, 0, data);

				inventory.RemoveSlotItems(slot, 1);
				inventory.AddSlotItems(slot, newValue, 1);
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

		private void EnsureFlameThrowerLoaded(IInventory inventory, int slot, int value)
		{
			int flameThrowerIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>();
			int data = Terrain.ExtractData(value);
			var state = FlameThrowerBlock.GetLoadState(data);
			int ammo = FlameThrowerBlock.GetAmmoCount(data);

			// Si está vacío o sin munición, recarga seleccionando un tipo aleatorio (Fuego o Veneno)
			if (state != FlameThrowerBlock.LoadState.Loaded || ammo == 0)
			{
				int selectedBulletType = m_random.Int(0, 1);
				int newData = data;
				newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
				newData = FlameThrowerBlock.SetAmmoCount(newData, 15);
				newData = (newData & ~0x300) | ((selectedBulletType & 3) << 8);

				int newValue = Terrain.MakeBlockValue(flameThrowerIndex, 0, newData);
				inventory.RemoveSlotItems(slot, 1);
				inventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void EnsureCrossbowLoaded(IInventory inventory, int slot, int value, float distance)
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
				bool canUseExplosive = distance >= SafeDistanceForExplosives.X && distance <= SafeDistanceForExplosives.Y;

				ArrowBlock.ArrowType[] supportedBolts;
				if (canUseExplosive)
				{
					supportedBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt,
						ArrowBlock.ArrowType.ExplosiveBolt
					};
				}
				else
				{
					supportedBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt
					};
				}

				ArrowBlock.ArrowType randomBolt = supportedBolts[m_random.Int(0, supportedBolts.Length - 1)];
				data = CrossbowBlock.SetArrowType(data, randomBolt);
				needsReload = true;
			}
			else if (arrowType == ArrowBlock.ArrowType.ExplosiveBolt)
			{
				bool canUseExplosive = distance >= SafeDistanceForExplosives.X && distance <= SafeDistanceForExplosives.Y;

				if (!canUseExplosive)
				{
					ArrowBlock.ArrowType[] safeBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt
					};
					ArrowBlock.ArrowType replacementBolt = safeBolts[m_random.Int(0, 1)];
					data = CrossbowBlock.SetArrowType(data, replacementBolt);
					needsReload = true;
				}
			}

			if (needsReload)
			{
				int newValue = Terrain.MakeBlockValue(crossbowIndex, 0, data);
				inventory.RemoveSlotItems(slot, 1);
				inventory.AddSlotItems(slot, newValue, 1);
			}
		}

		private void EnsureRepeatCrossbowLoaded(IInventory inventory, int slot, int value, float distance)
		{
			int repeatCrossbowIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>();
			int data = Terrain.ExtractData(value);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatBoltType? boltType = RepeatCrossbowBlock.GetRepeatBoltType(data);
			int count = RepeatCrossbowBlock.GetCount(data);

			bool needsReload = false;

			if (draw != 15)
			{
				data = RepeatCrossbowBlock.SetDraw(data, 15);
				needsReload = true;
			}

			if (boltType == null || count == 0)
			{
				RepeatBoltType selectedBolt;

				// Lógica de distancia de seguridad para virotes explosivos
				if (distance <= SafeDistanceForExplosives.X)
				{
					// DISTANCIA MÍNIMA: No usar explosivo, usar los otros 6 virotes disponibles
					RepeatBoltType[] normalBolts = new RepeatBoltType[]
					{
						RepeatBoltType.RepeatCopperBolt,
						RepeatBoltType.RepeatIronBolt,
						RepeatBoltType.RepeatDiamondBolt,
						RepeatBoltType.RepeatFireBolt,
						RepeatBoltType.RepeatPoisonBolt,
						RepeatBoltType.RepeatSeverelyPoisonousBolt
					};
					selectedBolt = normalBolts[m_random.Int(0, normalBolts.Length - 1)];
				}
				else if (distance >= SafeDistanceForExplosives.Y)
				{
					// DISTANCIA MÁXIMA: Usar virote explosivo
					selectedBolt = RepeatBoltType.RepeatExplosiveBolt;
				}
				else
				{
					// DISTANCIA INTERMEDIA: Puede usar CUALQUIERA de los 7 virotes
					RepeatBoltType[] allBolts = new RepeatBoltType[]
					{
						RepeatBoltType.RepeatCopperBolt,
						RepeatBoltType.RepeatIronBolt,
						RepeatBoltType.RepeatDiamondBolt,
						RepeatBoltType.RepeatExplosiveBolt,
						RepeatBoltType.RepeatFireBolt,
						RepeatBoltType.RepeatPoisonBolt,
						RepeatBoltType.RepeatSeverelyPoisonousBolt
					};
					selectedBolt = allBolts[m_random.Int(0, allBolts.Length - 1)];
				}

				data = RepeatCrossbowBlock.SetRepeatBoltType(data, selectedBolt);
				data = RepeatCrossbowBlock.SetCount(data, 1); // Cargar solo 1 para que dispare de 1 en 1 y varíe
				needsReload = true;
			}
			else if (boltType == RepeatBoltType.RepeatExplosiveBolt)
			{
				// Si por alguna razón ya tiene uno cargado pero se acercó demasiado, cambiarlo
				if (distance < SafeDistanceForExplosives.X)
				{
					RepeatBoltType[] safeBolts = new RepeatBoltType[]
					{
						RepeatBoltType.RepeatCopperBolt,
						RepeatBoltType.RepeatIronBolt,
						RepeatBoltType.RepeatDiamondBolt,
						RepeatBoltType.RepeatFireBolt,
						RepeatBoltType.RepeatPoisonBolt,
						RepeatBoltType.RepeatSeverelyPoisonousBolt
					};
					RepeatBoltType replacementBolt = safeBolts[m_random.Int(0, safeBolts.Length - 1)];
					data = RepeatCrossbowBlock.SetRepeatBoltType(data, replacementBolt);
					needsReload = true;
				}
			}

			if (needsReload)
			{
				int newValue = Terrain.MakeBlockValue(repeatCrossbowIndex, 0, data);
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
				m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
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
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				}

				// Asignar los tiempos correspondientes según el arma usada
				if (contents == BlocksManager.GetBlockIndex<ImprovedMusketBlock>())
				{
					CooldownTimer = ImprovedMusketCooldown;
					AimTimeTimer = ImprovedMusketAimTime;
				}
				else if (contents == musketIndex)
				{
					CooldownTimer = MusketCooldown;
					AimTimeTimer = MusketAimTime;
				}
				else if (contents == BlocksManager.GetBlockIndex<FlameThrowerBlock>())
				{
					CooldownTimer = FlameThrowerCooldown;
					AimTimeTimer = FlameThrowerAimTime;
				}
				else if (contents == BlocksManager.GetBlockIndex<CrossbowBlock>())
				{
					CooldownTimer = CrossbowCooldown;
					AimTimeTimer = CrossbowAimTime;
				}
				else if (contents == BlocksManager.GetBlockIndex<RepeatCrossbowBlock>())
				{
					CooldownTimer = RepeatCrossbowCooldown;
					AimTimeTimer = RepeatCrossbowAimTime;
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
				m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
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
