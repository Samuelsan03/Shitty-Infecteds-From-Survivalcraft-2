using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class ComponentZombieAI : Component, IUpdateable
	{
		// === NUEVO: Suscripción a heridas de la montura ===
		private Action<Injury> m_mountInjuredHandler;
		private ComponentHealth m_subscribedMountHealth;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentZombieChaseBehavior m_componentChaseBehavior;
		private ComponentCreatureClothing m_componentCreatureClothing;
		private float m_blockDestroyTimer;
		private const float BLOCK_DESTROY_INTERVAL = 0.5f;
		private const float BLOCK_DESTROY_RANGE = 2f;

		private static readonly HashSet<string> MountableCreatures = new HashSet<string>
		{
			"Horse_Bay_Saddled",
			"Horse_White_Saddled",
			"Horse_Palomino_Saddled",
			"Horse_Black_Saddled",
			"Camel_Saddled",
			"Horse_Chestnut_Saddled",
			"Donkey_Saddled",
			"FlyingInfected1"
		};

		public const float MountDetectionRange = 2.5f;

		public enum MountState
		{
			None,
			Searching,
			Mounting,
			Mounted,
			Dismounting
		}

		/// <summary>
		/// Enum para el estado de recarga de armas de fuego en la IA
		/// </summary>
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

		public bool CanItBeMounted;
		public MountState CurrentMountState { get; private set; } = MountState.None;

		public bool CanUseInventory;
		public bool CanWearClothing;
		public bool CanDestroyBlocks;

		/// <summary>
		/// Estado actual de recarga del arma de fuego
		/// </summary>
		public FirearmReloadState CurrentFirearmReloadState { get; private set; } = FirearmReloadState.None;

		public Vector2 DistanceRange = new Vector2(5f, 100f);
		public Vector2 DistanceRangeOfThrowable = new Vector2(5f, 15f);
		public Vector2 SafeDistanceForExplosives = new Vector2(20f, 100f);

		public float ImprovedMusketCooldown = 0.01f;
		public float ImprovedMusketAimTime = 1.5f;

		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		public float FlameThrowerCooldown = 0.01f;
		public float FlameThrowerAimTime = 1.5f;

		public float CrossbowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;

		public float RepeatCrossbowCooldown = 0.01f;
		public float RepeatCrossbowAimTime = 1.5f;

		public float BowCooldown = 0.01f;
		public float BowAimTime = 1.5f;

		public float ThrowableCooldown = 0.01f;
		public float ThrowableAimTime = 1.5f;

		public float CooldownTimer;
		public float AimTimeTimer;

		private const float FirearmReloadPauseTime = 1.5f;

		/// <summary>
		/// Estructura para almacenar los datos de las armas de fuego usando el nombre del bloque.
		/// </summary>
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

		private float m_equipTimer;
		private bool m_isEquipping;
		private int m_equipSlot;
		private int m_equipValue;

		private bool m_isFirearmAiming;
		private float m_firearmAimTimer;
		private float m_firearmReloadPauseTimer;
		private bool m_isWaitingForFirearmReload;
		private bool m_justFinishedReloading;
		private FirearmData? m_currentFirearmData;

		private Random m_random = new Random();

		private bool? m_cachedUsesNormalAnimation;
		private string m_cachedEntityName;

		private ComponentRider m_componentRider;
		private ComponentMount m_currentMount;
		private DynamicArray<ComponentBody> m_nearbyBodies = new DynamicArray<ComponentBody>();

		public static readonly HashSet<string> NormalAnimationCreatures = new HashSet<string>
		{
			"GhostNormal",
			"FatInfected",
			"FatInfectedArsonist",
			"FatInfectedPoisonous",
			"FatInfectedFrozen"
		};

		public bool IsMounted => CurrentMountState == MountState.Mounted;
		public ComponentMount CurrentMount => m_currentMount;

		private bool UsesNormalAimAnimation()
		{
			if (m_cachedUsesNormalAnimation.HasValue)
			{
				return m_cachedUsesNormalAnimation.Value;
			}

			if (Entity?.ValuesDictionary?.DatabaseObject != null)
			{
				m_cachedEntityName = Entity.ValuesDictionary.DatabaseObject.Name;
				m_cachedUsesNormalAnimation = NormalAnimationCreatures.Contains(m_cachedEntityName);
				return m_cachedUsesNormalAnimation.Value;
			}

			m_cachedUsesNormalAnimation = false;
			return false;
		}

		/// <summary>
		/// Aplica la configuración visual de apuntado según el tipo de criatura y arma.
		/// Para criaturas con animación normal: NO toca nada (deja la animación normal).
		/// Para las demás: solo se fija AimHandAngleOrder = 0 para que no muevan los brazos.
		/// </summary>
		private void ApplyAimVisualSettings(bool isBow, bool isCrossbow, bool isFlameThrower, bool isFirearm = false)
		{
			if (!UsesNormalAimAnimation())
			{
				m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
			}
			// Si usa animación normal, no se modifica nada
		}

		/// <summary>
		/// Reproduce los efectos de recarga: sonido de reload y partículas KillParticle
		/// </summary>
		private void PlayReloadEffects()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position + new Vector3(0f, m_componentCreature.ComponentBody.StanceBoxSize.Y / 2f, 0f);
			float size = m_componentCreature.ComponentBody.StanceBoxSize.X;

			KillParticleSystem killParticleSystem = new KillParticleSystem(m_subsystemTerrain, position, size);
			m_subsystemParticles.AddParticleSystem(killParticleSystem, false);

			m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), position, 10f, false);
		}

		/// <summary>
		/// Establece el estado de recarga y reproduce efectos si hay cambio de estado
		/// </summary>
		private void SetFirearmReloadState(FirearmReloadState newState)
		{
			if (CurrentFirearmReloadState != newState)
			{
				CurrentFirearmReloadState = newState;

				if (newState == FirearmReloadState.Reloading || newState == FirearmReloadState.Loaded)
				{
					PlayReloadEffects();
				}
			}
		}

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

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "RevolverBlock",
				MaxAmmo = 6,
				FireMode = FirearmFireMode.SemiAuto,
				AimTimeBeforeShot = 0.15f,
				CooldownAfterShot = 0.45f,
				GetAmmoCount = (data) => RevolverBlock.GetAmmoCount(data),
				SetAmmoCount = (data, count) => RevolverBlock.SetAmmoCount(data, count),
				GetLoadState = (data) => RevolverBlock.GetLoadState(data) == RevolverBlock.LoadState.Loaded,
				SetLoadState = (data, state) => RevolverBlock.SetLoadState(data, state == 1 ? RevolverBlock.LoadState.Loaded : RevolverBlock.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "IZH43Block",
				MaxAmmo = 2,
				FireMode = FirearmFireMode.SemiAuto,
				AimTimeBeforeShot = 0.2f,
				CooldownAfterShot = 0.5f,
				GetAmmoCount = (data) => IZH43Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => IZH43Block.SetAmmoCount(data, count),
				GetLoadState = (data) => IZH43Block.GetLoadState(data) == IZH43Block.LoadState.Loaded,
				SetLoadState = (data, state) => IZH43Block.SetLoadState(data, state == 1 ? IZH43Block.LoadState.Loaded : IZH43Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "BK93Block",
				MaxAmmo = 2,
				FireMode = FirearmFireMode.SemiAuto,
				AimTimeBeforeShot = 0.2f,
				CooldownAfterShot = 0.5f,
				GetAmmoCount = (data) => BK93Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => BK93Block.SetAmmoCount(data, count),
				GetLoadState = (data) => BK93Block.GetLoadState(data) == BK93Block.LoadState.Loaded,
				SetLoadState = (data, state) => BK93Block.SetLoadState(data, state == 1 ? BK93Block.LoadState.Loaded : BK93Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "UziBlock",
				MaxAmmo = 32,
				FireMode = FirearmFireMode.Automatic,
				AimTimeBeforeShot = 0.15f,
				CooldownAfterShot = 1.5f,
				GetAmmoCount = (data) => UziBlock.GetAmmoCount(data),
				SetAmmoCount = (data, count) => UziBlock.SetAmmoCount(data, count),
				GetLoadState = (data) => UziBlock.GetLoadState(data) == UziBlock.LoadState.Loaded,
				SetLoadState = (data, state) => UziBlock.SetLoadState(data, state == 1 ? UziBlock.LoadState.Loaded : UziBlock.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "Mac10Block",
				MaxAmmo = 30,
				FireMode = FirearmFireMode.Automatic,
				AimTimeBeforeShot = 0.12f,
				CooldownAfterShot = 1.3f,
				GetAmmoCount = (data) => Mac10Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => Mac10Block.SetAmmoCount(data, count),
				GetLoadState = (data) => Mac10Block.GetLoadState(data) == Mac10Block.LoadState.Loaded,
				SetLoadState = (data, state) => Mac10Block.SetLoadState(data, state == 1 ? Mac10Block.LoadState.Loaded : Mac10Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "M4Block",
				MaxAmmo = 30,
				FireMode = FirearmFireMode.Automatic,
				AimTimeBeforeShot = 0.25f,
				CooldownAfterShot = 1.6f,
				GetAmmoCount = (data) => M4Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => M4Block.SetAmmoCount(data, count),
				GetLoadState = (data) => M4Block.GetLoadState(data) == M4Block.LoadState.Loaded,
				SetLoadState = (data, state) => M4Block.SetLoadState(data, state == 1 ? M4Block.LoadState.Loaded : M4Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "Master308Block",
				MaxAmmo = 5,
				FireMode = FirearmFireMode.BoltAction,
				AimTimeBeforeShot = 0.045f,
				CooldownAfterShot = 0.45f,
				GetAmmoCount = (data) => Master308Block.GetAmmoCount(data),
				SetAmmoCount = (data, count) => Master308Block.SetAmmoCount(data, count),
				GetLoadState = (data) => Master308Block.GetLoadState(data) == Master308Block.LoadState.Loaded,
				SetLoadState = (data, state) => Master308Block.SetLoadState(data, state == 1 ? Master308Block.LoadState.Loaded : Master308Block.LoadState.Empty)
			});

			m_firearmsList.Add(new FirearmData
			{
				BlockName = "MP5SSDBlock",
				MaxAmmo = 30,
				FireMode = FirearmFireMode.Automatic,
				AimTimeBeforeShot = 0.15f,
				CooldownAfterShot = 1.5f,
				GetAmmoCount = (data) => MP5SSDBlock.GetAmmoCount(data),
				SetAmmoCount = (data, count) => MP5SSDBlock.SetAmmoCount(data, count),
				GetLoadState = (data) => MP5SSDBlock.GetLoadState(data) == MP5SSDBlock.LoadState.Loaded,
				SetLoadState = (data, state) => MP5SSDBlock.SetLoadState(data, state == 1 ? MP5SSDBlock.LoadState.Loaded : MP5SSDBlock.LoadState.Empty)
			});

			m_firearmsInitialized = true;
		}

		/// <summary>
		/// Detecta si la montura actual es una criatura voladora.
		/// </summary>
		private bool IsFlyingMount()
		{
			if (m_componentRider?.Mount == null) return false;
			ComponentLocomotion locomotion = m_componentRider.Mount.Entity.FindComponent<ComponentLocomotion>();
			return locomotion != null && locomotion.FlySpeed > 0f;
		}

		/// <summary>
		/// Verifica si una montura específica puede volar.
		/// </summary>
		private bool IsFlyingMount(ComponentMount mount)
		{
			if (mount == null || mount.Entity == null) return false;
			ComponentLocomotion locomotion = mount.Entity.FindComponent<ComponentLocomotion>();
			return locomotion != null && locomotion.FlySpeed > 0f;
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(false);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(false);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentCreatureClothing = Entity.FindComponent<ComponentCreatureClothing>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			CanWearClothing = valuesDictionary.GetValue<bool>("CanWearClothing", false);
			CanDestroyBlocks = valuesDictionary.GetValue<bool>("CanDestroyBlocks", false);
			CanItBeMounted = valuesDictionary.GetValue<bool>("CanItBeMounted", false);

			m_componentRider = Entity.FindComponent<ComponentRider>(false);

			if (CanItBeMounted && m_subsystemBodies == null)
			{
				m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			}

			_ = UsesNormalAimAnimation();

			CurrentMountState = CanItBeMounted ? MountState.Searching : MountState.None;

			InitializeFirearmsList();

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

			if (contents == arrowIndex || contents == repeatBoltIndex || contents == flameBulletIndex)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		public virtual void Update(float dt)
		{
			if (m_componentCreature != null && m_componentCreature.ComponentHealth != null
			&& m_componentCreature.ComponentHealth.Health <= 0f
			&& m_componentRider != null)
			{
				UnsubscribeFromMountInjuries(); // NUEVO

				ComponentBody riderBody = m_componentCreature.ComponentBody;
				if (riderBody != null && riderBody.ParentBody != null)
				{
					riderBody.Velocity = riderBody.ParentBody.Velocity;
					riderBody.ParentBody = null;
					riderBody.ParentBodyPositionOffset = Vector3.Zero;
					riderBody.ParentBodyRotationOffset = Quaternion.Identity;
				}
				m_componentRider.m_isAnimating = false;
				m_componentRider.m_isDismounting = false;
				m_currentMount = null;
				CurrentMountState = MountState.None;
			}

			UpdateMountingBehavior(dt);

			if (CanWearClothing && m_componentCreatureClothing != null && m_componentMiner?.Inventory != null)
			{
				if (!m_isEquipping)
				{
					int slot = FindClothingSlot();
					if (slot >= 0)
					{
						m_equipSlot = slot;
						m_equipValue = m_componentMiner.Inventory.GetSlotValue(slot);
						m_equipTimer = 0.5f;
						m_isEquipping = true;
					}
				}
				else
				{
					m_equipTimer -= m_subsystemTime.GameTimeDelta;
					if (m_equipTimer <= 0f)
					{
						EquipClothing(m_equipSlot, m_equipValue);
						m_isEquipping = false;
						m_equipTimer = 0f;
					}
				}
			}

			if (!CanUseInventory)
				return;

			if (m_componentCreature?.ComponentHealth?.Health <= 0f)
				return;

			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
				return;

			ComponentCreature target = m_componentChaseBehavior?.Target;

			if (target == null || target.ComponentHealth.Health <= 0f)
			{
				CancelAiming();
				if (IsMounted) StopMount();
				return;
			}

			bool isMounted = IsMounted;
			bool isFlyingMount = isMounted && IsFlyingMount();

			if (isMounted)
			{
				ComponentPathfinding pathfinding = Entity.FindComponent<ComponentPathfinding>(false);
				if (pathfinding != null)
				{
					pathfinding.Stop();
				}
			}

			Vector3 myPosition = isMounted ? m_componentRider.Mount.ComponentBody.Position : m_componentBody.Position;
			float distance = Vector3.Distance(myPosition, target.ComponentBody.Position);

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
					if (hasMelee || hasRanged)
					{
						HandleCloseRange(inventory, distance);
					}
					else
					{
						CancelAiming();
					}
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

			// Intentar destruir bloques que obstruyan el camino
			TryDestroyBlockingBlocks(target);

			if (isMounted)
			{
				if (isFlyingMount)
				{
					PilotMount(target);
				}
				else if (distance >= DistanceRange.X)
				{
					PilotMount(target);
				}
				else
				{
					// Ya no se desmonta al estar cerca, se mantiene montado y ataca
					// Simplemente se orienta hacia el objetivo
					PilotMount(target); // o bien no hacer nada, pero para mantener coherencia se llama a PilotMount
				}
			}
		}

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
						CurrentMountState = MountState.Searching;
					}
					break;
			}
		}

		private ComponentMount FindNearestMountableCreature()
		{
			if (m_subsystemBodies == null)
				return null;

			Vector2 position = new Vector2(
				m_componentBody.Position.X,
				m_componentBody.Position.Z);

			m_nearbyBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(position, MountDetectionRange, m_nearbyBodies);

			float closestDistance = float.MaxValue;
			ComponentMount closestMount = null;

			float maxRangeSquared = MountDetectionRange * MountDetectionRange;

			foreach (ComponentBody body in m_nearbyBodies)
			{
				if (body.Entity == Entity)
					continue;

				if (!IsMountableCreature(body.Entity))
					continue;

				ComponentMount mount = body.Entity.FindComponent<ComponentMount>();
				if (mount == null)
					continue;

				if (mount.Rider != null)
					continue;

				ComponentHealth mountHealth = body.Entity.FindComponent<ComponentHealth>();
				if (mountHealth == null || mountHealth.Health <= 0f)
					continue;

				float distanceSquared = Vector3.DistanceSquared(
					m_componentBody.Position,
					body.Position);

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
			if (entity?.ValuesDictionary?.DatabaseObject == null)
				return false;

			return MountableCreatures.Contains(entity.ValuesDictionary.DatabaseObject.Name);
		}

		public void ForceDismount()
		{
			if (CurrentMountState == MountState.Mounted && m_componentRider != null)
			{
				UnsubscribeFromMountInjuries(); // NUEVO
				m_componentRider.StartDismounting();
				CurrentMountState = MountState.Dismounting;
			}
		}

		private void StopMount()
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			// Detener pathfinding de la montura
			ComponentPathfinding mountPathfinding = m_componentRider.Mount.Entity.FindComponent<ComponentPathfinding>();
			if (mountPathfinding != null)
				mountPathfinding.Stop();

			// Detener Pilot (si existe)
			ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
			if (pilot != null && pilot.Destination != null)
				pilot.Stop();

			// Detener SteedBehaviorImproved (si existe)
			ComponentSteedBehaviorImproved steedImproved = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehaviorImproved>();
			if (steedImproved != null)
			{
				// Forzar detención (nivel 1 = quieto)
				steedImproved.m_speedLevel = 1;
				steedImproved.SpeedOrder = 0;
				steedImproved.TurnOrder = 0f;
				steedImproved.JumpOrder = 0f;
				// Nota: no se puede resetear m_speed directamente, pero con SpeedOrder=0 y nivel=1 se detiene.
				return;
			}

			// Detener SteedBehavior normal (igual que en DefensiveAI)
			ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steedBehavior != null)
			{
				steedBehavior.m_speedLevel = 1;
				steedBehavior.m_speed = 0f;
				steedBehavior.SpeedOrder = 0;
				steedBehavior.TurnOrder = 0f;
				steedBehavior.JumpOrder = 0f;
			}
		}

		private void PilotMount(ComponentCreature target)
		{
			if (m_componentRider == null || m_componentRider.Mount == null)
				return;

			ComponentBody mountBody = m_componentRider.Mount.ComponentBody;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 myPos = mountBody.Position;
			bool isInAir = mountBody.StandingOnValue == null;
			bool isFlying = IsFlyingMount();

			Vector3 dirToTarget = targetPos - myPos;
			dirToTarget.Y = 0f;

			if (dirToTarget.LengthSquared() < 0.01f)
			{
				StopMount();
				return;
			}

			Vector3 forward = mountBody.Matrix.Forward;
			forward.Y = 0f;

			if (forward.LengthSquared() < 0.001f)
			{
				forward = Vector3.UnitZ;
			}

			forward = Vector3.Normalize(forward);
			dirToTarget = Vector3.Normalize(dirToTarget);

			float cross = forward.X * dirToTarget.Z - forward.Z * dirToTarget.X;
			float dot = Vector3.Dot(forward, dirToTarget);

			float turnOrder = MathUtils.Clamp(cross * 2f, -0.5f, 0.5f);

			float distance = Vector3.Distance(new Vector3(myPos.X, 0, myPos.Z), new Vector3(targetPos.X, 0, targetPos.Z));

			float stopDistance = isFlying ? 0.3f : 2f;

			int speedOrder = 0;
			if (distance > stopDistance)
			{
				if (dot > 0.2f)
				{
					speedOrder = 1;
				}
				else if (dot < -0.5f)
				{
					speedOrder = -1;
				}
			}

			// ==========================================
			// NUEVO: CONTROL DE VUELO PARA MONTURAS VOLADORAS
			// ==========================================
			if (isFlying && isInAir)
			{
				ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
				if (pilot != null)
				{
					Vector3 dirXZ = targetPos - myPos;
					dirXZ.Y = 0f;
					float hDist = dirXZ.Length();

					// Mantener distancia horizontal falsa > 2f para evitar aterrizaje
					Vector3 pilotDest = myPos;
					if (hDist > 0.01f)
					{
						dirXZ = Vector3.Normalize(dirXZ);
						pilotDest = myPos + dirXZ * MathUtils.Max(hDist, 2.5f);
					}
					else
					{
						Vector3 fwd = new Vector3(mountBody.Matrix.Forward.X, 0f, mountBody.Matrix.Forward.Z);
						if (fwd.LengthSquared() > 0.01f) fwd = Vector3.Normalize(fwd);
						pilotDest = myPos + fwd * 2.5f;
					}

					// Mantenerse 3 bloques por encima del enemigo
					pilotDest.Y = targetPos.Y + 3f;

					pilot.SetDestination(pilotDest, 1f, 1f, false, false, true, null);
				}
			}
			else
			{
				// Apagar pilot si es montura de tierra
				ComponentPilot pilot = Entity.FindComponent<ComponentPilot>(false);
				if (pilot != null && pilot.Destination != null)
				{
					pilot.Stop();
				}
			}

			ComponentSteedBehaviorImproved steedImp = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehaviorImproved>();
			if (steedImp != null)
			{
				steedImp.TurnOrder = turnOrder;
				steedImp.SpeedOrder = speedOrder;
				steedImp.JumpOrder = 0f;
				return;
			}

			ComponentSteedBehavior steed = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steed != null)
			{
				steed.TurnOrder = turnOrder;
				steed.SpeedOrder = speedOrder;
				steed.JumpOrder = 0f;
			}
		}

		private int FindClothingSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0)
				{
					int value = m_componentMiner.Inventory.GetSlotValue(i);
					int blockId = Terrain.ExtractContents(value);
					if (blockId == ClothingBlock.Index)
					{
						Block block = BlocksManager.Blocks[blockId];
						if (block.GetClothingData(value) != null)
							return i;
					}
				}
			}
			return -1;
		}

		private void EquipClothing(int slot, int value)
		{
			ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
			if (data == null)
				return;

			if (!m_componentCreatureClothing.CanWearClothing(value))
				return;

			var currentList = m_componentCreatureClothing.GetClothes(data.Slot);
			List<int> newList = new List<int>(currentList) { value };
			m_componentCreatureClothing.SetClothes(data.Slot, newList);
			m_componentMiner.Inventory.RemoveSlotItems(slot, 1);
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

			if (!IsMounted)
			{
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
			if (!block.IsAimable || block.GetProjectileSpeed(0) <= 0f) return false;

			SubsystemThrowableBlockBehavior subsystemThrowable = Project.FindSubsystem<SubsystemThrowableBlockBehavior>(false);
			if (subsystemThrowable == null) return false;

			int[] handledBlocks = subsystemThrowable.HandledBlocks;
			return handledBlocks.Length == 0 || Array.IndexOf(handledBlocks, blockIndex) >= 0;
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
				// NO sobrescribir AimHandAngleOrder para lanzables -
				// SubsystemThrowableBlockBehavior ya lo maneja correctamente (3.2f)
				AimTimeTimer -= m_subsystemTime.GameTimeDelta;
			}
			else
			{
				m_componentMiner.Aim(aim, AimState.Completed);
				// NO sobrescribir AimHandAngleOrder para lanzables -
				// SubsystemThrowableBlockBehavior ya lo maneja correctamente
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

				// Si es arma de fuego, usar la lógica específica
				if (IsFirearmBlock(contents))
				{
					HandleFirearmAttack(inventory, m_componentChaseBehavior.Target);
					return;
				}

				m_currentFirearmData = null;

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

			// Si es arma de fuego, usar la lógica específica de armas de fuego
			if (IsFirearmBlock(contents))
			{
				HandleFirearmAttack(inventory, target);
				return;
			}

			m_currentFirearmData = null;

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

			if (blockIndex == improvedMusketIndex || blockIndex == musketIndex || blockIndex == crossbowIndex || blockIndex == bowIndex || blockIndex == repeatCrossbowIndex || blockIndex == flameThrowerIndex)
				return true;

			if (IsFirearmBlock(blockIndex))
				return true;

			return false;
		}

		private int FindRangedWeaponSlot(IInventory inventory)
		{
			int firearmSlot = FindFirearmSlot(inventory);
			if (firearmSlot >= 0) return firearmSlot;

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

			if (contents == BlocksManager.GetBlockIndex<ImprovedMusketBlock>())
				EnsureImprovedMusketLoaded(inventory, slot, value);
			else if (contents == BlocksManager.GetBlockIndex<MusketBlock>())
				EnsureMusketLoaded(inventory, slot, value);
			else if (contents == BlocksManager.GetBlockIndex<FlameThrowerBlock>())
				EnsureFlameThrowerLoaded(inventory, slot, value);
			else if (contents == BlocksManager.GetBlockIndex<CrossbowBlock>())
				EnsureCrossbowLoaded(inventory, slot, value, distance);
			else if (contents == BlocksManager.GetBlockIndex<BowBlock>())
				EnsureBowLoaded(inventory, slot, value);
			else if (contents == BlocksManager.GetBlockIndex<RepeatCrossbowBlock>())
				EnsureRepeatCrossbowLoaded(inventory, slot, value, distance);
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

			if (state != FlameThrowerBlock.LoadState.Loaded || ammo == 0)
			{
				int currentBulletType = (data >> 8) & 3;
				int selectedBulletType = currentBulletType != 0 ? currentBulletType : m_random.Int(0, 1);

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

				if (distance <= SafeDistanceForExplosives.X)
				{
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
					selectedBolt = RepeatBoltType.RepeatExplosiveBolt;
				}
				else
				{
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
				data = RepeatCrossbowBlock.SetCount(data, 1);
				needsReload = true;
			}
			else if (boltType == RepeatBoltType.RepeatExplosiveBolt)
			{
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

		/// <summary>
		/// Obtiene los flags de tipo de arma para el slot activo actual (no-firearm).
		/// </summary>
		private void GetRangedWeaponTypeFlags(out bool isBow, out bool isCrossbow, out bool isFlameThrower, out bool isImprovedMusket)
		{
			int contents = Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(m_componentMiner.Inventory.ActiveSlotIndex));
			isBow = contents == BlocksManager.GetBlockIndex<BowBlock>();
			isCrossbow = contents == BlocksManager.GetBlockIndex<CrossbowBlock>() || contents == BlocksManager.GetBlockIndex<RepeatCrossbowBlock>();
			isFlameThrower = contents == BlocksManager.GetBlockIndex<FlameThrowerBlock>();
			isImprovedMusket = contents == BlocksManager.GetBlockIndex<ImprovedMusketBlock>();
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

			GetRangedWeaponTypeFlags(out bool isBow, out bool isCrossbow, out bool isFlameThrower, out bool isImprovedMusket);

			if (AimTimeTimer > 0f)
			{
				m_componentMiner.Aim(aim, AimState.InProgress);
				ApplyAimVisualSettings(isBow, isCrossbow, isFlameThrower, false);
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
					ApplyAimVisualSettings(isBow, isCrossbow, isFlameThrower, false);
				}

				if (isImprovedMusket)
				{
					CooldownTimer = ImprovedMusketCooldown;
					AimTimeTimer = ImprovedMusketAimTime;
				}
				else if (contents == musketIndex)
				{
					CooldownTimer = MusketCooldown;
					AimTimeTimer = MusketAimTime;
				}
				else if (isFlameThrower)
				{
					CooldownTimer = FlameThrowerCooldown;
					AimTimeTimer = FlameThrowerAimTime;
				}
				else if (isCrossbow && contents == BlocksManager.GetBlockIndex<RepeatCrossbowBlock>())
				{
					CooldownTimer = RepeatCrossbowCooldown;
					AimTimeTimer = RepeatCrossbowAimTime;
				}
				else if (isCrossbow)
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
			GetRangedWeaponTypeFlags(out bool isBow, out bool isCrossbow, out bool isFlameThrower, out bool isImprovedMusket);

			for (int i = 0; i < 3; i++)
			{
				m_componentMiner.Aim(aim, AimState.Completed);
				ApplyAimVisualSettings(isBow, isCrossbow, isFlameThrower, false);
			}
		}

		private void CancelAiming()
		{
			AimTimeTimer = MusketAimTime;
			CooldownTimer = 0f;
			Ray3 emptyAim = new Ray3(Vector3.Zero, Vector3.UnitZ);
			m_componentMiner.Aim(emptyAim, AimState.Cancelled);
			m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;

			m_isFirearmAiming = false;
			m_firearmAimTimer = 0f;
			m_isWaitingForFirearmReload = false;
			m_firearmReloadPauseTimer = 0f;
			m_justFinishedReloading = false;
			m_currentFirearmData = null;
			SetFirearmReloadState(FirearmReloadState.None);
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

		// ============================================
		// MÉTODOS DE ARMAS DE FUEGO
		// ============================================

		private bool IsFirearmBlock(int blockIndex)
		{
			for (int i = 0; i < m_firearmsList.Count; i++)
			{
				int firearmIndex = m_firearmsList[i].GetBlockIndex();
				if (firearmIndex >= 0 && firearmIndex == blockIndex) return true;
			}
			return false;
		}

		private int FindFirearmSlot(IInventory inventory)
		{
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

		private FirearmData? GetFirearmData(IInventory inventory, int slotIndex)
		{
			int blockId = Terrain.ExtractContents(inventory.GetSlotValue(slotIndex));
			for (int i = 0; i < m_firearmsList.Count; i++)
			{
				int firearmIndex = m_firearmsList[i].GetBlockIndex();
				if (firearmIndex >= 0 && firearmIndex == blockId) return m_firearmsList[i];
			}
			return null;
		}

		private bool IsFirearmEmpty(IInventory inventory, int slotIndex, FirearmData firearm)
		{
			int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			return !firearm.GetLoadState(data) || firearm.GetAmmoCount(data) == 0;
		}

		private void ReloadFirearm(IInventory inventory, int slotIndex, FirearmData firearm)
		{
			int value = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int blockId = firearm.GetBlockIndex();

			data = firearm.SetLoadState(data, 1);
			data = firearm.SetAmmoCount(data, firearm.MaxAmmo);

			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(blockId, 0, data), 1);
		}

		private void HandleFirearmAttack(IInventory inventory, ComponentCreature target)
		{
			int activeSlot = inventory.ActiveSlotIndex;
			FirearmData? firearmDataNullable = GetFirearmData(inventory, activeSlot);

			if (!firearmDataNullable.HasValue) return;
			FirearmData firearm = firearmDataNullable.Value;
			m_currentFirearmData = firearm;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentBody.BoundingBox.Center();
			Vector3 aimDir = Vector3.Normalize(targetPos - eyePos);
			Ray3 firearmRay = new Ray3(eyePos, aimDir);

			if (m_isWaitingForFirearmReload)
			{
				m_firearmReloadPauseTimer -= m_subsystemTime.GameTimeDelta;

				if (m_firearmReloadPauseTimer <= 0f)
				{
					m_isWaitingForFirearmReload = false;
					m_firearmReloadPauseTimer = 0f;
					m_justFinishedReloading = true;
					CurrentFirearmReloadState = FirearmReloadState.Loaded;

					PlayReloadEffects();
				}
				return;
			}

			if (CooldownTimer > 0f)
			{
				CooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			bool isEmpty = IsFirearmEmpty(inventory, activeSlot, firearm);

			if (isEmpty)
			{
				if (m_isFirearmAiming)
				{
					m_componentMiner.Aim(firearmRay, AimState.Cancelled);
					m_isFirearmAiming = false;
					m_firearmAimTimer = 0f;
				}

				ReloadFirearm(inventory, activeSlot, firearm);
				SetFirearmReloadState(FirearmReloadState.Reloading);
				m_isWaitingForFirearmReload = true;
				m_firearmReloadPauseTimer = FirearmReloadPauseTime;

				return;
			}

			CurrentFirearmReloadState = FirearmReloadState.Loaded;

			switch (firearm.FireMode)
			{
				case FirearmFireMode.Automatic:
					HandleAutomaticFirearm(firearmRay, firearm);
					break;
				case FirearmFireMode.SemiAuto:
					HandleSemiAutoFirearm(firearmRay, firearm);
					break;
				case FirearmFireMode.BoltAction:
					HandleBoltActionFirearm(firearmRay, firearm);
					break;
			}
		}

		private void HandleAutomaticFirearm(Ray3 firearmRay, FirearmData firearm)
		{
			if (!m_isFirearmAiming)
			{
				m_isFirearmAiming = true;
				m_firearmAimTimer = 0f;
				m_componentMiner.Aim(firearmRay, AimState.InProgress);

				if (!m_justFinishedReloading)
				{
					PlayReloadEffects();
				}
				m_justFinishedReloading = false;

				ApplyAimVisualSettings(false, false, false, true);
			}
			else
			{
				m_firearmAimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(firearmRay, AimState.InProgress);

				ApplyAimVisualSettings(false, false, false, true);

				if (m_firearmAimTimer > firearm.CooldownAfterShot)
				{
					m_componentMiner.Aim(firearmRay, AimState.Cancelled);
					m_isFirearmAiming = false;
					m_firearmAimTimer = 0f;
					CooldownTimer = 0.3f;
				}
			}
		}

		private void HandleSemiAutoFirearm(Ray3 firearmRay, FirearmData firearm)
		{
			if (!m_isFirearmAiming)
			{
				m_isFirearmAiming = true;
				m_firearmAimTimer = 0f;
				m_componentMiner.Aim(firearmRay, AimState.InProgress);
				ApplyAimVisualSettings(false, false, false, true);
			}
			else
			{
				m_firearmAimTimer += m_subsystemTime.GameTimeDelta;

				if (m_firearmAimTimer >= firearm.AimTimeBeforeShot)
				{
					m_componentMiner.Aim(firearmRay, AimState.Completed);

					m_isFirearmAiming = false;
					m_firearmAimTimer = 0f;
					CooldownTimer = firearm.CooldownAfterShot;
				}
				else
				{
					m_componentMiner.Aim(firearmRay, AimState.InProgress);
					ApplyAimVisualSettings(false, false, false, true);
				}
			}
		}

		private void HandleBoltActionFirearm(Ray3 firearmRay, FirearmData firearm)
		{
			if (!m_isFirearmAiming)
			{
				m_isFirearmAiming = true;
				m_firearmAimTimer = 0f;
				m_componentMiner.Aim(firearmRay, AimState.InProgress);
				ApplyAimVisualSettings(false, false, false, true);
			}
			else
			{
				m_firearmAimTimer += m_subsystemTime.GameTimeDelta;

				if (m_firearmAimTimer >= firearm.AimTimeBeforeShot)
				{
					m_componentMiner.Aim(firearmRay, AimState.Completed);

					m_isFirearmAiming = false;
					m_firearmAimTimer = 0f;
					CooldownTimer = firearm.CooldownAfterShot;
				}
				else
				{
					m_componentMiner.Aim(firearmRay, AimState.InProgress);
					ApplyAimVisualSettings(false, false, false, true);
				}
			}
		}

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

			// === VERIFICACIÓN DE MANADA ZOMBIE ===
			// Obtener nuestra manada
			ComponentZombieHerdBehavior myHerd = m_componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();

			if (myHerd != null && !string.IsNullOrEmpty(myHerd.HerdName))
			{
				// Verificar si el atacante es un zombie aliado (misma manada)
				ComponentZombieHerdBehavior attackerHerd = attacker.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (attackerHerd != null && attackerHerd.HerdName == myHerd.HerdName)
					return;

				// Verificar si el atacante está montando una criatura aliada
				ComponentRider attackerRider = attacker.Entity.FindComponent<ComponentRider>();
				if (attackerRider != null && attackerRider.Mount != null)
				{
					ComponentZombieHerdBehavior mountHerd = attackerRider.Mount.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (mountHerd != null && mountHerd.HerdName == myHerd.HerdName)
						return;
				}
			}
			// === FIN VERIFICACIÓN ===

			ComponentZombieChaseBehavior chaseBehavior = m_componentChaseBehavior;
			if (chaseBehavior == null) return;

			// Usar Attack con parámetros persistentes para persecución agresiva
			chaseBehavior.Attack(attacker, 30f, 60f, true);
		}

		private void TryDestroyBlockingBlocks(ComponentCreature target)
        {
            if (!CanDestroyBlocks) return;
            if (m_subsystemTerrain == null) return;
            if (target == null) return;

            m_blockDestroyTimer -= m_subsystemTime.GameTimeDelta;
            if (m_blockDestroyTimer > 0f) return;

            Vector3 myPos = m_componentBody.Position;
            Vector3 targetPos = target.ComponentBody.Position;
            Vector3 direction = Vector3.Normalize(targetPos - myPos);
            direction.Y = 0f;

            if (direction.LengthSquared() < 0.001f) return;
            direction = Vector3.Normalize(direction);

            int bedrockIndex = BlocksManager.GetBlockIndex("BedrockBlock");

            int baseX = Terrain.ToCell(myPos.X);
            int baseY = Terrain.ToCell(myPos.Y);
            int baseZ = Terrain.ToCell(myPos.Z);

            int forwardX = (direction.X > 0.3f) ? 1 : ((direction.X < -0.3f) ? -1 : 0);
            int forwardZ = (direction.Z > 0.3f) ? 1 : ((direction.Z < -0.3f) ? -1 : 0);

            bool destroyed = false;

            for (int dy = 0; dy <= 1 && !destroyed; dy++)
            {
                for (int dx = 0; dx <= 1 && !destroyed; dx++)
                {
                    for (int dz = 0; dz <= 1 && !destroyed; dz++)
                    {
                        int checkX = baseX + (dx == 0 ? 0 : forwardX);
                        int checkY = baseY + dy;
                        int checkZ = baseZ + (dz == 0 ? 0 : forwardZ);

                        if (!m_subsystemTerrain.Terrain.IsCellValid(checkX, checkY, checkZ)) continue;

                        int cellValue = m_subsystemTerrain.Terrain.GetCellValue(checkX, checkY, checkZ);
                        int contents = Terrain.ExtractContents(cellValue);

                        if (contents == 0) continue;
                        if (contents == bedrockIndex) continue;

                        Block block = BlocksManager.Blocks[contents];
                        if (!block.IsCollidable) continue;

                        Vector3 blockCenter = new Vector3(checkX + 0.5f, checkY + 0.5f, checkZ + 0.5f);
                        float distToBlock = Vector3.Distance(myPos, blockCenter);

                        if (distToBlock <= BLOCK_DESTROY_RANGE)
                        {
                            // Reproducir sonido de impacto antes de destruir
                            if (m_subsystemSoundMaterials != null)
                            {
                                m_subsystemSoundMaterials.PlayImpactSound(cellValue, blockCenter, 0.5f);
                            }

                            // Destruir con drops y partículas habilitados
                            m_subsystemTerrain.DestroyCell(0, checkX, checkY, checkZ, 0, false, false);
                            m_blockDestroyTimer = BLOCK_DESTROY_INTERVAL;
                            destroyed = true;
                        }
                    }
                }
            }
        }
	}
}
