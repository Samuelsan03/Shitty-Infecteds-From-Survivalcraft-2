using System;
using System.Reflection;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFirearmUser : Component, IUpdateable
	{
		public enum FirearmType
		{
			Automatic,
			SemiAutomatic,
			BoltAction
		}

		public enum ReloadState
		{
			Reloading,
			Reloaded,
			Healed
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public static class FirearmData
		{
			public struct Info
			{
				public string BlockName;
				public FirearmType Type;
				public int MaxBullets;
				public float AimTime;
				public float FireCooldown;
			}

			public static readonly Info[] Firearms = new Info[]
	{
        // Originales
        new Info { BlockName = "AK47Block",          Type = FirearmType.Automatic,      MaxBullets = 30,  AimTime = 1.0f, FireCooldown = 0.1f   },
		new Info { BlockName = "DesertEagleBlock",  Type = FirearmType.SemiAutomatic, MaxBullets = 7,   AimTime = 0.5f, FireCooldown = 0.02f  },
		new Info { BlockName = "SPAS12Block",       Type = FirearmType.SemiAutomatic, MaxBullets = 8,   AimTime = 1.2f, FireCooldown = 0.3f   },
		new Info { BlockName = "SniperBlock",       Type = FirearmType.BoltAction,     MaxBullets = 1,   AimTime = 2.0f, FireCooldown = 1.5f   },

        // Nuevos - escopetas de doble cañón
        new Info { BlockName = "BK93Block",          Type = FirearmType.SemiAutomatic, MaxBullets = 2,   AimTime = 1.0f, FireCooldown = 0.5f   },
		new Info { BlockName = "IZH43Block",         Type = FirearmType.SemiAutomatic, MaxBullets = 2,   AimTime = 1.0f, FireCooldown = 0.5f   },

        // Nuevos - revólver/pistola semi-automática
        new Info { BlockName = "RevolverBlock",      Type = FirearmType.SemiAutomatic, MaxBullets = 6,   AimTime = 0.5f, FireCooldown = 0.45f  },

        // Nuevos - subfusiles automáticos
        new Info { BlockName = "MP5SSDBlock",        Type = FirearmType.Automatic,     MaxBullets = 30,  AimTime = 0.8f, FireCooldown = 0.075f },
		new Info { BlockName = "M4Block",            Type = FirearmType.Automatic,     MaxBullets = 30,  AimTime = 0.8f, FireCooldown = 0.08f  },
		new Info { BlockName = "Mac10Block",         Type = FirearmType.Automatic,     MaxBullets = 30,  AimTime = 0.5f, FireCooldown = 0.075f },
		new Info { BlockName = "UziBlock",           Type = FirearmType.Automatic,     MaxBullets = 32,  AimTime = 0.6f, FireCooldown = 0.075f },

        // Nuevos - ametralladora ligera
        new Info { BlockName = "M249Block",          Type = FirearmType.Automatic,     MaxBullets = 100, AimTime = 1.5f, FireCooldown = 0.075f },

        // Nuevos - rifle bolt-action
        new Info { BlockName = "Master308Block",     Type = FirearmType.BoltAction,    MaxBullets = 5,   AimTime = 2.0f, FireCooldown = 1.8f   }
	};

			public static Info? Find(string blockName)
			{
				foreach (var info in Firearms)
				{
					if (info.BlockName == blockName) return info;
				}
				return null;
			}
		}

		// ================================================================
		// Campos
		// ================================================================
		private Vector2 m_shootDistance = new Vector2(5f, 100f);
		private SubsystemTime m_subsystemTime;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemParticles m_subsystemParticles;
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentHealth m_componentHealth;

		private ComponentBanditChaseBehavior m_chase1;
		private ComponentNewChaseBehavior m_chase2;
		private ComponentZombieChaseBehavior m_chase3;

		private ComponentCreature m_target;
		private double m_nextFireTime;
		private int m_firearmSlot = -1;
		private Random m_random = new Random();

		private float m_aimProgress;
		private int m_lastAimedSlot = -1;
		private ComponentCreature m_lastAimedTarget = null;

		private ReloadState m_reloadState = ReloadState.Reloaded;
		private double m_reloadEndTime;

		// Campos para la curación
		private float m_healProbability;
		private double m_nextHealCheckTime;

		// ================================================================
		// Load — Único diccionario: HealProbability
		// ================================================================
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);

			m_chase1 = Entity.FindComponent<ComponentBanditChaseBehavior>();
			m_chase2 = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_chase3 = Entity.FindComponent<ComponentZombieChaseBehavior>();

			// Único diccionario agregado: probabilidad de curación
			m_healProbability = valuesDictionary.GetValue<float>("HealProbability");
		}

		// ================================================================
		// Update
		// ================================================================
		public void Update(float dt)
		{
			if (m_componentHealth == null || m_componentHealth.Health <= 0f) return;

			// Verificar si se debe curar (solo cuando esté a punto de morir)
			CheckHeal();

			m_target = GetActiveTarget();
			if (m_target == null) return;

			if (m_target.ComponentHealth == null || m_target.ComponentHealth.Health <= 0f)
			{
				m_target = null;
				return;
			}

			DetectFirearm();
			if (m_firearmSlot < 0) return;

			float dist = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				m_target.ComponentBody.Position);

			if (dist <= m_shootDistance.X)
			{
				// Si tiene arma de cuerpo, la cambia y no hace más nada en este update.
				// Si NO tiene arma de cuerpo, continúa el flujo para seguir disparando.
				if (SwitchToMelee())
				{
					return;
				}
			}

			if (dist >= m_shootDistance.Y)
			{
				CancelChase();
				return;
			}

			FireAtTarget(dt);
		}

		// ================================================================
		// Verificación de curación
		// ================================================================
		private void CheckHeal()
		{
			// Si ya tiene vida completa, resetear estado y no hacer nada
			if (m_componentHealth.Health >= 1f)
			{
				if (m_reloadState == ReloadState.Healed)
				{
					m_reloadState = ReloadState.Reloaded;
				}
				return;
			}

			// Condición principal: Solo se cura si está a punto de morir (0.2 o menos)
			if (m_componentHealth.Health <= 0.2f && m_subsystemTime.GameTime >= m_nextHealCheckTime)
			{
				// La probabilidad se mantiene: si se cumple, se cura
				if (m_random.Float(0f, 1f) < m_healProbability)
				{
					// Curar al 100%
					m_componentHealth.Health = 1f;

					// Solo cambiar a Healed si no está recargando, para no romper la recarga
					if (m_reloadState != ReloadState.Reloading)
					{
						m_reloadState = ReloadState.Healed;
					}

					// Solo mostrar el texto "Healed", sin partículas de muerte ni sonidos
					Vector3 pos = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1.5f, 0f);
					m_subsystemParticles.AddParticleSystem(
						new RainbowTextParticleSystem(pos, Vector3.Zero, LanguageControl.Get("ComponentFirearmUser", 1)), false);

					// Enfriamiento largo después de curarse (10 segundos) para evitar spam
					m_nextHealCheckTime = m_subsystemTime.GameTime + 10.0;
				}
				else
				{
					// Si la probabilidad no se cumplió, esperar 1 segundo para volver a checar
					m_nextHealCheckTime = m_subsystemTime.GameTime + 1.0;
				}
			}
		}

		private ComponentCreature GetActiveTarget()
		{
			if (m_chase1 != null && m_chase1.IsActive && m_chase1.Target != null) return m_chase1.Target;
			if (m_chase2 != null && m_chase2.IsActive && m_chase2.Target != null) return m_chase2.Target;
			if (m_chase3 != null && m_chase3.IsActive && m_chase3.Target != null) return m_chase3.Target;
			return null;
		}

		private void DetectFirearm()
		{
			m_firearmSlot = -1;
			IInventory inv = m_componentMiner?.Inventory;
			if (inv == null) return;

			for (int i = 0; i < inv.SlotsCount; i++)
			{
				int val = inv.GetSlotValue(i);
				if (val == 0 || inv.GetSlotCount(i) == 0) continue;

				int contents = Terrain.ExtractContents(val);
				if (contents <= 0) continue;

				string name = BlocksManager.Blocks[contents].GetType().Name;
				if (FirearmData.Find(name).HasValue)
				{
					m_firearmSlot = i;
					return;
				}
			}
		}

		private bool HasAmmo(int slot)
		{
			IInventory inv = m_componentMiner.Inventory;
			int val = inv.GetSlotValue(slot);
			int data = Terrain.ExtractData(val);
			int contents = Terrain.ExtractContents(val);
			Type blockType = BlocksManager.Blocks[contents].GetType();

			MethodInfo getAmmo = blockType.GetMethod("GetAmmoCount", BindingFlags.Public | BindingFlags.Static);
			if (getAmmo != null)
			{
				int ammo = (int)getAmmo.Invoke(null, new object[] { data });
				return ammo > 0;
			}
			return false;
		}

		private void SetAmmoToMax(int slot, FirearmData.Info info)
		{
			IInventory inv = m_componentMiner.Inventory;
			int val = inv.GetSlotValue(slot);
			int data = Terrain.ExtractData(val);
			int contents = Terrain.ExtractContents(val);
			Type blockType = BlocksManager.Blocks[contents].GetType();

			MethodInfo setAmmo = blockType.GetMethod("SetAmmoCount", BindingFlags.Public | BindingFlags.Static);
			if (setAmmo != null)
			{
				data = (int)setAmmo.Invoke(null, new object[] { data, info.MaxBullets });
			}

			MethodInfo setLoadState = blockType.GetMethod("SetLoadState", BindingFlags.Public | BindingFlags.Static);
			if (setLoadState != null)
			{
				Type loadStateEnum = blockType.GetNestedType("LoadState");
				if (loadStateEnum != null)
				{
					object loadedState = Enum.Parse(loadStateEnum, "Loaded");
					data = (int)setLoadState.Invoke(null, new object[] { data, loadedState });
				}
			}

			int newVal = Terrain.MakeBlockValue(contents, 0, data);
			inv.RemoveSlotItems(slot, 1);
			inv.AddSlotItems(slot, newVal, 1);
		}

		private void FireAtTarget(float dt)
		{
			IInventory inv = m_componentMiner.Inventory;
			if (inv == null || m_target == null) return;

			if (inv.ActiveSlotIndex != m_firearmSlot)
				inv.ActiveSlotIndex = m_firearmSlot;

			int val = inv.GetSlotValue(m_firearmSlot);
			int contents = Terrain.ExtractContents(val);
			string name = BlocksManager.Blocks[contents].GetType().Name;
			FirearmData.Info? infoOpt = FirearmData.Find(name);
			if (!infoOpt.HasValue) return;
			FirearmData.Info info = infoOpt.Value;

			SubsystemBlockBehavior[] behaviors = m_subsystemBlockBehaviors.GetBlockBehaviors(contents);
			Vector3 eye = m_componentCreatureModel.EyePosition;
			Vector3 targetCenter = m_target.ComponentBody.BoundingBox.Center();
			Vector3 pos = m_componentCreature.ComponentBody.Position + new Vector3(0f, 1.5f, 0f);

			// 1. Manejar el estado de recarga
			if (m_reloadState == ReloadState.Reloading)
			{
				if (m_subsystemTime.GameTime >= m_reloadEndTime)
				{
					// Tiempo de recarga terminado
					m_reloadState = ReloadState.Reloaded;
					m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					SetAmmoToMax(m_firearmSlot, info);

					m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, pos, 0.5f), false);
					m_subsystemParticles.AddParticleSystem(new RainbowTextParticleSystem(pos, Vector3.Zero, LanguageControl.Get("ComponentFirearmUser", 3)), false);

					m_aimProgress = 0f;
				}
				else
				{
					// Aún está recargando, no hace nada más
					return;
				}
			}
			else if (m_reloadState == ReloadState.Healed)
			{
				// Si estaba curado, se resetea para poder disparar/recargar normal sin sonidos extras
				m_reloadState = ReloadState.Reloaded;
			}

			// 2. Verificar si se quedó sin munición
			if (!HasAmmo(m_firearmSlot))
			{
				m_reloadState = ReloadState.Reloading;
				m_reloadEndTime = m_subsystemTime.GameTime + 1;
				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);

				m_subsystemParticles.AddParticleSystem(new KillParticleSystem(m_subsystemTerrain, pos, 0.5f), false);
				m_subsystemParticles.AddParticleSystem(new RainbowTextParticleSystem(pos, Vector3.Zero, LanguageControl.Get("ComponentFirearmUser", 2)), false);

				Ray3 dummyAim = new Ray3(eye, m_componentCreature.ComponentBody.Matrix.Forward);
				for (int i = 0; i < behaviors.Length; i++)
				{
					behaviors[i].OnAim(dummyAim, m_componentMiner, AimState.Cancelled);
					break;
				}
				return;
			}

			// 3. Si tiene balas, proceder a apuntar y disparar
			if (m_firearmSlot != m_lastAimedSlot || m_target != m_lastAimedTarget)
			{
				m_aimProgress = 0f;
				m_lastAimedSlot = m_firearmSlot;
				m_lastAimedTarget = m_target;
			}

			m_aimProgress += dt;
			m_componentCreatureModel.LookAtOrder = new Vector3?(targetCenter);

			if (m_aimProgress >= info.AimTime && m_subsystemTime.GameTime >= m_nextFireTime)
			{
				Vector3 dir = Vector3.Normalize(targetCenter - eye);

				float spread = 0.04f;
				dir = Vector3.Normalize(dir + new Vector3(
					m_random.Float(-spread, spread),
					m_random.Float(-spread, spread),
					m_random.Float(-spread, spread)));

				Ray3 aim = new Ray3(eye, dir);

				for (int i = 0; i < behaviors.Length; i++)
				{
					if (info.Type == FirearmType.Automatic)
					{
						behaviors[i].OnAim(aim, m_componentMiner, AimState.InProgress);
					}
					else if (info.Type == FirearmType.SemiAutomatic)
					{
						behaviors[i].OnAim(aim, m_componentMiner, AimState.InProgress);
						behaviors[i].OnAim(aim, m_componentMiner, AimState.Cancelled);
					}
					else if (info.Type == FirearmType.BoltAction)
					{
						behaviors[i].OnAim(aim, m_componentMiner, AimState.InProgress);
						behaviors[i].OnAim(aim, m_componentMiner, AimState.Completed);
					}
					break;
				}

				m_nextFireTime = m_subsystemTime.GameTime + info.FireCooldown;

				if (info.Type != FirearmType.Automatic)
				{
					m_aimProgress = 0f;
				}
			}
		}

		// ================================================================
		// Cambiar a arma de cuerpo a cuerpo
		// Devuelve true si encontró y cambió a un arma de cuerpo.
		// Devuelve false si no tiene ninguna, para seguir disparando.
		// ================================================================
		private bool SwitchToMelee()
		{
			IInventory inv = m_componentMiner?.Inventory;
			if (inv == null) return false;

			for (int i = 0; i < inv.SlotsCount; i++)
			{
				int val = inv.GetSlotValue(i);
				if (val == 0 || inv.GetSlotCount(i) == 0) continue;

				int contents = Terrain.ExtractContents(val);
				string name = BlocksManager.Blocks[contents].GetType().Name;

				if (FirearmData.Find(name).HasValue) continue;

				Block block = BlocksManager.Blocks[contents];
				if (block.GetMeleePower(val) > 0f)
				{
					inv.ActiveSlotIndex = i;
					return true; // Encontró arma de cuerpo
				}
			}

			// No tiene arma de cuerpo
			return false;
		}

		private void CancelChase()
		{
			if (m_chase1 != null) m_chase1.StopAttack();
			if (m_chase2 != null) m_chase2.StopAttack();
			if (m_chase3 != null) m_chase3.StopAttack();
			m_target = null;
		}
	}
}
