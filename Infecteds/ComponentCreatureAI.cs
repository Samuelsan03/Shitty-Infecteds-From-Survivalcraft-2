using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		// NOT loaded from XML - hardcoded values
		public Vector2 RangedDistanceRange = new Vector2(5f, 100f);
		public float MusketAimTime = 1.5f;
		public float MusketCooldown = 0.01f;
		public float ImprovedMusketAimTime = 1.5f;
		public float ImprovedMusketCooldown = 0.01f;
		public float BowAimTime = 1.5f;
		public float BowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;
		public float CrossbowCooldown = 0.01f;
		public float RepeatCrossbowAimTime = 1.5f;
		public float RepeatCrossbowCooldown = 0.01f;
		public float FlameThrowerAimTime = 1.5f;
		public float FlameThrowerCooldown = 0.01f;

		public Vector2 DistanceForUseOfThrowableObjects = new Vector2(5f, 15f);
		public float ThrowableAimTime = 1.5f;
		public float ThrowableCooldown = 0.01f;

		public Vector2 SafeDistanceForExplosives = new Vector2(20f, 100f);

		// Loaded from XML
		public bool CanUseInventory;

		// Private fields
		private SubsystemTime m_subsystemTime;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentPathfinding m_componentPathfinding;

		private double m_lastRangedTime;
		private double m_aimStartTime;
		private bool m_isAiming;
		private int m_originalActiveSlot = -1;

		private double m_lastThrowableTime;
		private double m_aimThrowableStartTime;
		private bool m_isAimingThrowable;

		private Random m_random = new Random();

		private static readonly ArrowBlock.ArrowType[] m_bowArrows = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};

		private static readonly ArrowBlock.ArrowType[] m_crossbowBolts = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		private static readonly ArrowBlock.ArrowType[] m_crossbowSafeBolts = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt
		};

		private static readonly RepeatBoltType[] m_repeatCrossbowBolts = new RepeatBoltType[]
		{
			RepeatBoltType.RepeatCopperBolt,
			RepeatBoltType.RepeatIronBolt,
			RepeatBoltType.RepeatDiamondBolt,
			RepeatBoltType.RepeatExplosiveBolt,
			RepeatBoltType.RepeatFireBolt,
			RepeatBoltType.RepeatPoisonBolt,
			RepeatBoltType.RepeatSeverelyPoisonousBolt
		};

		private static readonly RepeatBoltType[] m_repeatCrossbowSafeBolts = new RepeatBoltType[]
		{
			RepeatBoltType.RepeatCopperBolt,
			RepeatBoltType.RepeatIronBolt,
			RepeatBoltType.RepeatDiamondBolt,
			RepeatBoltType.RepeatFireBolt,
			RepeatBoltType.RepeatPoisonBolt,
			RepeatBoltType.RepeatSeverelyPoisonousBolt
		};

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
		}

		public void Update(float dt)
		{
			if (!CanUseInventory || m_componentCreature?.ComponentBody == null ||
				m_componentCreature?.ComponentCreatureModel == null)
			{
				StopAllCombat();
				return;
			}

			ComponentCreature target = m_componentChaseBehavior?.Target;
			bool hasValidTarget = target?.ComponentBody != null &&
								   target.ComponentHealth?.Health > 0f;

			if (!hasValidTarget)
			{
				StopAllCombat();
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
			{
				StopAllCombat();
				return;
			}

			double gameTime = m_subsystemTime.GameTime;
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 aimDir = Vector3.Normalize(targetCenter - eyePos);
			Ray3 aimRay = new Ray3(eyePos, aimDir);

			// 1. LÓGICA DE OBJETOS LANZABLES (PRIORIDAD)
			int throwableSlot = FindThrowableSlot();
			bool inThrowableRange = distance >= DistanceForUseOfThrowableObjects.X &&
									distance <= DistanceForUseOfThrowableObjects.Y;

			bool hasLineOfSight = IsThrowableLineOfSightClear(eyePos, targetCenter, target);
			bool isInFront = IsTargetInFront(eyePos, targetCenter);

			if (throwableSlot >= 0 && inThrowableRange && hasLineOfSight && isInFront)
			{
				if (m_isAiming)
				{
					StopRangedCombat(false);
				}

				if (m_componentPathfinding != null)
				{
					m_componentPathfinding.Stop();
				}

				if (inventory.ActiveSlotIndex != throwableSlot)
				{
					SwitchToSlot(throwableSlot);
				}

				if (!m_isAimingThrowable)
				{
					if ((gameTime - m_lastThrowableTime) < ThrowableCooldown)
					{
						return;
					}

					m_isAimingThrowable = true;
					m_aimThrowableStartTime = gameTime;
					m_componentMiner.Aim(aimRay, AimState.InProgress);
					return;
				}

				float aimDuration = (float)(gameTime - m_aimThrowableStartTime);
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (aimDuration >= ThrowableAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_lastThrowableTime = gameTime;
					m_isAimingThrowable = false;
				}
				return;
			}
			else
			{
				if (m_isAimingThrowable)
				{
					StopThrowableCombat();
				}
			}

			// 2. LÓGICA DE RANGO (MOSQUETE MEJORADO, MOSQUETE, ARCO, BALLESTA, BALLESTA REPETIDORA Y LANZALLAMAS)
			int improvedMusketBlockIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>(false, false);
			int musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			int bowBlockIndex = BlocksManager.GetBlockIndex<BowBlock>(false, false);
			int crossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false, false);
			int repeatCrossbowBlockIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>(false, false);
			int flameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false);

			int improvedMusketSlot = improvedMusketBlockIndex > 0 ? FindAndLoadImprovedMusketSlot(improvedMusketBlockIndex) : -1;
			int musketSlot = FindBlockSlot(musketBlockIndex);
			int bowSlot = bowBlockIndex > 0 ? FindAndLoadBowSlot(bowBlockIndex) : -1;
			int crossbowSlot = crossbowBlockIndex > 0 ? FindAndLoadCrossbowSlot(crossbowBlockIndex, distance) : -1;
			int repeatCrossbowSlot = repeatCrossbowBlockIndex > 0 ? FindAndLoadRepeatCrossbowSlot(repeatCrossbowBlockIndex, distance) : -1;
			int flameThrowerSlot = flameThrowerBlockIndex > 0 ? FindAndLoadFlameThrowerSlot(flameThrowerBlockIndex) : -1;

			int meleeSlot = FindMeleeWeaponSlot();
			bool hasMeleeWeapon = meleeSlot >= 0;

			int activeRangedSlot = -1;
			float currentAimTime = MusketAimTime;
			float currentCooldown = MusketCooldown;
			bool isMusket = false;

			// Prioridad: Mosquete Mejorado > Mosquete > Arco > Ballesta > Ballesta Repetidora > Lanzallamas
			if (improvedMusketSlot >= 0)
			{
				activeRangedSlot = improvedMusketSlot;
				currentAimTime = ImprovedMusketAimTime;
				currentCooldown = ImprovedMusketCooldown;
			}
			else if (musketSlot >= 0)
			{
				activeRangedSlot = musketSlot;
				isMusket = true;
			}
			else if (bowSlot >= 0)
			{
				activeRangedSlot = bowSlot;
				currentAimTime = BowAimTime;
				currentCooldown = BowCooldown;
			}
			else if (crossbowSlot >= 0)
			{
				activeRangedSlot = crossbowSlot;
				currentAimTime = CrossbowAimTime;
				currentCooldown = CrossbowCooldown;
			}
			else if (repeatCrossbowSlot >= 0)
			{
				activeRangedSlot = repeatCrossbowSlot;
				currentAimTime = RepeatCrossbowAimTime;
				currentCooldown = RepeatCrossbowCooldown;
			}
			else if (flameThrowerSlot >= 0)
			{
				activeRangedSlot = flameThrowerSlot;
				currentAimTime = FlameThrowerAimTime;
				currentCooldown = FlameThrowerCooldown;
			}

			bool shouldUseRanged = activeRangedSlot >= 0 &&
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

			if (inventory.ActiveSlotIndex != activeRangedSlot)
			{
				SwitchToSlot(activeRangedSlot);
			}

			if (!m_isAiming)
			{
				if ((gameTime - m_lastRangedTime) < currentCooldown)
				{
					return;
				}

				m_isAiming = true;
				m_aimStartTime = gameTime;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
				return;
			}

			float rangedAimDuration = (float)(gameTime - m_aimStartTime);
			m_componentMiner.Aim(aimRay, AimState.InProgress);

			if (rangedAimDuration >= currentAimTime)
			{
				if (isMusket)
				{
					FireWeapon(musketBlockIndex, aimRay);
				}
				else
				{
					// Mosquete Mejorado, Arco, Ballesta, Ballesta Repetidora y Lanzallamas
					// usan el comportamiento estándar del SubsystemBlockBehavior
					m_componentMiner.Aim(aimRay, AimState.Completed);
				}

				m_lastRangedTime = gameTime;
				m_isAiming = false;
			}
		}

		private int FindAndLoadImprovedMusketSlot(int improvedMusketBlockIndex)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(value) == improvedMusketBlockIndex)
				{
					int data = Terrain.ExtractData(value);
					int ammoCount = ImprovedMusketBlock.GetAmmoCount(data);

					// Si tiene munición, usarlo directamente
					if (ammoCount > 0)
					{
						return i;
					}

					// Si no tiene munición, recargar instantáneamente con 2 (capacidad máxima)
					int newData = ImprovedMusketBlock.SetAmmoCount(data, 2);
					int newValue = Terrain.MakeBlockValue(improvedMusketBlockIndex, 0, newData);

					inventory.RemoveSlotItems(i, 1);
					inventory.AddSlotItems(i, newValue, 1);
					return i;
				}
			}
			return -1;
		}

		private int FindAndLoadBowSlot(int bowBlockIndex)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(value) == bowBlockIndex)
				{
					int data = Terrain.ExtractData(value);
					int draw = BowBlock.GetDraw(data);
					ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);

					if (draw == 15 && arrowType != null)
					{
						return i;
					}

					if (draw == 0)
					{
						ArrowBlock.ArrowType randomArrow = m_bowArrows[m_random.Int(0, m_bowArrows.Length - 1)];
						int newData = BowBlock.SetDraw(data, 15);
						newData = BowBlock.SetArrowType(newData, randomArrow);
						int newValue = Terrain.MakeBlockValue(bowBlockIndex, 0, newData);

						inventory.RemoveSlotItems(i, 1);
						inventory.AddSlotItems(i, newValue, 1);
						return i;
					}
				}
			}
			return -1;
		}

		private int FindAndLoadCrossbowSlot(int crossbowBlockIndex, float distanceToTarget)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			bool isSafeDistance = distanceToTarget >= SafeDistanceForExplosives.X;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(value) == crossbowBlockIndex)
				{
					int data = Terrain.ExtractData(value);
					int draw = CrossbowBlock.GetDraw(data);
					ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);

					if (draw == 15 && arrowType != null)
					{
						if (!isSafeDistance && arrowType == ArrowBlock.ArrowType.ExplosiveBolt)
						{
							continue;
						}
						return i;
					}

					if (draw == 0)
					{
						ArrowBlock.ArrowType randomBolt;
						if (isSafeDistance)
						{
							randomBolt = m_crossbowBolts[m_random.Int(0, m_crossbowBolts.Length - 1)];
						}
						else
						{
							randomBolt = m_crossbowSafeBolts[m_random.Int(0, m_crossbowSafeBolts.Length - 1)];
						}

						int newData = CrossbowBlock.SetDraw(data, 15);
						newData = CrossbowBlock.SetArrowType(newData, randomBolt);
						int newValue = Terrain.MakeBlockValue(crossbowBlockIndex, 0, newData);

						inventory.RemoveSlotItems(i, 1);
						inventory.AddSlotItems(i, newValue, 1);
						return i;
					}
				}
			}
			return -1;
		}

		private int FindAndLoadRepeatCrossbowSlot(int repeatCrossbowBlockIndex, float distanceToTarget)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			bool isSafeDistance = distanceToTarget >= SafeDistanceForExplosives.X;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(value) == repeatCrossbowBlockIndex)
				{
					int data = Terrain.ExtractData(value);
					int draw = RepeatCrossbowBlock.GetDraw(data);
					RepeatBoltType? boltType = RepeatCrossbowBlock.GetRepeatBoltType(data);
					int count = RepeatCrossbowBlock.GetCount(data);

					if (draw == 15 && boltType != null && count > 0)
					{
						if (!isSafeDistance && boltType == RepeatBoltType.RepeatExplosiveBolt)
						{
							continue;
						}
						return i;
					}

					if (count == 0 || (draw == 0 && boltType == null))
					{
						RepeatBoltType randomBolt;
						if (isSafeDistance)
						{
							randomBolt = m_repeatCrossbowBolts[m_random.Int(0, m_repeatCrossbowBolts.Length - 1)];
						}
						else
						{
							randomBolt = m_repeatCrossbowSafeBolts[m_random.Int(0, m_repeatCrossbowSafeBolts.Length - 1)];
						}

						int newData = RepeatCrossbowBlock.SetDraw(data, 15);
						newData = RepeatCrossbowBlock.SetRepeatBoltType(newData, randomBolt);
						newData = RepeatCrossbowBlock.SetCount(newData, 1);
						int newValue = Terrain.MakeBlockValue(repeatCrossbowBlockIndex, 0, newData);

						inventory.RemoveSlotItems(i, 1);
						inventory.AddSlotItems(i, newValue, 1);
						return i;
					}
				}
			}
			return -1;
		}

		private int FindAndLoadFlameThrowerSlot(int flameThrowerBlockIndex)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(value) == flameThrowerBlockIndex)
				{
					int data = Terrain.ExtractData(value);
					FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
					int ammo = FlameThrowerBlock.GetAmmoCount(data);

					// Si está cargado y con munición, usarlo
					if (loadState == FlameThrowerBlock.LoadState.Loaded && ammo > 0)
					{
						return i;
					}

					// Si está vacío o sin munición, recargar instantáneamente con 15 de munición
					int randomBulletType = m_random.Int(0, 1); // 0 = Fire, 1 = Poison

					int newData = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetAmmoCount(newData, 15);
					newData = FlameThrowerBlock.SetSwitchState(newData, false);

					// SetBulletType (bits 8-9) replicando la lógica privada del subsystem
					newData = (newData & ~0x300) | ((randomBulletType & 3) << 8);

					int newValue = Terrain.MakeBlockValue(flameThrowerBlockIndex, 0, newData);

					inventory.RemoveSlotItems(i, 1);
					inventory.AddSlotItems(i, newValue, 1);
					return i;
				}
			}
			return -1;
		}

		private bool IsThrowableLineOfSightClear(Vector3 start, Vector3 end, ComponentCreature target)
		{
			float maxDistance = Vector3.Distance(start, end);

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, end, 0.1f, (ComponentBody body, float distance) =>
			{
				return body.Entity != m_componentCreature.Entity && body.Entity != target.Entity;
			});

			if (bodyHit.HasValue && bodyHit.Value.Distance < maxDistance)
			{
				return false;
			}

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(start, end, false, true, null);

			if (terrainHit.HasValue && terrainHit.Value.Distance < maxDistance - 0.5f)
			{
				return false;
			}

			return true;
		}

		private bool IsTargetInFront(Vector3 eyePos, Vector3 targetCenter)
		{
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 dirToTarget = Vector3.Normalize(targetCenter - eyePos);
			float dot = Vector3.Dot(forward, dirToTarget);
			return dot >= 0.2f;
		}

		private bool IsThrowable(int blockIndex)
		{
			if (blockIndex <= 0) return false;

			SubsystemBlockBehavior[] behaviors = m_subsystemBlockBehaviors.GetBlockBehaviors(blockIndex);
			for (int i = 0; i < behaviors.Length; i++)
			{
				if (behaviors[i] is SubsystemThrowableBlockBehavior)
				{
					return true;
				}
			}
			return false;
		}

		private int FindThrowableSlot()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && IsThrowable(Terrain.ExtractContents(value)))
				{
					return i;
				}
			}
			return -1;
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

		private void StopThrowableCombat()
		{
			if (m_isAimingThrowable)
			{
				if (m_componentCreature?.ComponentCreatureModel != null)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 forward = m_componentCreature.ComponentCreatureModel.EyeRotation.GetForwardVector();
					Ray3 aimRay = new Ray3(eyePos, forward);

					m_componentMiner.Aim(aimRay, AimState.Cancelled);
				}
				m_isAimingThrowable = false;
			}
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

		private void StopAllCombat()
		{
			StopThrowableCombat();
			StopRangedCombat(true);
		}
	}
}
