using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{

	public class ComponentDefensiveCreatureAI : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		/// Lista de criaturas montables que la IA puede usar.
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
		/// Distancia máxima para detectar y montar una criatura.
		public const float MountDetectionRange = 2.5f;

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
		public enum MountState
		{
			None,
			Searching,
			Mounting,
			Mounted,
			Dismounting
		}
		/// Indica si la IA puede montarse en criaturas montables.
		public bool CanItBeMounted { get; private set; }
		/// Estado actual de montado de la IA.
		public MountState CurrentMountState { get; private set; } = MountState.None;

		public bool CanWearClothing { get; set; } = false;

		public Vector2 AttackDistanceRange = new Vector2(5f, 100f);
		public Vector2 ThrowableObjectThrowingDistance = new Vector2(5f, 15f);

		// Distancia de seguridad para uso de virotes explosivos
		public Vector2 SafetyDistanceUseOfExplosiveBolt = new Vector2(20f, 100f);

		// Tiempos del Mosquete Mejorado
		public float ImprovedMusketCooldown = 0.01f;
		public float ImprovedMusketAimTime = 1.5f;

		// Tiempos del Mosquete
		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		// Tiempos del Lanzallamas
		public float FlameThrowerCooldown = 0.01f;
		public float FlameThrowerAimTime = 1.5f;

		// Tiempos de la Ballesta
		public float CrossbowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;

		// Tiempos de la Ballesta Repetidora
		public float RepeatCrossbowCooldown = 0.01f;
		public float RepeatCrossbowAimTime = 1.5f;

		// Tiempos del Arco
		public float BowCooldown = 0.01f;
		public float BowAimTime = 1.5f;

		// Tiempos de objetos lanzables
		public float ThrowableAimTime = 1.5f;
		public float ThrowableCooldown = 0.01f;

		private static readonly HashSet<string> m_noArmMovementCreatures = new HashSet<string>
		{
			"InfectedNormalTamed1",
			"InfectedNormalTamed2",
			"InfectedMuscleTamed1",
			"InfectedMuscleTamed2"
		};

		private bool m_canUseInventory;
		private float m_aimTimer;
		private float m_cooldownTimer;
		private bool m_isAiming;
		private bool m_isThrowing;
		private float m_equipTimer;
		private bool m_isEquipping;
		private int m_equipSlot;
		private int m_equipValue;
		private ComponentCreatureClothing m_componentCreatureClothing;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentPathfinding m_componentPathfinding;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;

		// Componentes para montura
		private ComponentRider m_componentRider;
		private ComponentMount m_currentMount;

		private Random m_random;
		private DynamicArray<ComponentBody> m_nearbyBodies = new DynamicArray<ComponentBody>();

		/// <summary>
		/// Verifica si la IA está actualmente montada en una criatura.
		/// </summary>
		public bool IsMounted => CurrentMountState == MountState.Mounted;

		/// <summary>
		/// Obtiene la montura actual si está montado.
		/// </summary>
		public ComponentMount CurrentMount => m_currentMount;

		private bool ShouldSkipArmMovementForRanged()
		{
			if (Entity?.ValuesDictionary?.DatabaseObject != null)
			{
				return m_noArmMovementCreatures.Contains(Entity.ValuesDictionary.DatabaseObject.Name);
			}
			return false;
		}

		private void ApplyNoArmMovementAimSettings(bool isBow, bool isCrossbow, bool isFlameThrower)
		{
			m_componentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;

			if (isBow)
			{
				m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(0f, 0f, 0f);
				m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);
			}
			else if (isFlameThrower)
			{
				m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
				m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
			}
			else if (isCrossbow)
			{
				m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
				m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
			}
			else
			{
				m_componentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
				m_componentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");
			CanItBeMounted = valuesDictionary.GetValue<bool>("CanItBeMounted", false);
			CanWearClothing = valuesDictionary.GetValue<bool>("CanWearClothing", false);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();
			m_componentCreatureClothing = Entity.FindComponent<ComponentCreatureClothing>(false);
			m_componentRider = Entity.FindComponent<ComponentRider>(false);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			m_random = new Random();

			// Establecer estado inicial basado en si puede montar
			CurrentMountState = CanItBeMounted ? MountState.Searching : MountState.None;
		}

		public void Update(float dt)
		{
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

			if (!m_canUseInventory || m_componentMiner.Inventory == null) return;

			ComponentNewChaseBehavior chaseBehavior = m_componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior == null || chaseBehavior.Target == null || chaseBehavior.m_chaseTime <= 0f) return;

			ComponentCreature target = chaseBehavior.Target;
			if (target.ComponentHealth.Health <= 0f) return;

			bool isMounted = m_componentRider != null && m_componentRider.Mount != null;

			// NUEVO: Si está montado, el pathfinding del jinete no hace mover a la montura, así que lo detenemos
			if (isMounted && m_componentPathfinding != null)
			{
				m_componentPathfinding.Stop();
			}

			// NUEVO: Usar la posición real de la montura para calcular distancias si está montado
			Vector3 myPosition = isMounted ? m_componentRider.Mount.ComponentBody.Position : m_componentCreature.ComponentBody.Position;
			float distance = Vector3.Distance(myPosition, target.ComponentBody.Position);

			int throwableSlot = FindThrowableSlot();
			if (throwableSlot >= 0 && distance >= ThrowableObjectThrowingDistance.X && distance <= ThrowableObjectThrowingDistance.Y)
			{
				if (m_componentPathfinding != null && m_componentPathfinding.IsStuck)
				{
					if (m_isThrowing)
					{
						CancelAim();
						m_isThrowing = false;
						m_cooldownTimer = 0f;
					}
				}
				else
				{
					Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetPos = target.ComponentCreatureModel.EyePosition;

					bool isBehind = IsTargetBehind(target);
					bool hasLOS = HasClearLineOfSight(eyePos, targetPos, target);

					if (!isBehind && hasLOS)
					{
						HandleThrowableAttack(target, throwableSlot);
						if (isMounted) PilotMount(target); // NUEVO
						return;
					}
					else
					{
						if (m_isThrowing)
						{
							CancelAim();
							m_isThrowing = false;
						}

						// NUEVO: Manejar la montura en lugar del pathfinding
						if (isMounted) PilotMount(target);
						else MoveToGetClearLineOfSight(target);
						return;
					}
				}
			}

			if (m_isThrowing)
			{
				CancelAim();
				m_isThrowing = false;
				m_cooldownTimer = 0f;
			}

			if (distance <= AttackDistanceRange.Y)
			{
				if (distance < AttackDistanceRange.X)
				{
					int meleeSlot = FindMeleeWeaponSlot();
					if (meleeSlot >= 0)
					{
						CancelAim();
						m_componentMiner.Inventory.ActiveSlotIndex = meleeSlot;
						if (isMounted) StopMount(); // NUEVO: Frenar la montura al atacar cuerpo a cuerpo
					}
					else
					{
						HandleRangedAttack(target, distance);
						if (isMounted) PilotMount(target); // NUEVO
					}
				}
				else
				{
					HandleRangedAttack(target, distance);
					if (isMounted) PilotMount(target); // NUEVO
				}
			}
			else
			{
				CancelAim();

				// NUEVO: Si está montado y muy lejos, manejar la montura (ya que ComponentNewChaseBehavior no controla monturas)
				if (isMounted) PilotMount(target);
			}
		}

		// NUEVO: Método para detener la montura
		private void StopMount()
		{
			if (m_componentRider != null && m_componentRider.Mount != null)
			{
				ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
				if (steedBehavior != null)
				{
					steedBehavior.SpeedOrder = 0;
					steedBehavior.TurnOrder = 0f;
					steedBehavior.JumpOrder = 0f;
				}
			}
		}

		/// <summary>
		/// Actualiza el comportamiento de montura de la IA.
		/// </summary>
		private void UpdateMountingBehavior(float dt)
		{
			// Si no puede montar, no hacer nada
			if (!CanItBeMounted)
			{
				CurrentMountState = MountState.None;
				return;
			}

			// Si no tiene componente Rider, no puede montar
			if (m_componentRider == null)
			{
				CurrentMountState = MountState.None;
				return;
			}

			// Verificar estado actual
			switch (CurrentMountState)
			{
				case MountState.None:
					// Si puede montar pero está en None, cambiar a Searching
					CurrentMountState = MountState.Searching;
					break;

				case MountState.Searching:
					// Buscar una montura cercana
					ComponentMount nearestMount = FindNearestMountableCreature();
					if (nearestMount != null)
					{
						// Montarse inmediatamente
						m_componentRider.StartMounting(nearestMount);
						m_currentMount = nearestMount;
						CurrentMountState = MountState.Mounting;
					}
					break;

				case MountState.Mounting:
					// Esperar a que termine la animación de montar
					if (m_componentRider.Mount != null)
					{
						CurrentMountState = MountState.Mounted;
					}
					else
					{
						// Si por alguna razón no se montó, volver a buscar
						CurrentMountState = MountState.Searching;
					}
					break;

				case MountState.Mounted:
					// Verificar si todavía está montado
					if (m_componentRider.Mount == null)
					{
						m_currentMount = null;
						CurrentMountState = MountState.Searching;
					}
					break;

				case MountState.Dismounting:
					// Esperar a que termine de desmontarse
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
			Vector2 position = new Vector2(
				m_componentCreature.ComponentBody.Position.X,
				m_componentCreature.ComponentBody.Position.Z);

			m_nearbyBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(position, MountDetectionRange, m_nearbyBodies);

			float closestDistance = float.MaxValue;
			ComponentMount closestMount = null;

			foreach (ComponentBody body in m_nearbyBodies)
			{
				// No considerar el propio cuerpo
				if (body.Entity == Entity)
					continue;

				// Verificar si es una criatura montable
				if (!IsMountableCreature(body.Entity))
					continue;

				// Buscar el componente Mount
				ComponentMount mount = body.Entity.FindComponent<ComponentMount>();
				if (mount == null)
					continue;

				// Verificar que no tenga ya un jinete
				if (mount.Rider != null)
					continue;

				// Calcular distancia real (incluyendo Y)
				float distanceSquared = Vector3.DistanceSquared(
					m_componentCreature.ComponentBody.Position,
					body.Position);

				if (distanceSquared < closestDistance)
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
						{
							return i;
						}
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

		private bool HasClearLineOfSight(Vector3 from, Vector3 to, ComponentCreature target)
		{
			float dist = Vector3.Distance(from, to);
			if (dist < 0.1f) return true;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(from, to, false, true, null);
			if (terrainHit != null && terrainHit.Value.Distance < dist - 0.1f)
			{
				return false;
			}

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(from, to, 0.35f, delegate (ComponentBody b, float d)
			{
				return b.Entity != m_componentCreature.Entity &&
					   !b.IsChildOfBody(m_componentCreature.ComponentBody) &&
					   !m_componentCreature.ComponentBody.IsChildOfBody(b) &&
					   b.Entity != target.Entity &&
					   !target.ComponentBody.IsChildOfBody(b);
			});

			if (bodyHit != null && bodyHit.Value.Distance < dist - 0.1f)
			{
				return false;
			}

			return true;
		}

		private bool IsTargetBehind(ComponentCreature target)
		{
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			Vector3 toTarget = target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;

			toTarget.Y = 0f;
			forward.Y = 0f;

			if (forward.LengthSquared() < 0.001f || toTarget.LengthSquared() < 0.001f) return false;

			float dot = Vector3.Dot(Vector3.Normalize(forward), Vector3.Normalize(toTarget));

			return dot < 0f;
		}

		private void MoveToGetClearLineOfSight(ComponentCreature target)
		{
			if (m_componentPathfinding == null) return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 dirToTarget = Vector3.Normalize(targetPos - myPos);
			dirToTarget.Y = 0f;

			Vector3 sideDir = new Vector3(-dirToTarget.Z, 0f, dirToTarget.X);

			if (m_random.Bool(0.5f))
			{
				sideDir = -sideDir;
			}

			Vector3 moveDestination = myPos + sideDir * 3f;
			moveDestination.Y = targetPos.Y;

			m_componentPathfinding.SetDestination(moveDestination, 1f, 1f, 50, true, false, false, target.ComponentBody);
		}

		private void HandleThrowableAttack(ComponentCreature target, int throwableSlot)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			m_componentMiner.Inventory.ActiveSlotIndex = throwableSlot;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, direction);

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_isThrowing = true;
				m_aimTimer = 0f;
				m_componentMiner.Aim(aimRay, AimState.InProgress);
			}
			else
			{
				m_aimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (m_aimTimer >= ThrowableAimTime)
				{
					FireThrowable(aimRay);
					m_isAiming = false;
					m_isThrowing = false;
					m_cooldownTimer = ThrowableCooldown;
					m_aimTimer = 0f;
				}
			}
		}

		private void FireThrowable(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private void HandleRangedAttack(ComponentCreature target, float distance)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			int improvedMusketSlot = FindImprovedMusketSlot();
			int musketSlot = improvedMusketSlot >= 0 ? -1 : FindMusketSlot();
			int flameThrowerSlot = (improvedMusketSlot >= 0 || musketSlot >= 0) ? -1 : FindFlameThrowerSlot();
			int repeatCrossbowSlot = (improvedMusketSlot >= 0 || musketSlot >= 0 || flameThrowerSlot >= 0) ? -1 : FindRepeatCrossbowSlot();
			int crossbowSlot = (improvedMusketSlot >= 0 || musketSlot >= 0 || flameThrowerSlot >= 0 || repeatCrossbowSlot >= 0) ? -1 : FindCrossbowSlot();
			int bowSlot = (improvedMusketSlot >= 0 || musketSlot >= 0 || flameThrowerSlot >= 0 || repeatCrossbowSlot >= 0 || crossbowSlot >= 0) ? -1 : FindBowSlot();

			int activeSlot = improvedMusketSlot >= 0 ? improvedMusketSlot : (musketSlot >= 0 ? musketSlot : (flameThrowerSlot >= 0 ? flameThrowerSlot : (repeatCrossbowSlot >= 0 ? repeatCrossbowSlot : (crossbowSlot >= 0 ? crossbowSlot : bowSlot))));

			if (activeSlot < 0) return;

			m_componentMiner.Inventory.ActiveSlotIndex = activeSlot;

			bool isImprovedMusket = improvedMusketSlot >= 0;
			bool isFlameThrower = flameThrowerSlot >= 0;
			bool isRepeatCrossbow = repeatCrossbowSlot >= 0;
			bool isCrossbow = crossbowSlot >= 0;
			bool isBow = bowSlot >= 0;

			if (isImprovedMusket)
			{
				EnsureImprovedMusketLoaded(improvedMusketSlot);
			}
			else if (isFlameThrower)
			{
				EnsureFlameThrowerLoaded(flameThrowerSlot);
			}
			else if (isRepeatCrossbow)
			{
				EnsureRepeatCrossbowLoaded(repeatCrossbowSlot, distance);
			}
			else if (isCrossbow)
			{
				EnsureCrossbowLoaded(crossbowSlot, distance);
			}
			else if (isBow)
			{
				EnsureBowLoaded(bowSlot);
			}
			else
			{
				EnsureMusketLoaded(musketSlot);
			}

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetPos - eyePos);
			Ray3 aimRay = new Ray3(eyePos, direction);

			bool skipArmMovement = ShouldSkipArmMovementForRanged();

			if (!m_isAiming)
			{
				m_isAiming = true;
				m_aimTimer = 0f;
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (skipArmMovement)
				{
					ApplyNoArmMovementAimSettings(isBow, isCrossbow || isRepeatCrossbow || isImprovedMusket, isFlameThrower);
				}
			}
			else
			{
				m_aimTimer += m_subsystemTime.GameTimeDelta;
				m_componentMiner.Aim(aimRay, AimState.InProgress);

				if (skipArmMovement)
				{
					ApplyNoArmMovementAimSettings(isBow, isCrossbow || isRepeatCrossbow || isImprovedMusket, isFlameThrower);
				}

				float requiredAimTime;
				if (isImprovedMusket)
					requiredAimTime = ImprovedMusketAimTime;
				else if (isFlameThrower)
					requiredAimTime = FlameThrowerAimTime;
				else if (isBow)
					requiredAimTime = BowAimTime;
				else if (isCrossbow)
					requiredAimTime = CrossbowAimTime;
				else if (isRepeatCrossbow)
					requiredAimTime = RepeatCrossbowAimTime;
				else
					requiredAimTime = MusketAimTime;

				if (m_aimTimer >= requiredAimTime)
				{
					if (isImprovedMusket)
					{
						FireImprovedMusket(aimRay);
					}
					else if (isFlameThrower)
					{
						FireFlameThrower(aimRay);
					}
					else if (isRepeatCrossbow)
					{
						FireRepeatCrossbow(aimRay);
					}
					else if (isCrossbow)
					{
						FireCrossbow(aimRay);
					}
					else if (isBow)
					{
						FireBow(aimRay);
					}
					else
					{
						if (m_random.Float() < 0.05f)
						{
							FireBullet(BulletBlock.BulletType.MusketBall, aimRay);
							FireBullet(BulletBlock.BulletType.Buckshot, aimRay);
							FireBullet(BulletBlock.BulletType.BuckshotBall, aimRay);
						}
						else
						{
							BulletBlock.BulletType[] bulletTypes = new BulletBlock.BulletType[]
							{
								BulletBlock.BulletType.MusketBall,
								BulletBlock.BulletType.Buckshot,
								BulletBlock.BulletType.BuckshotBall
							};

							BulletBlock.BulletType selectedBullet = bulletTypes[m_random.Int(0, bulletTypes.Length - 1)];
							FireBullet(selectedBullet, aimRay);
						}
					}

					m_isAiming = false;

					if (isImprovedMusket)
						m_cooldownTimer = ImprovedMusketCooldown;
					else if (isFlameThrower)
						m_cooldownTimer = FlameThrowerCooldown;
					else if (isBow)
						m_cooldownTimer = BowCooldown;
					else if (isCrossbow)
						m_cooldownTimer = CrossbowCooldown;
					else if (isRepeatCrossbow)
						m_cooldownTimer = RepeatCrossbowCooldown;
					else
						m_cooldownTimer = MusketCooldown;

					m_aimTimer = 0f;
				}
			}
		}

		private void FireBow(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);

			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireCrossbow(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);

			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireRepeatCrossbow(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);

			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireImprovedMusket(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);

			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireFlameThrower(Ray3 aimRay)
		{
			m_componentMiner.Aim(aimRay, AimState.Completed);

			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireBullet(BulletBlock.BulletType bulletType, Ray3 aimRay)
		{
			int musketSlot = FindMusketSlot();
			if (musketSlot < 0) return;

			int value = m_componentMiner.Inventory.GetSlotValue(musketSlot);
			int data = Terrain.ExtractData(value);

			data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
			data = MusketBlock.SetBulletType(data, bulletType);

			m_componentMiner.Inventory.RemoveSlotItems(musketSlot, 1);
			m_componentMiner.Inventory.AddSlotItems(musketSlot, Terrain.MakeBlockValue(MusketBlock.Index, 0, data), 1);

			m_componentMiner.Aim(aimRay, AimState.Completed);
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
				m_isThrowing = false;
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

		private int FindThrowableSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0)
				{
					int value = m_componentMiner.Inventory.GetSlotValue(i);
					int blockId = Terrain.ExtractContents(value);

					if (blockId == MusketBlock.Index || blockId == ImprovedMusketBlock.Index ||
						blockId == BowBlock.Index || blockId == CrossbowBlock.Index ||
						blockId == RepeatCrossbowBlock.Index || blockId == FlameThrowerBlock.Index)
						continue;

					if (m_subsystemBlockBehaviors != null)
					{
						SubsystemBlockBehavior[] behaviors = m_subsystemBlockBehaviors.GetBlockBehaviors(blockId);
						if (behaviors != null)
						{
							for (int j = 0; j < behaviors.Length; j++)
							{
								if (behaviors[j] is SubsystemThrowableBlockBehavior)
								{
									return i;
								}
							}
						}
					}
				}
			}
			return -1;
		}

		private int FindImprovedMusketSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == ImprovedMusketBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindMusketSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == MusketBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindFlameThrowerSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == FlameThrowerBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindRepeatCrossbowSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == RepeatCrossbowBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindCrossbowSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == CrossbowBlock.Index)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindBowSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 &&
					Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == BowBlock.Index)
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

		private void EnsureImprovedMusketLoaded(int slotIndex)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int ammoCount = ImprovedMusketBlock.GetAmmoCount(data);

			if (ammoCount == 0)
			{
				data = ImprovedMusketBlock.SetAmmoCount(data, 2);

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(ImprovedMusketBlock.Index, 0, data), 1);
			}
		}

		private void EnsureFlameThrowerLoaded(int slotIndex)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
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

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, newData), 1);
			}
		}

		private void EnsureRepeatCrossbowLoaded(int slotIndex, float distanceToTarget)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatBoltType? boltType = RepeatCrossbowBlock.GetRepeatBoltType(data);
			int count = RepeatCrossbowBlock.GetCount(data);

			if (draw != 15 || boltType == null || count == 0)
			{
				RepeatBoltType selectedBolt;

				if (distanceToTarget <= SafetyDistanceUseOfExplosiveBolt.X)
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
				else if (distanceToTarget >= SafetyDistanceUseOfExplosiveBolt.Y)
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

				data = RepeatCrossbowBlock.SetDraw(data, 15);
				data = RepeatCrossbowBlock.SetRepeatBoltType(data, selectedBolt);

				data = RepeatCrossbowBlock.SetCount(data, 1);

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, data), 1);
			}
		}

		private void EnsureCrossbowLoaded(int slotIndex, float distanceToTarget)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int draw = CrossbowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);

			if (draw != 15 || arrowType == null)
			{
				ArrowBlock.ArrowType selectedBolt;

				if (distanceToTarget <= SafetyDistanceUseOfExplosiveBolt.X)
				{
					ArrowBlock.ArrowType[] normalBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt
					};
					selectedBolt = normalBolts[m_random.Int(0, normalBolts.Length - 1)];
				}
				else if (distanceToTarget >= SafetyDistanceUseOfExplosiveBolt.Y)
				{
					selectedBolt = ArrowBlock.ArrowType.ExplosiveBolt;
				}
				else
				{
					ArrowBlock.ArrowType[] allBolts = new ArrowBlock.ArrowType[]
					{
						ArrowBlock.ArrowType.IronBolt,
						ArrowBlock.ArrowType.DiamondBolt,
						ArrowBlock.ArrowType.ExplosiveBolt
					};
					selectedBolt = allBolts[m_random.Int(0, allBolts.Length - 1)];
				}

				data = CrossbowBlock.SetDraw(data, 15);
				data = CrossbowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(selectedBolt));

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data), 1);
			}
		}

		private void EnsureBowLoaded(int slotIndex)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int draw = BowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);

			if (draw != 15 || arrowType == null)
			{
				ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.CopperArrow,
					ArrowBlock.ArrowType.IronArrow,
					ArrowBlock.ArrowType.DiamondArrow,
					ArrowBlock.ArrowType.FireArrow
				};

				ArrowBlock.ArrowType selectedArrow = arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];

				data = BowBlock.SetDraw(data, 15);
				data = BowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(selectedArrow));

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BowBlock.Index, 0, data), 1);
			}
		}

		// NUEVO: Método para pilotar la montura hacia el objetivo
		private void PilotMount(ComponentCreature target)
		{
			if (m_componentRider == null || m_componentRider.Mount == null) return;

			ComponentSteedBehavior steedBehavior = m_componentRider.Mount.Entity.FindComponent<ComponentSteedBehavior>();
			if (steedBehavior == null) return;

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

			// CORRECCIÓN: Usar el método estático Vector3.Normalize() y reasignar la variable
			forward = Vector3.Normalize(forward);
			dirToTarget = Vector3.Normalize(dirToTarget);

			// Producto cruzado para saber si el target está a la izquierda (-) o derecha (+)
			float cross = forward.X * dirToTarget.Z - forward.Z * dirToTarget.X;
			// Producto punto para saber si estamos mirando hacia el target
			float dot = Vector3.Dot(forward, dirToTarget);

			// Enviar orden de giro (Clamp entre -0.5 y 0.5 porque así lo lee ComponentSteedBehavior)
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
					steedBehavior.SpeedOrder = -1; // Retroceder (raro, pero por si acaso)
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
	}
}
