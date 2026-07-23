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
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentMiner m_componentMiner;
		private ComponentCreature m_componentCreature;
		private ComponentBody m_componentBody;
		private ComponentZombieChaseBehavior m_componentChaseBehavior;
		private ComponentCreatureClothing m_componentCreatureClothing;

		/// <summary>
		/// Lista de criaturas montables que la IA del zombi puede usar.
		/// </summary>
		private static readonly HashSet<string> MountableCreatures = new HashSet<string>
		{
			"Horse_Bay_Saddled",
			"Horse_White_Saddled",
			"Horse_Palomino_Saddled",
			"Horse_Black_Saddled",
			"Camel_Saddled",
			"Horse_Chestnut_Saddled",
			"Donkey_Saddled"
            // Agregar más criaturas montables aquí...
        };

		/// <summary>
		/// Distancia máxima para detectar y montar una criatura.
		/// </summary>
		public const float MountDetectionRange = 2.5f;

		/// <summary>
		/// Estados posibles de la montura para la IA del zombi.
		/// </summary>
		public enum MountState
		{
			None,
			Searching,
			Mounting,
			Mounted,
			Dismounting
		}

		/// <summary>
		/// Indica si la IA del zombi puede montarse en criaturas montables.
		/// </summary>
		public bool CanItBeMounted;

		/// <summary>
		/// Estado actual de montado de la IA.
		/// </summary>
		public MountState CurrentMountState { get; private set; } = MountState.None;

		public bool CanUseInventory;
		public bool CanWearClothing;

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

		private float m_equipTimer;
		private bool m_isEquipping;
		private int m_equipSlot;
		private int m_equipValue;

		private Random m_random = new Random();

		// Cache del resultado para no buscar en el HashSet cada frame
		private bool? m_cachedUsesNormalAnimation;
		private string m_cachedEntityName;

		// Componentes para montura
		private ComponentRider m_componentRider;
		private ComponentMount m_currentMount;
		private DynamicArray<ComponentBody> m_nearbyBodies = new DynamicArray<ComponentBody>();

		// Lista de criaturas que usan animación normal de apunte (manos levantadas como humano)
		public static readonly HashSet<string> NormalAnimationCreatures = new HashSet<string>
		{
			"GhostNormal"
            // Agregar más nombres de criaturas aquí según sea necesario
        };

		/// <summary>
		/// Verifica si la IA del zombi está actualmente montada en una criatura.
		/// </summary>
		public bool IsMounted => CurrentMountState == MountState.Mounted;

		/// <summary>
		/// Obtiene la montura actual si está montado.
		/// </summary>
		public ComponentMount CurrentMount => m_currentMount;

		/// <summary>
		/// Verifica si la criatura actual debe usar la animación normal de apunte (manos levantadas).
		/// Usa el nombre de la ENTIDAD (no del componente) para la comparación.
		/// </summary>
		private bool UsesNormalAimAnimation()
		{
			// Usar caché para evitar búsquedas repetitivas en el HashSet
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
			m_componentCreatureClothing = Entity.FindComponent<ComponentCreatureClothing>(false);

			CanUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory", false);
			CanWearClothing = valuesDictionary.GetValue<bool>("CanWearClothing", false);
			CanItBeMounted = valuesDictionary.GetValue<bool>("CanItBeMounted", false);

			// Componente de jinete para montura
			m_componentRider = Entity.FindComponent<ComponentRider>(false);

			// Si puede montar, asegurar que SubsystemBodies esté disponible
			if (CanItBeMounted && m_subsystemBodies == null)
			{
				m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			}

			// Precalcular si usa animación normal (el nombre de entidad no cambia en runtime)
			_ = UsesNormalAimAnimation();

			// Establecer estado inicial basado en si puede montar
			CurrentMountState = CanItBeMounted ? MountState.Searching : MountState.None;

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
			// Actualizar comportamiento de montura
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

			if (m_componentChaseBehavior?.Target == null)
			{
				CancelAiming();
				// Detener la montura cuando no hay objetivo
				if (IsMounted) StopMount();
				return;
			}

			ComponentCreature target = m_componentChaseBehavior.Target;

			// Detener la montura cuando el objetivo muere
			if (target.ComponentHealth.Health <= 0f)
			{
				CancelAiming();
				if (IsMounted) StopMount();
				return;
			}

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

			bool isMounted = IsMounted;

			// Si está montado, el pathfinding del jinete no hace mover a la montura, así que lo detenemos
			if (isMounted)
			{
				ComponentPathfinding pathfinding = Entity.FindComponent<ComponentPathfinding>(false);
				if (pathfinding != null)
				{
					pathfinding.Stop();
				}
			}

			// Usar la posición real de la montura para calcular distancias si está montado
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
						if (isMounted) StopMount();
					}
					else
					{
						CancelAiming();
						if (isMounted) StopMount();
					}
				}
				else if (distance <= DistanceRangeOfThrowable.Y)
				{
					HandleThrowableAttack(inventory, target, distance);
					if (isMounted) PilotMount(target);
				}
				else
				{
					if (hasRanged)
					{
						HandleRangedAttack(inventory, target, distance);
						if (isMounted) PilotMount(target);
					}
					else
					{
						CancelAiming();
						if (isMounted) StopMount();
					}
				}
			}
			else
			{
				if (distance < DistanceRange.X)
				{
					if (hasMelee)
					{
						HandleCloseRange(inventory, distance);
						if (isMounted) StopMount();
					}
					else
					{
						// Sin melee pero con rango, seguir disparando desde la montura
						if (hasRanged)
						{
							HandleCloseRange(inventory, distance);
							if (isMounted) PilotMount(target);
						}
						else
						{
							HandleCloseRange(inventory, distance);
							if (isMounted) StopMount();
						}
					}
				}
				else if (distance <= DistanceRange.Y)
				{
					HandleRangedAttack(inventory, target, distance);
					if (isMounted) PilotMount(target);
				}
				else
				{
					CancelAiming();
					if (isMounted) StopMount();
				}
			}
		}

		#region Mounting Behavior

		/// <summary>
		/// Actualiza el comportamiento de montura de la IA del zombi.
		/// </summary>
		private void UpdateMountingBehavior(float dt)
		{
			if (!CanItBeMounted)
			{
				CurrentMountState = MountState.None;
				return;
			}

			if (m_componentRider == null)
			{
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
					if (m_componentRider.Mount != null)
					{
						CurrentMountState = MountState.Mounted;
					}
					else
					{
						CurrentMountState = MountState.Searching;
					}
					break;

				case MountState.Mounted:
					if (m_componentRider.Mount == null)
					{
						m_currentMount = null;
						CurrentMountState = MountState.Searching;
					}
					else
					{
						// Si la montura muere mientras está montada, desmontar de inmediato
						ComponentHealth mountHealth = m_componentRider.Mount.Entity.FindComponent<ComponentHealth>();
						if (mountHealth != null && mountHealth.Health <= 0f)
						{
							m_componentRider.StartDismounting();
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
					break;
			}
		}

		/// <summary>
		/// Busca la criatura montable más cercana dentro del rango de detección.
		/// </summary>
		/// <returns>El ComponentMount de la criatura montable más cercana, o null si no hay ninguna.</returns>
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

				// No intentar montar si la criatura está muerta
				ComponentHealth mountHealth = body.Entity.FindComponent<ComponentHealth>();
				if (mountHealth == null || mountHealth.Health <= 0f)
					continue;

				float distanceSquared = Vector3.DistanceSquared(
					m_componentBody.Position,
					body.Position);

				// Validar que esté estrictamente dentro de MountDetectionRange
				if (distanceSquared <= maxRangeSquared && distanceSquared < closestDistance)
				{
					closestDistance = distanceSquared;
					closestMount = mount;
				}
			}

			return closestMount;
		}

		/// <summary>
		/// Verifica si una entidad es una criatura montable según la lista definida.
		/// </summary>
		/// <param name="entity">La entidad a verificar.</param>
		/// <returns>True si es una criatura montable, false en caso contrario.</returns>
		private bool IsMountableCreature(Entity entity)
		{
			if (entity?.ValuesDictionary?.DatabaseObject == null)
				return false;

			return MountableCreatures.Contains(entity.ValuesDictionary.DatabaseObject.Name);
		}

		/// <summary>
		/// Fuerza el desmonte de la criatura actual.
		/// </summary>
		public void ForceDismount()
		{
			if (CurrentMountState == MountState.Mounted && m_componentRider != null)
			{
				m_componentRider.StartDismounting();
				CurrentMountState = MountState.Dismounting;
			}
		}

		/// <summary>
		/// Detiene el movimiento de la montura.
		/// </summary>
		private void StopMount()
		{
			if (m_componentRider == null || m_componentRider.Mount == null)
				return;

			// Usar el pathfinding de la montura para ordenar la detención del movimiento
			ComponentPathfinding mountPathfinding = m_componentRider.Mount.Entity.FindComponent<ComponentPathfinding>();
			if (mountPathfinding != null)
			{
				mountPathfinding.Stop();
			}

			ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steedBehavior != null)
			{
				// Forzar la detención inmediata
				steedBehavior.m_speedLevel = 1;
				steedBehavior.m_speedChangeFactor = 100f;

				steedBehavior.SpeedOrder = 0;
				steedBehavior.TurnOrder = 0f;
				steedBehavior.JumpOrder = 0f;
			}
		}

		/// <summary>
		/// Pilotea la montura hacia el objetivo.
		/// </summary>
		private void PilotMount(ComponentCreature target)
		{
			if (m_componentRider == null || m_componentRider.Mount == null)
				return;

			ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steedBehavior == null)
				return;

			ComponentBody mountBody = m_componentRider.Mount.ComponentBody;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 myPos = mountBody.Position;

			Vector3 dirToTarget = targetPos - myPos;
			dirToTarget.Y = 0f;

			if (dirToTarget.LengthSquared() < 0.01f)
			{
				steedBehavior.TurnOrder = 0f;
				steedBehavior.SpeedOrder = 0;
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

			// Producto cruzado para saber si el target está a la izquierda (-) o derecha (+)
			float cross = forward.X * dirToTarget.Z - forward.Z * dirToTarget.X;
			// Producto punto para saber si estamos mirando hacia el target
			float dot = Vector3.Dot(forward, dirToTarget);

			// Enviar orden de giro (Clamp entre -0.5 y 0.5)
			steedBehavior.TurnOrder = MathUtils.Clamp(cross * 2f, -0.5f, 0.5f);

			float distance = Vector3.Distance(new Vector3(myPos.X, 0, myPos.Z), new Vector3(targetPos.X, 0, targetPos.Z));

			// Lógica de avance
			if (distance > 2f)
			{
				if (dot > 0.2f)
				{
					steedBehavior.SpeedOrder = 1; // Avanzar
				}
				else if (dot < -0.5f)
				{
					steedBehavior.SpeedOrder = -1; // Retroceder
				}
				else
				{
					steedBehavior.SpeedOrder = 0; // Solo girar
				}
			}
			else
			{
				steedBehavior.SpeedOrder = 0; // Ya estamos cerca, frenar
			}

			steedBehavior.JumpOrder = 0f;
		}

		#endregion

		#region Clothing

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

		#endregion

		#region Throwable Attacks

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

			// Si está montado, no verificar pathfinding propio (la montura se maneja con PilotMount)
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
				if (!UsesNormalAimAnimation())
				{
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				}
				AimTimeTimer -= m_subsystemTime.GameTimeDelta;
			}
			else
			{
				m_componentMiner.Aim(aim, AimState.Completed);
				if (!UsesNormalAimAnimation())
				{
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				}
				CooldownTimer = ThrowableCooldown;
				AimTimeTimer = ThrowableAimTime;
			}
		}

		#endregion

		#region Close Range

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

		#endregion

		#region Ranged Attacks

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

		#endregion

		#region Weapon Loading

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

		#endregion

		#region Aiming and Firing

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
				if (!UsesNormalAimAnimation())
				{
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				}
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
					if (!UsesNormalAimAnimation())
					{
						m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
					}
				}

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
				if (!UsesNormalAimAnimation())
				{
					m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				}
			}
		}

		private void CancelAiming()
		{
			AimTimeTimer = MusketAimTime;
			CooldownTimer = 0f;
			Ray3 emptyAim = new Ray3(Vector3.Zero, Vector3.UnitZ);
			m_componentMiner.Aim(emptyAim, AimState.Cancelled);
			m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
		}

		#endregion

		#region Utility

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

		#endregion
	}
}
