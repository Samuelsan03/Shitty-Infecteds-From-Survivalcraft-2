using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class ComponentCreatureAI : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public enum FirearmReloadState
		{
			None,
			Reloading,
			Loaded
		}

		public enum FirearmFireMode
		{
			Automatic,
			SemiAuto,
			BoltAction
		}

		public enum MountState
		{
			None,
			Searching,
			Mounting,
			Mounted,
			Dismounting
		}

		private static readonly HashSet<string> MountableCreatures = new HashSet<string>
		{
			"Horse_Bay_Saddled",
			"Horse_White_Saddled",
			"Horse_Palomino_Saddled",
			"Horse_Black_Saddled",
			"Camel_Saddled",
			"Horse_Chestnut_Saddled",
			"Donkey_Saddled"
		};

		public const float MountDetectionRange = 2.5f;
		private const float FirearmReloadPauseTime = 1.5f;

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
		public bool CanItBeMounted { get; private set; }

		public MountState CurrentMountState { get; private set; } = MountState.None;
		public FirearmReloadState CurrentFirearmReloadState { get; private set; } = FirearmReloadState.None;
		public bool IsMounted => CurrentMountState == MountState.Mounted;
		public ComponentMount CurrentMount => m_currentMount;

		public bool IsOnFlyingMount
		{
			get
			{
				if (m_componentRider == null || m_componentRider.Mount == null) return false;
				return IsFlyingMount(m_componentRider.Mount);
			}
		}

		// === NUEVO: Suscripción a heridas de la montura ===
		private Action<Injury> m_mountInjuredHandler;
		private ComponentHealth m_subscribedMountHealth;

		private Action<Projectile> m_projectileAddedHandler;

		// Subsystems
		private SubsystemTime m_subsystemTime;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemProjectiles m_subsystemProjectiles;

		// Components
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentRider m_componentRider;
		private ComponentMount m_currentMount;
		private ComponentPilot m_componentPilot;

		// Timers (unificados con float + delta)
		private float m_aimTimer;
		private float m_cooldownTimer;
		private bool m_isAiming;
		private int m_originalActiveSlot = -1;

		private float m_throwableAimTimer;
		private float m_throwableCooldownTimer;
		private bool m_isAimingThrowable;

		// Firearm state
		private float m_firearmReloadPauseTimer;
		private bool m_isWaitingForFirearmReload;
		private bool m_isUsingFirearm;
		private FirearmData? m_currentFirearmData;

		// Misc
		private Random m_random = new Random();
		private DynamicArray<ComponentBody> m_nearbyBodies = new DynamicArray<ComponentBody>();

		private struct FirearmData
		{
			public string BlockName;
			public int MaxAmmo;
			public FirearmFireMode FireMode;
			public float AimTimeBeforeShot;
			public float CooldownAfterShot;
			public Func<int, int> GetAmmoCount;
			public Func<int, int, int> SetAmmoCount;
			public Func<int, bool> GetLoadState;
			public Func<int, int, int> SetLoadState;

			public int GetBlockIndex()
			{
				return BlocksManager.GetBlockIndex(BlockName);
			}
		}

		private static readonly List<FirearmData> m_firearmsList = new List<FirearmData>();
		private static bool m_firearmsInitialized = false;

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

		private static void InitializeFirearmsList()
		{
			if (m_firearmsInitialized) return;

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "AK47Block",
				MaxAmmo = 30,
				FireMode = FirearmFireMode.Automatic,
				AimTimeBeforeShot = 0.3f,
				CooldownAfterShot = 1.8f,
				GetAmmoCount = (data) => AK47Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => AK47Block.SetAmmoCount(data, count),
				GetLoadState = (data) => AK47Block.GetLoadState(data) == AK47Block.LoadState.Loaded,
				SetLoadState = (data, state) => AK47Block.SetLoadState(data, state == 1 ? AK47Block.LoadState.Loaded : AK47Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "DesertEagleBlock",
				MaxAmmo = 7,
				FireMode = FirearmFireMode.SemiAuto,
				AimTimeBeforeShot = 0.15f,
				CooldownAfterShot = 0.35f,
				GetAmmoCount = (data) => DesertEagleBlock.GetAmmoCount(data),
				SetAmmoCount = (data, count) => DesertEagleBlock.SetAmmoCount(data, count),
				GetLoadState = (data) => DesertEagleBlock.GetLoadState(data) == DesertEagleBlock.LoadState.Loaded,
				SetLoadState = (data, state) => DesertEagleBlock.SetLoadState(data, state == 1 ? DesertEagleBlock.LoadState.Loaded : DesertEagleBlock.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "SPAS12Block",
				MaxAmmo = 8,
				FireMode = FirearmFireMode.SemiAuto,
				AimTimeBeforeShot = 0.2f,
				CooldownAfterShot = 0.45f,
				GetAmmoCount = (data) => SPAS12Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => SPAS12Block.SetAmmoCount(data, count),
				GetLoadState = (data) => SPAS12Block.GetLoadState(data) == SPAS12Block.LoadState.Loaded,
				SetLoadState = (data, state) => SPAS12Block.SetLoadState(data, state == 1 ? SPAS12Block.LoadState.Loaded : SPAS12Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "SniperBlock",
				MaxAmmo = 1,
				FireMode = FirearmFireMode.BoltAction,
				AimTimeBeforeShot = 1.2f,
				CooldownAfterShot = 2.5f,
				GetAmmoCount = (data) => SniperBlock.GetAmmoCount(data),
				SetAmmoCount = (data, count) => SniperBlock.SetAmmoCount(data, count),
				GetLoadState = (data) => SniperBlock.GetLoadState(data) == SniperBlock.LoadState.Loaded,
				SetLoadState = (data, state) => SniperBlock.SetLoadState(data, state == 1 ? SniperBlock.LoadState.Loaded : SniperBlock.LoadState.Empty)
			});

			m_firearmsInitialized = true;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();
			m_componentRider = Entity.FindComponent<ComponentRider>(false);
			m_componentPilot = Entity.FindComponent<ComponentPilot>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			CanItBeMounted = valuesDictionary.GetValue<bool>("CanItBeMounted", false);
			CurrentMountState = CanItBeMounted ? MountState.Searching : MountState.None;

			InitializeFirearmsList();

			// NUEVO: Suscribirse al evento de adición de proyectiles
			if (m_subsystemProjectiles != null)
			{
				m_projectileAddedHandler = (projectile) =>
				{
					// Solo interesa si el proyectil pertenece a esta criatura
					if (projectile == null || projectile.Owner != m_componentCreature)
						return;

					int blockIndex = Terrain.ExtractContents(projectile.Value);
					// Flechas de arco (ArrowBlock) y virotes de ballesta repetidora (RepeatBoltBlock)
					if (blockIndex == ArrowBlock.Index || blockIndex == RepeatBoltBlock.Index)
					{
						// Al detenerse (suelo) desaparecerá en lugar de convertirse en pickable
						projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					}
				};
				m_subsystemProjectiles.ProjectileAdded += m_projectileAddedHandler;
			}
		}

		public override void Dispose()
		{
			if (m_subsystemProjectiles != null && m_projectileAddedHandler != null)
			{
				m_subsystemProjectiles.ProjectileAdded -= m_projectileAddedHandler;
				m_projectileAddedHandler = null;
			}
			base.Dispose();
		}

		public void Update(float dt)
		{
			UpdateMountingBehavior(dt);

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
				if (IsMounted) StopMount();
				return;
			}

			bool isMounted = m_componentRider != null && m_componentRider.Mount != null;

			Vector3 myPosition = isMounted ? m_componentRider.Mount.ComponentBody.Position : m_componentCreature.ComponentBody.Position;
			float distance = Vector3.Distance(myPosition, target.ComponentBody.Position);

			if (isMounted && m_componentPathfinding != null)
			{
				m_componentPathfinding.Stop();
			}

			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
			{
				StopAllCombat();
				return;
			}

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
					if (m_throwableCooldownTimer > 0f)
					{
						if (isMounted) PilotMount(target);
						return;
					}

					m_isAimingThrowable = true;
					m_throwableAimTimer = 0f;
					m_componentMiner.Aim(aimRay, AimState.InProgress);
					if (isMounted) PilotMount(target);
					return;
				}

				m_throwableAimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (m_throwableAimTimer >= ThrowableAimTime)
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
					m_throwableCooldownTimer = ThrowableCooldown;
					m_isAimingThrowable = false;
					m_throwableAimTimer = 0f;
				}
				if (isMounted) PilotMount(target);
				return;
			}
			else
			{
				if (m_isAimingThrowable)
				{
					StopThrowableCombat();
				}
			}

			// Decrementar cooldown de throwables
			if (m_throwableCooldownTimer > 0f)
			{
				m_throwableCooldownTimer -= m_subsystemTime.GameTimeDelta;
			}

			// 2. LÓGICA DE ARMAS DE FUEGO (PRIORIDAD ALTA)
			int firearmSlot = FindFirearmSlot();
			bool inRangedRange = distance <= RangedDistanceRange.Y &&
								  (distance > RangedDistanceRange.X || FindMeleeWeaponSlot() < 0);

			if (firearmSlot >= 0 && inRangedRange)
			{
				HandleFirearmAttack(target, firearmSlot);
				if (isMounted) PilotMount(target);
				return;
			}

			// Si estaba usando arma de fuego pero ya no puede, cancelar
			if (m_isUsingFirearm)
			{
				CancelFirearmAim();
			}

			// 3. LÓGICA DE RANGO LEGADO (MOSQUETE MEJORADO, MOSQUETE, ARCO, BALLESTA, BALLESTA REPETIDORA, LANZALLAMAS)
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

			bool shouldUseRanged = activeRangedSlot >= 0 && inRangedRange;

			if (!shouldUseRanged)
			{
				if (distance <= RangedDistanceRange.X && hasMeleeWeapon)
				{
					SwitchToSlot(meleeSlot);
					StopRangedCombat(false);
					// Eliminada la línea: if (isMounted) StopMount();
					// Ahora no se desmonta, ataca desde la montura si está montado
				}
				else
				{
					StopRangedCombat(true);
					if (isMounted) PilotMount(target);
				}
				return;
			}

			if (inventory.ActiveSlotIndex != activeRangedSlot)
			{
				SwitchToSlot(activeRangedSlot);
			}

			// Decrementar cooldown de rango legado
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				if (isMounted) PilotMount(target);
				return;
			}

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimTimer = 0f;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
				if (isMounted) PilotMount(target);
				return;
			}

			m_aimTimer += m_subsystemTime.GameTimeDelta;
			m_componentMiner.Aim(aimRay, AimState.InProgress);

			if (m_aimTimer >= currentAimTime)
			{
				if (isMusket)
				{
					FireWeapon(musketBlockIndex, aimRay);
				}
				else
				{
					m_componentMiner.Aim(aimRay, AimState.Completed);
				}

				m_cooldownTimer = currentCooldown;
				m_isAiming = false;
				m_aimTimer = 0f;
			}

			if (isMounted) PilotMount(target);
		}

		#region Mounting

		private void UpdateMountingBehavior(float dt)
		{
			if (!CanItBeMounted || m_componentRider == null)
			{
				if (CurrentMountState == MountState.Mounted || m_subscribedMountHealth != null)
				{
					UnsubscribeFromMountInjuries(); // NUEVO
				}
				CurrentMountState = MountState.None;
				return;
			}

			// Si la criatura está muerta, nunca intentar montar
			if (m_componentCreature.ComponentHealth.Health <= 0f)
			{
				if (m_componentRider.Mount != null)
				{
					UnsubscribeFromMountInjuries(); // NUEVO
					StopMount();

					ComponentBody riderBody = m_componentCreature.ComponentBody;
					if (riderBody.ParentBody != null)
					{
						riderBody.Velocity = riderBody.ParentBody.Velocity;
						riderBody.ParentBody = null;
						riderBody.ParentBodyPositionOffset = Vector3.Zero;
						riderBody.ParentBodyRotationOffset = Quaternion.Identity;
					}

					m_componentRider.m_isAnimating = false;
					m_componentRider.m_isDismounting = false;
					m_currentMount = null;
					ClearPilotDestination();
				}
				CurrentMountState = MountState.None;
				return;
			}

			switch (CurrentMountState)
			{
				case MountState.None:
					CurrentMountState = MountState.Searching;
					break;

				case MountState.Searching:
					ComponentMount nearestMount = FindNearestMountableCreature();
					if (nearestMount != null)
					{
						m_componentRider.StartMounting(nearestMount);
						m_currentMount = nearestMount;
						CurrentMountState = MountState.Mounting;
					}
					break;

				case MountState.Mounting:
					CurrentMountState = m_componentRider.Mount != null ? MountState.Mounted : MountState.Searching;
					if (CurrentMountState == MountState.Mounted)
					{
						SubscribeToMountInjuries(); // NUEVO
					}
					break;

				case MountState.Mounted:
					if (m_componentRider.Mount == null)
					{
						UnsubscribeFromMountInjuries(); // NUEVO
						m_currentMount = null;
						CurrentMountState = MountState.Searching;
						ClearPilotDestination();
					}
					else
					{
						ComponentHealth mountHealth = m_componentRider.Mount.Entity.FindComponent<ComponentHealth>();
						if (mountHealth != null && mountHealth.Health <= 0f)
						{
							UnsubscribeFromMountInjuries(); // NUEVO
							StopMount();

							ComponentBody riderBody = m_componentCreature.ComponentBody;
							if (riderBody.ParentBody != null)
							{
								riderBody.Velocity = riderBody.ParentBody.Velocity;
								riderBody.ParentBody = null;
								riderBody.ParentBodyPositionOffset = Vector3.Zero;
								riderBody.ParentBodyRotationOffset = Quaternion.Identity;
							}

							m_componentRider.m_isAnimating = false;
							m_componentRider.m_isDismounting = false;
							m_currentMount = null;
							CurrentMountState = MountState.Dismounting;
							ClearPilotDestination();
						}
					}
					break;

				case MountState.Dismounting:
					if (m_componentRider.Mount == null)
					{
						m_currentMount = null;
						CurrentMountState = MountState.Searching;
					}
					else
					{
						UnsubscribeFromMountInjuries(); // NUEVO
						StopMount();

						ComponentBody riderBody = m_componentCreature.ComponentBody;
						if (riderBody.ParentBody != null)
						{
							riderBody.Velocity = riderBody.ParentBody.Velocity;
							riderBody.ParentBody = null;
							riderBody.ParentBodyPositionOffset = Vector3.Zero;
							riderBody.ParentBodyRotationOffset = Quaternion.Identity;
						}

						m_componentRider.m_isAnimating = false;
						m_componentRider.m_isDismounting = false;
						m_currentMount = null;
						ClearPilotDestination();
						CurrentMountState = MountState.Searching;
					}
					break;
			}
		}

		private ComponentMount FindNearestMountableCreature()
		{
			Vector2 position = new Vector2(m_componentCreature.ComponentBody.Position.X, m_componentCreature.ComponentBody.Position.Z);
			m_nearbyBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(position, MountDetectionRange, m_nearbyBodies);

			float closestDistance = float.MaxValue;
			ComponentMount closestMount = null;
			float maxRangeSquared = MountDetectionRange * MountDetectionRange;

			foreach (ComponentBody body in m_nearbyBodies)
			{
				if (body.Entity == Entity || !IsMountableCreature(body.Entity)) continue;

				ComponentMount mount = body.Entity.FindComponent<ComponentMount>();
				if (mount == null || mount.Rider != null) continue;

				ComponentHealth mountHealth = body.Entity.FindComponent<ComponentHealth>();
				if (mountHealth == null || mountHealth.Health <= 0f) continue;

				float distanceSquared = Vector3.DistanceSquared(m_componentCreature.ComponentBody.Position, body.Position);
				if (distanceSquared <= maxRangeSquared && distanceSquared < closestDistance)
				{
					closestDistance = distanceSquared;
					closestMount = mount;
				}
			}

			return closestMount;
		}

		private bool IsMountableCreature(Entity entity)
		{
			if (entity?.ValuesDictionary?.DatabaseObject == null) return false;
			return MountableCreatures.Contains(entity.ValuesDictionary.DatabaseObject.Name);
		}

		private bool IsFlyingMount(ComponentMount mount)
		{
			if (mount == null || mount.Entity == null) return false;
			ComponentLocomotion mountLocomotion = mount.Entity.FindComponent<ComponentLocomotion>();
			return mountLocomotion != null && mountLocomotion.FlySpeed > 0f;
		}

		public void ForceDismount()
		{
			if (CurrentMountState == MountState.Mounted && m_componentRider != null)
			{
				UnsubscribeFromMountInjuries(); // NUEVO
				m_componentRider.StartDismounting();
				CurrentMountState = MountState.Dismounting;
				ClearPilotDestination();
			}
		}

		private void StopMount()
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steedBehavior != null)
			{
				// CORRECCIÓN: Forzar detención completa (nivel 1 = quieto, velocidad 0)
				steedBehavior.m_speedLevel = 1;
				steedBehavior.m_speed = 0f;
				steedBehavior.SpeedOrder = 0;
				steedBehavior.TurnOrder = 0f;
				steedBehavior.JumpOrder = 0f;
			}

			ClearPilotDestination();
		}

		private void PilotMount(ComponentCreature target)
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 mountPos = m_componentRider.Mount.ComponentBody.Position;
			float distance = Vector3.Distance(mountPos, targetPos);

			float desiredDistance = RangedDistanceRange.X + (RangedDistanceRange.Y - RangedDistanceRange.X) * 0.5f;
			Vector3 toTarget = Vector3.Normalize(targetPos - mountPos);
			toTarget.Y = 0f;

			Vector3 destination;
			if (distance < RangedDistanceRange.X + 2f)
			{
				destination = targetPos - toTarget * desiredDistance;
			}
			else if (distance > RangedDistanceRange.Y - 5f)
			{
				destination = targetPos - toTarget * desiredDistance;
			}
			else
			{
				Vector3 sideDir = new Vector3(-toTarget.Z, 0f, toTarget.X);
				if (m_random.Bool(0.5f)) sideDir = -sideDir;
				destination = mountPos + sideDir * 3f;
			}

			PilotMountToPosition(destination);
		}

		private void PilotMountToPosition(Vector3 targetPos)
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			ComponentBody mountBody = m_componentRider.Mount.ComponentBody;
			Vector3 myPos = mountBody.Position;
			float distance = Vector3.Distance(myPos, targetPos);

			if (distance < MountDetectionRange)
			{
				ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
				if (steedBehavior != null)
				{
					steedBehavior.SpeedOrder = 0;
					steedBehavior.TurnOrder = 0f;
					steedBehavior.JumpOrder = 0f;
				}
				ClearPilotDestination();
				return;
			}

			if (IsOnFlyingMount && m_componentPilot != null)
			{
				m_componentPilot.SetDestination(targetPos, 1f, MountDetectionRange, false, false, true, null);
			}
			else
			{
				ClearPilotDestination();

				ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
				if (steedBehavior == null) return;

				Vector3 dirToTarget = targetPos - myPos;
				dirToTarget.Y = 0f;
				if (dirToTarget.LengthSquared() < 0.01f) return;
				dirToTarget = Vector3.Normalize(dirToTarget);

				Vector3 forward = new Vector3(mountBody.Matrix.Forward.X, 0f, mountBody.Matrix.Forward.Z);
				if (forward.LengthSquared() < 0.01f) return;
				forward = Vector3.Normalize(forward);

				float dot = Vector3.Dot(forward, dirToTarget);
				float cross = forward.X * dirToTarget.Z - forward.Z * dirToTarget.X;
				float angleToTarget = MathF.Atan2(cross, dot);
				float turnAmount = MathUtils.Clamp(angleToTarget * 3f, -1f, 1f);

				steedBehavior.TurnOrder = turnAmount;
				steedBehavior.SpeedOrder = MathF.Abs(angleToTarget) < 0.5f ? 1 : 0;
			}
		}

		private void SetPilotDestination(Vector3 targetPos, float distance)
		{
			if (m_componentPilot == null) return;
			if (distance < MountDetectionRange)
			{
				m_componentPilot.Stop();
				return;
			}
			m_componentPilot.SetDestination(targetPos, 1f, MountDetectionRange, false, false, true, null);
		}

		private void ClearPilotDestination()
		{
			if (m_componentPilot == null) return;
			m_componentPilot.Stop();
		}

		#endregion

		#region Firearms

		private int FindFirearmSlot()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) > 0)
				{
					int blockId = Terrain.ExtractContents(inventory.GetSlotValue(i));
					for (int j = 0; j < m_firearmsList.Count; j++)
					{
						int firearmIndex = m_firearmsList[j].GetBlockIndex();
						if (firearmIndex >= 0 && firearmIndex == blockId) return i;
					}
				}
			}
			return -1;
		}

		private FirearmData? GetFirearmData(int slotIndex)
		{
			int blockId = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(slotIndex));
			for (int i = 0; i < m_firearmsList.Count; i++)
			{
				int firearmIndex = m_firearmsList[i].GetBlockIndex();
				if (firearmIndex >= 0 && firearmIndex == blockId) return m_firearmsList[i];
			}
			return null;
		}

		private bool IsFirearmEmpty(int slotIndex, FirearmData firearm)
		{
			int data = Terrain.ExtractData(m_componentMiner.Inventory.GetSlotValue(slotIndex));
			return !firearm.GetLoadState(data) || firearm.GetAmmoCount(data) == 0;
		}

		private void ReloadFirearm(int slotIndex, FirearmData firearm)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int blockId = firearm.GetBlockIndex();

			data = firearm.SetLoadState(data, 1);
			data = firearm.SetAmmoCount(data, firearm.MaxAmmo);

			m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(blockId, 0, data), 1);
		}

		private void HandleFirearmAttack(ComponentCreature target, int firearmSlot)
		{
			m_componentMiner.Inventory.ActiveSlotIndex = firearmSlot;
			FirearmData? firearmDataNullable = GetFirearmData(firearmSlot);

			if (!firearmDataNullable.HasValue) return;
			FirearmData firearm = firearmDataNullable.Value;
			m_currentFirearmData = firearm;
			m_isUsingFirearm = true;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 aimDir = Vector3.Normalize(targetPos - eyePos);
			Ray3 firearmRay = new Ray3(eyePos, aimDir);

			if (m_isWaitingForFirearmReload)
			{
				m_firearmReloadPauseTimer -= m_subsystemTime.GameTimeDelta;

				if (m_firearmReloadPauseTimer <= 0f)
				{
					m_isWaitingForFirearmReload = false;
					m_firearmReloadPauseTimer = 0f;
					CurrentFirearmReloadState = FirearmReloadState.Loaded;
					PlayReloadEffects();
				}
				return;
			}

			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			bool isEmpty = IsFirearmEmpty(firearmSlot, firearm);

			if (isEmpty)
			{
				if (m_isAiming)
				{
					m_componentMiner.Aim(firearmRay, AimState.Cancelled);
					m_isAiming = false;
					m_aimTimer = 0f;
				}

				ReloadFirearm(firearmSlot, firearm);
				SetFirearmReloadState(FirearmReloadState.Reloading);
				m_isWaitingForFirearmReload = true;
				m_firearmReloadPauseTimer = FirearmReloadPauseTime;
				return;
			}

			CurrentFirearmReloadState = FirearmReloadState.Loaded;

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimTimer = 0f;
				ApplyFirearmAimSettings();
				m_componentMiner.Aim(firearmRay, AimState.InProgress);
			}
			else
			{
				m_aimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(firearmRay, AimState.InProgress);
				ApplyFirearmAimSettings();

				if (m_aimTimer >= firearm.AimTimeBeforeShot)
				{
					m_componentMiner.Aim(firearmRay, AimState.Completed);
					m_isAiming = false;
					m_cooldownTimer = firearm.CooldownAfterShot;
					m_aimTimer = 0f;
				}
			}
		}

		private void ApplyFirearmAimSettings()
		{
			if (m_componentCreature?.ComponentCreatureModel == null) return;
			m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.2f;
			m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.1f, 0.05f);
			m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
		}

		private void SetFirearmReloadState(FirearmReloadState newState)
		{
			if (CurrentFirearmReloadState != newState)
			{
				CurrentFirearmReloadState = newState;
				if (newState == FirearmReloadState.Reloading)
				{
					PlayReloadEffects();
				}
			}
		}

		private void PlayReloadEffects()
		{
			if (m_componentCreature?.ComponentBody == null) return;
			Vector3 position = m_componentCreature.ComponentBody.Position + new Vector3(0f, m_componentCreature.ComponentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentCreature.ComponentBody.StanceBoxSize.X;

			KillParticleSystem killParticleSystem = new KillParticleSystem(m_subsystemTerrain, position, size);
			m_subsystemParticles.AddParticleSystem(killParticleSystem, false);

			m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), position, 10f, false);
		}

		private void CancelFirearmAim()
		{
			if (m_isAiming && m_isUsingFirearm)
			{
				if (m_componentCreature?.ComponentCreatureModel != null)
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
					Ray3 cancelRay = new Ray3(eyePos, forward);
					m_componentMiner.Aim(cancelRay, AimState.Cancelled);
				}
				m_isAiming = false;
				m_aimTimer = 0f;
			}

			m_isUsingFirearm = false;
			m_isWaitingForFirearmReload = false;
			m_firearmReloadPauseTimer = 0f;
			m_cooldownTimer = 0f;
			m_currentFirearmData = null;
			SetFirearmReloadState(FirearmReloadState.None);
		}

		#endregion

		#region Legacy Ranged Weapons

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

					if (ammoCount > 0) return i;

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

					if (draw == 15 && arrowType != null) return i;

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
						if (!isSafeDistance && arrowType == ArrowBlock.ArrowType.ExplosiveBolt) continue;
						return i;
					}

					if (draw == 0)
					{
						ArrowBlock.ArrowType randomBolt;
						if (isSafeDistance)
							randomBolt = m_crossbowBolts[m_random.Int(0, m_crossbowBolts.Length - 1)];
						else
							randomBolt = m_crossbowSafeBolts[m_random.Int(0, m_crossbowSafeBolts.Length - 1)];

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
						if (!isSafeDistance && boltType == RepeatBoltType.RepeatExplosiveBolt) continue;
						return i;
					}

					if (count == 0 || (draw == 0 && boltType == null))
					{
						RepeatBoltType randomBolt;
						if (isSafeDistance)
							randomBolt = m_repeatCrossbowBolts[m_random.Int(0, m_repeatCrossbowBolts.Length - 1)];
						else
							randomBolt = m_repeatCrossbowSafeBolts[m_random.Int(0, m_repeatCrossbowSafeBolts.Length - 1)];

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

					if (loadState == FlameThrowerBlock.LoadState.Loaded && ammo > 0) return i;

					int randomBulletType = m_random.Int(0, 1);

					int newData = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetAmmoCount(newData, 15);
					newData = FlameThrowerBlock.SetSwitchState(newData, false);
					newData = (newData & ~0x300) | ((randomBulletType & 3) << 8);

					int newValue = Terrain.MakeBlockValue(flameThrowerBlockIndex, 0, newData);

					inventory.RemoveSlotItems(i, 1);
					inventory.AddSlotItems(i, newValue, 1);
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

		#endregion

		#region Utility

		private bool IsThrowableLineOfSightClear(Vector3 start, Vector3 end, ComponentCreature target)
		{
			float maxDistance = Vector3.Distance(start, end);

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, end, 0.1f, (ComponentBody body, float distance) =>
			{
				return body.Entity != m_componentCreature.Entity && body.Entity != target.Entity;
			});

			if (bodyHit.HasValue && bodyHit.Value.Distance < maxDistance) return false;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(start, end, false, true, null);
			if (terrainHit.HasValue && terrainHit.Value.Distance < maxDistance - 0.5f) return false;

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
				if (behaviors[i] is SubsystemThrowableBlockBehavior) return true;
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
				if (inventory.GetSlotCount(i) > 0)
				{
					int blockId = Terrain.ExtractContents(value);

					if (blockId == MusketBlock.Index || blockId == ImprovedMusketBlock.Index ||
						blockId == BowBlock.Index || blockId == CrossbowBlock.Index ||
						blockId == RepeatCrossbowBlock.Index || blockId == FlameThrowerBlock.Index)
						continue;

					bool isFirearm = false;
					for (int j = 0; j < m_firearmsList.Count; j++)
					{
						int firearmIndex = m_firearmsList[j].GetBlockIndex();
						if (firearmIndex >= 0 && firearmIndex == blockId)
						{
							isFirearm = true;
							break;
						}
					}
					if (isFirearm) continue;

					if (IsThrowable(blockId)) return i;
				}
			}
			return -1;
		}

		private int FindBlockSlot(int blockIndex)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null) return -1;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(inventory.GetSlotValue(i)) == blockIndex) return i;
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
				if (block.GetMeleePower(value) > 1f) return i;
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

		#endregion

		#region Stop / Cancel

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
				m_throwableAimTimer = 0f;
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
				m_aimTimer = 0f;
			}

			m_cooldownTimer = 0f;

			if (restoreSlot && m_originalActiveSlot >= 0 && m_componentMiner.Inventory != null)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = m_originalActiveSlot;
				m_originalActiveSlot = -1;
			}
		}

		private void StopAllCombat()
		{
			StopThrowableCombat();
			CancelFirearmAim();
			StopRangedCombat(true);
		}

		#endregion

		// === NUEVO: Lógica para perseguir cuando golpean a la montura ===
		private void SubscribeToMountInjuries()
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			ComponentHealth mountHealth = m_componentRider.Mount.Entity.FindComponent<ComponentHealth>();
			if (mountHealth == null) return;

			// Evitar suscribirse dos veces a la misma montura
			if (m_subscribedMountHealth == mountHealth) return;

			// Desuscribirse de la montura anterior si existe
			if (m_subscribedMountHealth != null && m_mountInjuredHandler != null)
			{
				m_subscribedMountHealth.Injured = (Action<Injury>)Delegate.Remove(m_subscribedMountHealth.Injured, m_mountInjuredHandler);
			}

			if (m_mountInjuredHandler == null)
			{
				m_mountInjuredHandler = new Action<Injury>(OnMountInjured);
			}

			mountHealth.Injured = (Action<Injury>)Delegate.Combine(mountHealth.Injured, m_mountInjuredHandler);
			m_subscribedMountHealth = mountHealth;
		}

		private void UnsubscribeFromMountInjuries()
		{
			if (m_subscribedMountHealth != null && m_mountInjuredHandler != null)
			{
				m_subscribedMountHealth.Injured = (Action<Injury>)Delegate.Remove(m_subscribedMountHealth.Injured, m_mountInjuredHandler);
				m_subscribedMountHealth = null;
			}
		}

		private void OnMountInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker == null) return;

			// No reaccionar si el atacante es nosotros mismos
			if (attacker.Entity == Entity) return;

			// No reaccionar si el atacante es nuestra propia montura (caso raro)
			if (m_componentRider != null && m_componentRider.Mount != null && attacker.Entity == m_componentRider.Mount.Entity) return;

			// === VERIFICACIÓN DE MANADA NORMAL (ComponentHerdBehavior) ===
			ComponentHerdBehavior myHerd = m_componentCreature.Entity.FindComponent<ComponentHerdBehavior>();

			if (myHerd != null && !string.IsNullOrEmpty(myHerd.HerdName))
			{
				// Verificar si el atacante es de la misma manada
				ComponentHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentHerdBehavior>();
				if (attackerHerd != null && attackerHerd.HerdName == myHerd.HerdName)
					return;

				// Verificar si el atacante está montando una criatura aliada
				ComponentRider attackerRider = attacker.Entity.FindComponent<ComponentRider>();
				if (attackerRider != null && attackerRider.Mount != null)
				{
					ComponentHerdBehavior mountHerd = attackerRider.Mount.Entity.FindComponent<ComponentHerdBehavior>();
					if (mountHerd != null && mountHerd.HerdName == myHerd.HerdName)
						return;
				}
			}
			// === FIN VERIFICACIÓN ===

			ComponentChaseBehavior chaseBehavior = m_componentChaseBehavior;
			if (chaseBehavior == null) return;

			// Usar Attack con parámetros persistentes para persecución agresiva
			chaseBehavior.Attack(attacker, 30f, 60f, true);
		}
	}
}
