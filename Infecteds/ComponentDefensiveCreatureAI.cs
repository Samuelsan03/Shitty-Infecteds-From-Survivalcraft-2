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
		public Vector2 ThrowableObjectThrowingDistance = new Vector2(5f, 15f);

		// Tiempos del Mosquete
		public float MusketCooldown = 0.01f;
		public float MusketAimTime = 1.5f;

		// Tiempos de la Ballesta
		public float CrossbowCooldown = 0.01f;
		public float CrossbowAimTime = 1.5f;

		// Tiempos del Arco
		public float BowCooldown = 0.01f;
		public float BowAimTime = 1.5f;

		// Tiempos de objetos lanzables
		public float ThrowableAimTime = 1.5f;
		public float ThrowableCooldown = 0.01f;

		private bool m_canUseInventory;
		private float m_aimTimer;
		private float m_cooldownTimer;
		private bool m_isAiming;
		private bool m_isThrowing;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentPathfinding m_componentPathfinding;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;

		private Random m_random;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_canUseInventory = valuesDictionary.GetValue<bool>("CanUseInventory");
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>();
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);

			m_random = new Random();
		}

		public void Update(float dt)
		{
			if (!m_canUseInventory || m_componentMiner.Inventory == null) return;

			ComponentNewChaseBehavior chaseBehavior = m_componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior == null || !chaseBehavior.IsActive || chaseBehavior.Target == null) return;

			ComponentCreature target = chaseBehavior.Target;
			if (target.ComponentHealth.Health <= 0f) return;

			float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);

			int throwableSlot = FindThrowableSlot();

			// Si estamos en el rango de las lanzables y tenemos lanzables, priorizar lanzables
			if (throwableSlot >= 0 && distance >= ThrowableObjectThrowingDistance.X && distance <= ThrowableObjectThrowingDistance.Y)
			{
				// Si estábamos apuntando con un arma a distancia, cancelar el arma a distancia primero
				if (m_isAiming && !m_isThrowing)
				{
					CancelAim();
					m_cooldownTimer = 0f;
				}

				// Detener el pathfinding al lanzar
				if (m_componentPathfinding != null)
				{
					m_componentPathfinding.Stop();
				}
				HandleThrowableAttack(target, throwableSlot);
				return;
			}

			// Si se sale del rango de las lanzables mientras se lanza, cancelar y pasar a armas a distancia inmediatamente
			if (m_isThrowing)
			{
				CancelAim();
				m_isThrowing = false;
				m_cooldownTimer = 0f;
			}

			// Si estamos fuera del rango de lanzables pero dentro del rango de armas a distancia, usar armas a distancia
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
			// Ejecutamos el Aim Completed para que el SubsystemThrowableBlockBehavior lo lance de forma normal
			m_componentMiner.Aim(aimRay, AimState.Completed);
		}

		private void HandleRangedAttack(ComponentCreature target)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= m_subsystemTime.GameTimeDelta;
				return;
			}

			// Prioridad de armas: Mosquete > Ballesta > Arco
			int musketSlot = FindMusketSlot();
			int crossbowSlot = musketSlot >= 0 ? -1 : FindCrossbowSlot();
			int bowSlot = (musketSlot >= 0 || crossbowSlot >= 0) ? -1 : FindBowSlot();

			int activeSlot = musketSlot >= 0 ? musketSlot : (crossbowSlot >= 0 ? crossbowSlot : bowSlot);

			if (activeSlot < 0) return;

			m_componentMiner.Inventory.ActiveSlotIndex = activeSlot;

			bool isCrossbow = crossbowSlot >= 0;
			bool isBow = bowSlot >= 0;

			if (isCrossbow)
			{
				EnsureCrossbowLoaded(crossbowSlot);
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

				// Evaluar el AimTime dependiendo del arma que se está usando
				float requiredAimTime = isBow ? BowAimTime : (isCrossbow ? CrossbowAimTime : MusketAimTime);

				if (m_aimTimer >= requiredAimTime)
				{
					if (isCrossbow)
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

					// Aplicar el Cooldown dependiendo del arma que se usó
					m_cooldownTimer = isBow ? BowCooldown : (isCrossbow ? CrossbowCooldown : MusketCooldown);

					m_aimTimer = 0f;
				}
			}
		}

		private void FireBow(Ray3 aimRay)
		{
			// 1. Ejecutamos el Aim Completed original para que el arco haga su cálculo de velocidad, sonidos y animaciones
			m_componentMiner.Aim(aimRay, AimState.Completed);

			// 2. Buscamos la flecha que acaba de ser disparada por esta criatura en la lista global
			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					// 3. Forzamos a que la flecha DESAPAREZCA al tocar el piso
					projectiles[i].ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					break;
				}
			}
		}

		private void FireCrossbow(Ray3 aimRay)
		{
			// 1. Ejecutamos el Aim Completed original para que la ballesta haga su lógica nativa, sonidos y animaciones
			m_componentMiner.Aim(aimRay, AimState.Completed);

			// 2. Buscamos el proyectil que acaba de ser disparado por esta criatura en la lista global
			ReadOnlyList<Projectile> projectiles = m_subsystemProjectiles.Projectiles;
			for (int i = projectiles.Count - 1; i >= 0; i--)
			{
				if (projectiles[i].Owner == m_componentCreature)
				{
					// 3. Forzamos a que el virote DESAPAREZCA al tocar el piso en lugar de convertirse en objeto
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

					// Excluir mosquete, arco y ballesta
					if (blockId == MusketBlock.Index || blockId == BowBlock.Index || blockId == CrossbowBlock.Index)
						continue;

					// Usar el SubsystemThrowableBlockBehavior para detectar si el bloque es lanzable
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

		private int FindCrossbowSlot()
		{
			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == CrossbowBlock.Index)
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
				if (m_componentMiner.Inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(m_componentMiner.Inventory.GetSlotValue(i)) == BowBlock.Index)
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

		private void EnsureCrossbowLoaded(int slotIndex)
		{
			int value = m_componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(value);
			int draw = CrossbowBlock.GetDraw(data);
			ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);

			if (draw != 15 || arrowType == null)
			{
				ArrowBlock.ArrowType[] boltTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.IronBolt,
					ArrowBlock.ArrowType.DiamondBolt,
					ArrowBlock.ArrowType.ExplosiveBolt
				};

				ArrowBlock.ArrowType selectedBolt = boltTypes[m_random.Int(0, boltTypes.Length - 1)];

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

			// Si no está completamente tenso (draw 15) o no tiene flecha asignada
			if (draw != 15 || arrowType == null)
			{
				// Tipos de flechas soportados según el SubsystemBowBlockBehavior original
				ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.CopperArrow,
					ArrowBlock.ArrowType.IronArrow,
					ArrowBlock.ArrowType.DiamondArrow,
					ArrowBlock.ArrowType.FireArrow
				};

				// Variación aleatoria de flechas
				ArrowBlock.ArrowType selectedArrow = arrowTypes[m_random.Int(0, arrowTypes.Length - 1)];

				// Tensar completamente y asignar la flecha
				data = BowBlock.SetDraw(data, 15);
				data = BowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(selectedArrow));

				m_componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				m_componentMiner.Inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BowBlock.Index, 0, data), 1);
			}
		}
	}
}
