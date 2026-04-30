using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public enum AttackType
	{
		Default,
		CleanHands,
		Remote
	}

	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		public ComponentCreature Target
		{
			get { return m_target; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return m_importanceLevel; }
		}

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (componentCreature != null && IsPlayerHerd() && componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
				return;

			if (componentCreature != null && m_componentNewHerdBehavior != null)
			{
				ComponentNewHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.HerdName == m_componentNewHerdBehavior.HerdName)
					return;
			}

			if (Suppressed)
				return;

			m_target = componentCreature;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = (isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent);
			m_autoChaseSuppressionTime = 0f;
			m_isAiming = false;
			m_aimingTimer = 0f;
			m_cooldownTimer = 0f;

			IsActive = true;
		}

		private bool IsPlayerHerd()
		{
			return m_componentNewHerdBehavior != null && m_componentNewHerdBehavior.HerdName == "player";
		}

		public virtual void StopAttack()
		{
			m_stateMachine.TransitionTo("LookingForTarget");
			IsActive = false;
			m_target = null;
			m_nextUpdateTime = 0.0;
			m_range = 0f;
			m_chaseTime = 0f;
			m_isPersistent = false;
			m_importanceLevel = 0f;
			m_isAiming = false;
		}

		public virtual void Update(float dt)
		{
			if (Suppressed)
				StopAttack();

			m_autoChaseSuppressionTime -= dt;

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;
				m_componentCreature.ComponentCreatureModel.LookAtOrder = m_target.ComponentCreatureModel.EyePosition;

				// Nueva verificación de línea de visión (se usa varias veces en el frame)
				bool targetVisible = IsTargetVisible(m_target);

				// ----- INICIO: Lógica de cambio inmediato de arma según distancia -----
				// Solo se aplica en AttackType.Default y cuando el objetivo es visible
				if (m_attackType == AttackType.Default)
				{
					float dist = Vector3.Distance(
						m_componentCreature.ComponentBody.Position,
						m_target.ComponentBody.Position);

					bool hasThrowable = HasThrowableWeaponInInventory();
					// Si hay arma lanzable, se usa su rango; si no, se recurre al arma de fuego.
					float meleeRange = m_rangedAttackRange.X; // rango cuerpo a cuerpo se toma del inicio del rango a distancia
					float preferredMax = hasThrowable ? m_throwableAttackRange.Y : m_rangedAttackRange.Y;
					float preferredMin = hasThrowable ? m_throwableAttackRange.X : m_rangedAttackRange.X;

					bool isInMeleeRange = dist < meleeRange;
					bool isInPreferredRange = !isInMeleeRange && dist >= preferredMin && dist <= preferredMax;

					if (targetVisible)
					{
						if (isInMeleeRange)
						{
							// Cancelar cualquier apunte o enfriamiento activo
							if (m_isAiming)
								CancelAiming();
							if (m_cooldownTimer > 0f)
								m_cooldownTimer = 0f;
							// Cambiar a arma cuerpo a cuerpo si no tenemos ya una
							if (!IsHoldingMeleeWeapon())
								TryEquipMeleeWeapon();
						}
						else if (isInPreferredRange)
						{
							if (!m_isAiming && m_cooldownTimer <= 0f)
							{
								if (hasThrowable && !IsHoldingThrowableWeapon())
								{
									CancelAiming();
									m_cooldownTimer = 0f;
									TryEquipThrowableWeapon();
								}
								else if (!hasThrowable && !IsHoldingRangedWeapon())
								{
									CancelAiming();
									m_cooldownTimer = 0f;
									TryEquipRangedWeapon();
								}
							}
						}
					}
				}
				// ----- FIN: Lógica de cambio inmediato de arma -----

				// Control de apunte con animación y detención del movimiento
				if (m_isAiming)
				{
					// Perdió la visión mientras apuntaba => cancelar
					if (!targetVisible || m_componentPathfinding.IsStuck)
					{
						CancelAiming();
					}
					else
					{
						m_aimingTimer -= dt;

						// Animación de apunte cada frame
						Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 aimDir = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - eyePos);
						Ray3 aimRay = new Ray3(eyePos, aimDir);
						m_componentMiner.Aim(aimRay, AimState.InProgress);

						// Al terminar el tiempo de apunte, disparar/lanzar
						if (m_aimingTimer <= 0f)
						{
							PerformRangedAttack();
							m_isAiming = false;
							// El cooldown se establece dentro de PerformRangedAttack según el arma
						}
					}
				}
				else if (m_cooldownTimer > 0f)
				{
					m_cooldownTimer -= dt;
				}

				bool useRanged = ShouldUseRangedAttack();

				if (useRanged)
				{
					// Solo iniciar apunte si el objetivo es visible
					if (!m_isAiming && m_cooldownTimer <= 0f && targetVisible && !m_componentPathfinding.IsStuck)
					{
						// Intentar equipar primero arma lanzable, luego arma de fuego
						bool throwableEquipped = TryEquipThrowableWeapon();
						bool rangedEquipped = throwableEquipped ? false : (IsHoldingRangedWeapon() || TryEquipRangedWeapon());

						if (throwableEquipped || rangedEquipped)
						{
							m_isAiming = true;
							if (throwableEquipped)
								m_aimingTimer = ThrowableAimingTime;
							else if (IsActiveCrossbow())
								m_aimingTimer = CrossbowAimingTime;
							else if (IsActiveBow())
								m_aimingTimer = BowAimingTime;
							else
								m_aimingTimer = MusketAimingTime;

							m_componentPathfinding.Stop();
							m_componentCreature.ComponentLocomotion.WalkOrder = null;

							// Si es ballesta o arco, tensar y colocar proyectil durante el apunte
							if (IsActiveCrossbow() && m_componentMiner.Inventory != null)
							{
								IInventory inv = m_componentMiner.Inventory;
								int slot = inv.ActiveSlotIndex;
								int value = inv.GetSlotValue(slot);
								int data = Terrain.ExtractData(value);
								data = CrossbowBlock.SetDraw(data, 15);
								ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
								if (arrowType == null)
								{
									ArrowBlock.ArrowType newArrow = m_crossbowArrowTypes[m_random.Int(0, m_crossbowArrowTypes.Length - 1)];
									data = CrossbowBlock.SetArrowType(data, newArrow);
								}
								int newValue = Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data);
								inv.RemoveSlotItems(slot, 1);
								inv.AddSlotItems(slot, newValue, 1);
							}
							if (IsActiveBow() && m_componentMiner.Inventory != null)
							{
								IInventory inv = m_componentMiner.Inventory;
								int slot = inv.ActiveSlotIndex;
								int value = inv.GetSlotValue(slot);
								int data = Terrain.ExtractData(value);
								data = BowBlock.SetDraw(data, 15);
								ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
								if (arrowType == null)
								{
									ArrowBlock.ArrowType newArrow = m_bowArrowTypes[m_random.Int(0, m_bowArrowTypes.Length - 1)];
									data = BowBlock.SetArrowType(data, newArrow);
								}
								int newValue = Terrain.MakeBlockValue(BowBlock.Index, 0, data);
								inv.RemoveSlotItems(slot, 1);
								inv.AddSlotItems(slot, newValue, 1);
							}
						}
					}
				}
				else
				{
					if (m_isAiming)
					{
						m_isAiming = false;
					}
					// Ataque cuerpo a cuerpo solo si el objetivo está visible y en rango
					if (IsTargetInAttackRange(m_target.ComponentBody) && targetVisible)
						m_componentCreatureModel.AttackOrder = true;
				}

				if (!useRanged && m_componentCreatureModel.IsAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
					if (hitBody != null)
					{
						float x = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, x);
						m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();

						// Invocar rayo si la habilidad está activada (50% de probabilidad)
						if (m_invokeLightningOnHit && m_target != null && m_random.Float(0f, 1f) < 0.5f)
						{
							m_subsystemSky.MakeLightningStrike(m_target.ComponentBody.Position, false);
						}

						// Explotar al golpear si la habilidad está activada (10% de probabilidad)
						// Valores fijos: presión 555f, no incendiaria, sonido de explosión activado
						if (m_explodeOnHit && m_target != null && m_random.Float(0f, 1f) < 0.1f)
						{
							Vector3 pos = m_target.ComponentBody.Position;
							int cellX = Terrain.ToCell(pos.X);
							int cellY = Terrain.ToCell(pos.Y);
							int cellZ = Terrain.ToCell(pos.Z);
							m_subsystemExplosions.AddExplosion(
								x: cellX,
								y: cellY,
								z: cellZ,
								pressure: 555f,
								isIncendiary: false,
								noExplosionSound: false
							);
						}
					}
				}
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		// ----- NUEVO MÉTODO DE VERIFICACIÓN DE LÍNEA DE VISIÓN -----
		/// <summary>
		/// Verifica si el objetivo está dentro de un cono de 45° horizontales y si no hay obstáculos
		/// (bloques o cuerpos) entre la criatura y el objetivo.
		/// </summary>
		private bool IsTargetVisible(ComponentCreature target)
		{
			if (target == null) return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEyePos = target.ComponentCreatureModel.EyePosition;
			Vector3 dirToTarget = targetEyePos - eyePos;
			float distSq = dirToTarget.LengthSquared();
			if (distSq < 0.01f) return true;
			float dist = MathF.Sqrt(distSq);

			// Comprobación del ángulo horizontal (45°)
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			float flatForwardLen = new Vector2(forward.X, forward.Z).Length();
			if (flatForwardLen < 0.001f) return false; // sin dirección horizontal válida

			Vector2 flatDir = new Vector2(dirToTarget.X, dirToTarget.Z);
			float flatDist = flatDir.Length();
			if (flatDist < 0.001f) return true; // objetivo justo encima/debajo, se permite si está muy cerca

			float dot = Vector2.Dot(
				new Vector2(forward.X, forward.Z) / flatForwardLen,
				flatDir / flatDist);
			if (dot < MathF.Cos(0.785398f)) // cos(45°) ≈ 0.7071068
				return false;

			// Raycast de terreno para detectar bloques
			TerrainRaycastResult? terrainResult = m_subsystemTerrain.Raycast(eyePos, targetEyePos, false, true, null);
			if (terrainResult != null && terrainResult.Value.Distance < dist - 0.01f)
			{
				return false;
			}

			// Raycast de cuerpos para detectar otras criaturas
			Ray3 ray = new Ray3(eyePos, dirToTarget / dist);
			BodyRaycastResult? bodyResult = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyResult != null && bodyResult.Value.Distance < dist - 0.01f)
			{
				ComponentBody hitBody = bodyResult.Value.ComponentBody;
				// Si no es el objetivo ni está emparentado con él, bloquea la visión
				if (hitBody != target.ComponentBody &&
					!hitBody.IsChildOfBody(target.ComponentBody) &&
					!target.ComponentBody.IsChildOfBody(hitBody))
				{
					return false;
				}
			}

			return true;
		}
		// ----- FIN DEL MÉTODO DE VISIÓN -----

		// ----- MÉTODO AUXILIAR PARA CANCELAR EL APUNTADO (sin restaurar armas) -----
		private void CancelAiming()
		{
			if (!m_isAiming) return;
			m_isAiming = false;
			m_aimingTimer = 0f;
			m_cooldownTimer = 0f;
		}
		// ----- FIN DEL MÉTODO CANCELAR APUNTADO -----

		// ----- NUEVOS MÉTODOS PARA ARMAS LANZABLES -----
		/// <summary>
		/// Determina si un bloque es un arma lanzable (ej. lanza, bomba).
		/// Se considera lanzable si es apuntable y no es mosquete, ballesta ni arco.
		/// </summary>
		private bool IsThrowableItem(int contents)
		{
			Block block = BlocksManager.Blocks[contents];
			return block != null && block.IsAimable_(0) && contents != MusketBlock.Index && contents != CrossbowBlock.Index && contents != BowBlock.Index;
		}

		private bool IsHoldingThrowableWeapon()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			int activeValue = m_componentMiner.ActiveBlockValue;
			return IsThrowableItem(Terrain.ExtractContents(activeValue));
		}

		private bool HasThrowableWeaponInInventory()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			IInventory inventory = m_componentMiner.Inventory;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && IsThrowableItem(Terrain.ExtractContents(slotValue)))
					return true;
			}
			return false;
		}

		private bool TryEquipThrowableWeapon()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			IInventory inventory = m_componentMiner.Inventory;
			// Si ya lo tiene equipado, éxito
			if (IsHoldingThrowableWeapon())
				return true;
			// Buscar y equipar el primer arma lanzable del inventario
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) > 0 && IsThrowableItem(Terrain.ExtractContents(slotValue)))
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private bool IsHoldingMeleeWeapon()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			int activeValue = m_componentMiner.ActiveBlockValue;
			int contents = Terrain.ExtractContents(activeValue);
			// No es arma a distancia ni lanzable -> consideramos cuerpo a cuerpo
			return contents != MusketBlock.Index && contents != CrossbowBlock.Index && contents != BowBlock.Index && !IsThrowableItem(contents);
		}
		// ----- FIN DE MÉTODOS PARA ARMAS LANZABLES -----

		// ----- MÉTODOS AUXILIARES GENÉRICOS PARA ARMAS A DISTANCIA -----
		private bool IsActiveCrossbow()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			int activeValue = m_componentMiner.ActiveBlockValue;
			return Terrain.ExtractContents(activeValue) == CrossbowBlock.Index;
		}

		private bool IsActiveMusket()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			int activeValue = m_componentMiner.ActiveBlockValue;
			return Terrain.ExtractContents(activeValue) == MusketBlock.Index;
		}

		private bool IsHoldingRangedWeapon()
		{
			return IsActiveMusket() || IsActiveCrossbow() || IsActiveBow();
		}

		private bool TryEquipRangedWeapon()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			IInventory inventory = m_componentMiner.Inventory;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) <= 0)
					continue;
				int contents = Terrain.ExtractContents(slotValue);
				if (contents == MusketBlock.Index || contents == CrossbowBlock.Index || contents == BowBlock.Index)
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private bool TryEquipMeleeWeapon()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;

			IInventory inventory = m_componentMiner.Inventory;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (inventory.GetSlotCount(i) <= 0)
					continue;

				int contents = Terrain.ExtractContents(slotValue);

				// Excluir explícitamente todas las armas a distancia y lanzables
				if (contents == MusketBlock.Index || contents == CrossbowBlock.Index || contents == BowBlock.Index || IsThrowableItem(contents))
					continue;

				Block block = BlocksManager.Blocks[contents];
				if (block.GetMeleePower(slotValue) > 0f)
				{
					inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		private bool IsActiveBow()
		{
			if (m_componentMiner == null || m_componentMiner.Inventory == null)
				return false;
			int activeValue = m_componentMiner.ActiveBlockValue;
			return Terrain.ExtractContents(activeValue) == BowBlock.Index;
		}
		// ----- FIN DE MÉTODOS AUXILIARES -----

		private bool ShouldUseRangedAttack()
		{
			if (m_attackType == AttackType.CleanHands)
				return false;
			if (m_attackType == AttackType.Remote)
				return true;
			if (m_target == null)
				return false;

			float dist = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_target.ComponentBody.Position);

			// Prioridad: si hay arma lanzable, usar su rango; si no, el de armas de fuego.
			if (HasThrowableWeaponInInventory())
				return dist >= m_throwableAttackRange.X && dist <= m_throwableAttackRange.Y;
			else
				return dist >= m_rangedAttackRange.X && dist <= m_rangedAttackRange.Y;
		}

		private void TripleShot()
		{
			// Dispara MusketBall, Buckshot (8 bolas) y BuckshotBall simultáneamente
			SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>();
			if (subsystemProjectiles == null)
				return;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 aimDir = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - eyePos);
			Vector3 vector = eyePos + m_componentCreature.ComponentBody.Matrix.Right * 0.3f - m_componentCreature.ComponentBody.Matrix.Up * 0.2f;
			Vector3 vector2 = Vector3.Normalize(vector + aimDir * 10f - vector);
			Vector3 vector3 = Vector3.Normalize(Vector3.Cross(vector2, Vector3.UnitY));
			Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector2, vector3));

			ComponentCreature owner = m_componentCreature;

			// 1. MusketBall
			int musketBallValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall));
			Vector3 velocityMB = m_componentCreature.ComponentBody.Velocity + 120f * vector2;
			Projectile projectileMB = subsystemProjectiles.FireProjectile(musketBallValue, vector, velocityMB, Vector3.Zero, owner);
			if (projectileMB != null) projectileMB.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;

			// 2. Buckshot (8 balas)
			int buckshotBallValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
			Vector3 zeroBuckshot = new Vector3(0.04f, 0.04f, 0.25f);
			for (int i = 0; i < 8; i++)
			{
				Vector3 v3 = m_random.Float(0f - zeroBuckshot.X, zeroBuckshot.X) * vector3 +
						   m_random.Float(0f - zeroBuckshot.Y, zeroBuckshot.Y) * v2 +
						   m_random.Float(0f - zeroBuckshot.Z, zeroBuckshot.Z) * vector2;
				Vector3 velocityBuck = m_componentCreature.ComponentBody.Velocity + 80f * (vector2 + v3);
				Projectile projectileBuck = subsystemProjectiles.FireProjectile(buckshotBallValue, vector, velocityBuck, Vector3.Zero, owner);
				if (projectileBuck != null) projectileBuck.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}

			// 3. BuckshotBall (una sola)
			int buckshotBallSingleValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
			Vector3 zeroBB = new Vector3(0.06f, 0.06f, 0f);
			Vector3 vBB = m_random.Float(0f - zeroBB.X, zeroBB.X) * vector3 + m_random.Float(0f - zeroBB.Y, zeroBB.Y) * v2;
			Vector3 velocityBB = m_componentCreature.ComponentBody.Velocity + 60f * (vector2 + vBB);
			Projectile projectileBB = subsystemProjectiles.FireProjectile(buckshotBallSingleValue, vector, velocityBB, Vector3.Zero, owner);
			if (projectileBB != null) projectileBB.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		// ----- ATAQUE A DISTANCIA UNIFICADO -----
		private void PerformRangedAttack()
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (inventory == null)
				return;

			int slotIndex = inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(slotIndex);
			int contents = Terrain.ExtractContents(slotValue);

			// Si es un arma lanzable, lanzar directamente y salir
			if (IsThrowableItem(contents))
			{
				Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 aimDir = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - eyePos);
				Ray3 aimRay = new Ray3(eyePos, aimDir);
				m_componentMiner.Aim(aimRay, AimState.Completed);
				m_cooldownTimer = ThrowableCooldownTime;
				return;
			}

			// A partir de aquí, manejo normal de armas de fuego
			if (contents != MusketBlock.Index && contents != CrossbowBlock.Index && contents != BowBlock.Index)
			{
				if (!TryEquipRangedWeapon())
					return;
				slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
				contents = Terrain.ExtractContents(slotValue);
			}

			// --- Mosquete ---
			if (contents == MusketBlock.Index)
			{
				if (m_random.Float() < 0.05f)
				{
					TripleShot();
					return;
				}

				int data = Terrain.ExtractData(slotValue);
				BulletBlock.BulletType bulletType;
				int rnd = m_random.Int(0, 2);
				switch (rnd)
				{
					case 0: bulletType = BulletBlock.BulletType.MusketBall; break;
					case 1: bulletType = BulletBlock.BulletType.Buckshot; break;
					default: bulletType = BulletBlock.BulletType.BuckshotBall; break;
				}

				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				if (loadState != MusketBlock.LoadState.Loaded || !MusketBlock.GetHammerState(data))
				{
					int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
					newData = MusketBlock.SetBulletType(newData, bulletType);
					newData = MusketBlock.SetHammerState(newData, true);
					int newValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, newData);
					inventory.RemoveSlotItems(slotIndex, 1);
					inventory.AddSlotItems(slotIndex, newValue, 1);
				}

				slotValue = inventory.GetSlotValue(inventory.ActiveSlotIndex);
				data = Terrain.ExtractData(slotValue);
				if (!MusketBlock.GetHammerState(data))
				{
					int hammerData = MusketBlock.SetHammerState(data, true);
					int hammerValue = Terrain.MakeBlockValue(MusketBlock.Index, 0, hammerData);
					inventory.RemoveSlotItems(inventory.ActiveSlotIndex, 1);
					inventory.AddSlotItems(inventory.ActiveSlotIndex, hammerValue, 1);
				}
			}
			// --- Ballesta ---
			else if (contents == CrossbowBlock.Index)
			{
				slotValue = inventory.GetSlotValue(slotIndex);
			}
			// --- Arco ---
			else if (contents == BowBlock.Index)
			{
				slotValue = inventory.GetSlotValue(slotIndex);
			}
			else
			{
				return;
			}

			SubsystemProjectiles subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>();
			int projectileCountBefore = (subsystemProjectiles != null) ? subsystemProjectiles.Projectiles.Count : 0;

			Vector3 eyePos2 = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 aimDir2 = Vector3.Normalize(m_target.ComponentCreatureModel.EyePosition - eyePos2);
			Ray3 aimRay2 = new Ray3(eyePos2, aimDir2);
			m_componentMiner.Aim(aimRay2, AimState.Completed);

			if ((contents == CrossbowBlock.Index || contents == BowBlock.Index) && subsystemProjectiles != null)
			{
				for (int i = projectileCountBefore; i < subsystemProjectiles.Projectiles.Count; i++)
				{
					Projectile projectile = subsystemProjectiles.Projectiles[i];
					if (projectile != null && !projectile.ToRemove)
					{
						projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
					}
				}
			}

			// Establecer cooldown según el arma de fuego
			if (IsActiveCrossbow())
				m_cooldownTimer = CrossbowCooldownTime;
			else if (IsActiveBow())
				m_cooldownTimer = BowCooldownTime;
			else
				m_cooldownTimer = MusketCooldownTime;
		}
		// ----- FIN DEL ATAQUE A DISTANCIA UNIFICADO -----

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true); // NUEVO
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);
			m_componentNewHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();

			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			// NUEVOS PARÁMETROS
			m_attackType = valuesDictionary.GetValue<AttackType>("AttackType", AttackType.Default);
			m_rangedAttackRange = valuesDictionary.GetValue<Vector2>("RangedAttackRange", new Vector2(8f, 25f));
			m_invokeLightningOnHit = valuesDictionary.GetValue<bool>("InvokeLightningOnHit", false);
			m_explodeOnHit = valuesDictionary.GetValue<bool>("ExplodeOnHit", false);

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;

						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !isPlayer && flag2))
						{
							Attack(componentCreature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
						}
					}
				}

				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody && body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool persistent = false;
					float range, time;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						range = 30f;
						time = 60f;
						persistent = true;
					}
					else
					{
						range = 7f;
						time = 7f;
					}
					range = ChaseRangeOnAttacked.GetValueOrDefault(range);
					time = ChaseTimeOnAttacked.GetValueOrDefault(time);
					persistent = ChasePersistentOnAttacked.GetValueOrDefault(persistent);
					Attack(attacker, range, time, persistent);
				}
			}));

			m_stateMachine.AddState("LookingForTarget", delegate
			{
				m_importanceLevel = 0f;
				m_target = null;
			}, delegate
			{
				if (IsActive)
				{
					m_stateMachine.TransitionTo("Chasing");
					return;
				}

				if (!Suppressed && m_autoChaseSuppressionTime <= 0f &&
					(m_target == null || ScoreTarget(m_target) <= 0f) &&
					m_componentCreature.ComponentHealth.Health > MinHealthToAttackActively)
				{
					m_range = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;
					m_range *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
					ComponentCreature creature = FindTarget();

					if (creature != null)
						m_targetInRangeTime += m_dt;
					else
						m_targetInRangeTime = 0f;

					if (m_targetInRangeTime > TargetInRangeTimeToChase)
					{
						bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
						float maxTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
						Attack(creature, maxRange, maxTime, !isDay);
					}
				}
			}, null);

			m_stateMachine.AddState("RandomMoving", delegate
			{
				m_componentPathfinding.SetDestination(
					m_componentCreature.ComponentBody.Position + new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f)),
					1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
					m_stateMachine.TransitionTo("Chasing");
				if (!IsActive)
					m_stateMachine.TransitionTo("LookingForTarget");
			}, delegate
			{
				m_componentPathfinding.Stop();
			});

			m_stateMachine.AddState("Chasing", delegate
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
				if (PlayIdleSoundWhenStartToChase)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				m_nextUpdateTime = 0.0;
			}, delegate
			{
				if (!IsActive)
					m_stateMachine.TransitionTo("LookingForTarget");
				else if (m_chaseTime <= 0f)
				{
					m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					m_importanceLevel = 0f;
				}
				else if (m_target == null)
					m_importanceLevel = 0f;
				else if (m_target.ComponentHealth.Health <= 0f)
				{
					if (m_componentFeedBehavior != null)
					{
						m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + (double)m_random.Float(1f, 3f), delegate
						{
							if (m_target != null)
								m_componentFeedBehavior.Feed(m_target.ComponentBody.Position);
						});
					}
					m_importanceLevel = 0f;
				}
				else if (!m_isPersistent && m_componentPathfinding.IsStuck)
					m_importanceLevel = 0f;
				else if (m_isPersistent && m_componentPathfinding.IsStuck)
					m_stateMachine.TransitionTo("RandomMoving");
				else
				{
					if (m_isAiming)
						return;
					if (ScoreTarget(m_target) <= 0f)
						m_targetUnsuitableTime += m_dt;
					else
						m_targetUnsuitableTime = 0f;

					if (m_targetUnsuitableTime > 3f)
						m_importanceLevel = 0f;
					else
					{
						int maxPos = 0;
						if (m_isPersistent)
							maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;

						BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox bb2 = m_target.ComponentBody.BoundingBox;
						Vector3 center1 = 0.5f * (bb1.Min + bb1.Max);
						Vector3 center2 = 0.5f * (bb2.Min + bb2.Max);
						float dist = Vector3.Distance(center1, center2);
						float predict = (dist < 4f) ? 0.2f : 0f;

						m_componentPathfinding.SetDestination(
							center2 + predict * dist * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPos, true, false, true, m_target.ComponentBody);

						if (m_isAiming)
							return;
						if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}, null);

			m_stateMachine.TransitionTo("LookingForTarget");
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						result = creature;
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			if (m_componentNewHerdBehavior != null)
			{
				ComponentNewHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.HerdName == m_componentNewHerdBehavior.HerdName)
					return 0f;
			}

			float score = 0f;
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool notWater = m_componentCreature.Category != CreatureCategory.WaterPredator && m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isTargetOrGameMode = componentCreature == Target || m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool matchesMask = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
			bool randomPass = componentCreature == Target || (matchesMask && MathUtils.Remainder(0.004999999888241291 * m_subsystemTime.GameTime + (double)((float)(GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)m_chaseNonPlayerProbability);

			if (m_componentNewHerdBehavior != null && m_componentNewHerdBehavior.HerdName == "player" && isPlayer)
			{
				score = 0f;
			}
			else if (componentCreature != m_componentCreature && ((!isPlayer && randomPass) || (isPlayer && isTargetOrGameMode)) &&
					 componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f &&
					 (notWater || IsTargetInWater(componentCreature.ComponentBody)))
			{
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (dist < m_range)
					score = m_range - dist;
			}
			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target))
				return true;
			BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bb2 = target.BoundingBox;
			Vector3 c1 = 0.5f * (bb1.Min + bb1.Max);
			Vector3 c2 = 0.5f * (bb2.Min + bb2.Max) - c1;
			float len = c2.Length();
			Vector3 dir = c2 / len;
			float width = 0.5f * (bb1.Max.X - bb1.Min.X + bb2.Max.X - bb2.Min.X);
			float height = 0.5f * (bb1.Max.Y - bb1.Min.Y + bb2.Max.Y - bb2.Min.Y);

			if (MathF.Abs(c2.Y) < height * 0.99f)
			{
				if (len < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (len < height + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bb2 = target.BoundingBox;
			Vector3 c1 = 0.5f * (bb1.Min + bb1.Max);
			Vector3 c2 = 0.5f * (bb2.Min + bb2.Max) - c1;
			float len = c2.Length();
			Vector3 dir = c2 / len;
			float width = 0.5f * (bb1.Max.X - bb1.Min.X + bb2.Max.X - bb2.Min.X);
			float height = 0.5f * (bb1.Max.Y - bb1.Min.Y + bb2.Max.Y - bb2.Min.Y);

			if (MathF.Abs(c2.Y) < height * 0.99f)
			{
				if (len < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (len < height + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 eye = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Ray3 ray = new Ray3(eye, Vector3.Normalize(targetCenter - eye));
			BodyRaycastResult? raycast = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (raycast != null && raycast.Value.Distance < MaxAttackRange &&
				(raycast.Value.ComponentBody == target || raycast.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(raycast.Value.ComponentBody) ||
				 (target.StandingOnBody == raycast.Value.ComponentBody && AllowAttackingStandingOnBody)))
			{
				hitPoint = raycast.Value.HitPoint();
				return raycast.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemExplosions m_subsystemExplosions; // NUEVO
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemTerrain m_subsystemTerrain;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public ComponentFactors m_componentFactors;
		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;
		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public float m_autoChaseSuppressionTime;
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public bool AttacksPlayer = true;
		public bool AttacksNonPlayerCreature = true;
		public float ChaseRangeOnTouch = 7f;
		public float ChaseTimeOnTouch = 7f;
		public float? ChaseRangeOnAttacked;
		public float? ChaseTimeOnAttacked;
		public bool? ChasePersistentOnAttacked;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;
		public ComponentNewHerdBehavior m_componentNewHerdBehavior;

		// NUEVOS CAMPOS
		public AttackType m_attackType = AttackType.Default;
		public Vector2 m_rangedAttackRange = new Vector2(8f, 25f);
		// Rango para armas lanzables (no se carga desde diccionario, se puede ajustar externamente)
		public Vector2 m_throwableAttackRange = new Vector2(5f, 15f);

		public float MusketAimingTime = 1.0f;
		public float MusketCooldownTime = 0.5f;
		public float CrossbowAimingTime = 1.5f;
		public float CrossbowCooldownTime = 0.02f;
		public float BowAimingTime = 1.5f;
		public float BowCooldownTime = 0.01f;

		// Tiempos para armas lanzables
		public float ThrowableAimingTime = 1.5f;
		public float ThrowableCooldownTime = 0.01f;

		// Nueva habilidad: invocar rayo al golpear
		public bool m_invokeLightningOnHit = false;

		// Nueva habilidad: explotar al golpear (solo se carga el bool, los detalles van fijos en código)
		public bool m_explodeOnHit = false;

		private bool m_isAiming;
		private float m_aimingTimer;
		private float m_cooldownTimer;

		// Tipos de virote soportados por la ballesta
		private static ArrowBlock.ArrowType[] m_crossbowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Tipos de flecha soportados por el arco
		private static ArrowBlock.ArrowType[] m_bowArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.CopperArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.FireArrow
		};
	}
}
