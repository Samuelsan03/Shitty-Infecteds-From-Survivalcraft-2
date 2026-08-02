using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFlameThrowerBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { FlameThrowerBlock.Index };

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();

		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, double> m_lastFireTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, int> m_shotsFired = new Dictionary<ComponentMiner, int>();
		private Dictionary<ComponentMiner, bool> m_emptyMessageShown = new Dictionary<ComponentMiner, bool>();
		private Dictionary<ComponentMiner, FireFlameThrowerParticleSystem> m_fireParticleSystems = new Dictionary<ComponentMiner, FireFlameThrowerParticleSystem>();
		private Dictionary<ComponentMiner, PoisonFlameThrowerParticleSystem> m_poisonParticleSystems = new Dictionary<ComponentMiner, PoisonFlameThrowerParticleSystem>();

		private Dictionary<ComponentMiner, int> m_aimBulletTypes = new Dictionary<ComponentMiner, int>();
		private Dictionary<ComponentMiner, int> m_aimAmmo = new Dictionary<ComponentMiner, int>();
		private Dictionary<ComponentMiner, bool> m_aimSwitchOn = new Dictionary<ComponentMiner, bool>();
		private Dictionary<ComponentMiner, int> m_aimData = new Dictionary<ComponentMiner, int>();
		private Dictionary<ComponentMiner, int> m_aimDamageAccumulator = new Dictionary<ComponentMiner, int>();

		private int m_flameBulletBlockIndex;

		private const int BulletTypeMask = 0x300;
		private const int BulletTypeShift = 8;

		private int GetBulletType(int data) => (data >> BulletTypeShift) & 3;
		private int SetBulletType(int data, int type) => (data & ~BulletTypeMask) | ((type & 3) << BulletTypeShift);

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			if (componentPlayer.ComponentGui.ModalPanelWidget == null)
			{
				componentPlayer.ComponentGui.ModalPanelWidget = new FlameThrowerWidget(inventory, slotIndex);
			}
			else
			{
				componentPlayer.ComponentGui.ModalPanelWidget = null;
			}
			return true;
		}

		private void ClearAimState(ComponentMiner miner)
		{
			m_aimStartTimes.Remove(miner);
			m_lastFireTimes.Remove(miner);
			m_shotsFired.Remove(miner);
			m_emptyMessageShown.Remove(miner);
			m_aimBulletTypes.Remove(miner);
			m_aimAmmo.Remove(miner);
			m_aimSwitchOn.Remove(miner);
			m_aimData.Remove(miner);
			m_aimDamageAccumulator.Remove(miner);
		}

		private void ForceResetAnimations(ComponentMiner miner)
		{
			if (miner?.ComponentCreature?.ComponentCreatureModel != null)
			{
				miner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
				miner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
				miner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
			}
			ComponentFirstPersonModel fpm = miner?.Entity?.FindComponent<ComponentFirstPersonModel>();
			if (fpm != null)
			{
				fpm.ItemOffsetOrder = Vector3.Zero;
				fpm.ItemRotationOrder = Vector3.Zero;
			}

			ClearAimState(miner);
		}

		private void StopAndRemoveFireParticles(ComponentMiner miner)
		{
			if (m_fireParticleSystems.TryGetValue(miner, out var psFire))
			{
				psFire.IsStopped = true;
				m_fireParticleSystems.Remove(miner);
			}
		}

		private void StopAndRemovePoisonParticles(ComponentMiner miner)
		{
			if (m_poisonParticleSystems.TryGetValue(miner, out var psPoison))
			{
				psPoison.IsStopped = true;
				m_poisonParticleSystems.Remove(miner);
			}
		}

		private void StopAndRemoveAllParticles(ComponentMiner miner)
		{
			StopAndRemoveFireParticles(miner);
			StopAndRemovePoisonParticles(miner);
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlotIndex = inventory.ActiveSlotIndex;
			if (activeSlotIndex < 0) return false;

			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int slotCount = inventory.GetSlotCount(activeSlotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);

			if (contents != FlameThrowerBlock.Index || slotCount <= 0)
			{
				if (m_fireParticleSystems.ContainsKey(componentMiner) || m_poisonParticleSystems.ContainsKey(componentMiner))
				{
					StopAndRemoveAllParticles(componentMiner);
					ForceResetAnimations(componentMiner);
				}
				return false;
			}

			switch (state)
			{
				case AimState.InProgress:
					HandleAimInProgress(componentMiner, aim, data);

					// CORREGIDO: Actualizar el inventario inmediatamente cuando cambia el switch 
					// (exactamente igual a como lo hace el SubsystemMusketBlockBehavior original).
					// Esto permite que DrawBlock lea el estado y muestre la animación 3D de la palanca.
					// Como IsSwapAnimationNeeded ignora este bit, NO cortará la animación de apuntado.
					if (m_aimData.TryGetValue(componentMiner, out int updatedData) && updatedData != data)
					{
						int newValue = Terrain.MakeBlockValue(contents, 0, updatedData);
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, newValue, 1);
					}
					return false;

				case AimState.Cancelled:
				case AimState.Completed:
					return HandleAimCompleted(componentMiner, aim, state, contents, data, activeSlotIndex, inventory);

				default:
					return false;
			}
		}

		private bool HandleAimInProgress(ComponentMiner componentMiner, Ray3 aim, int currentData)
		{
			if (!m_aimStartTimes.ContainsKey(componentMiner))
			{
				m_aimStartTimes[componentMiner] = m_subsystemTime.GameTime;
				m_shotsFired[componentMiner] = 0;
				m_aimBulletTypes[componentMiner] = GetBulletType(currentData);
				m_aimAmmo[componentMiner] = FlameThrowerBlock.GetAmmoCount(currentData);
				m_aimSwitchOn[componentMiner] = FlameThrowerBlock.GetSwitchState(currentData);
				m_aimData[componentMiner] = currentData;
				m_aimDamageAccumulator[componentMiner] = 0;
			}

			int aimBulletType = m_aimBulletTypes[componentMiner];
			int ammo = m_aimAmmo[componentMiner];
			bool switchOn = m_aimSwitchOn[componentMiner];
			int aimData = m_aimData[componentMiner];

			double aimStartTime = m_aimStartTimes[componentMiner];
			float aimDuration = (float)(m_subsystemTime.GameTime - aimStartTime);

			float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) * new Vector3
			{
				X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
				Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
				Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
			};
			aim.Direction = Vector3.Normalize(aim.Direction + v);

			Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 fireDir = Vector3.Normalize(aim.Direction);
			Vector3 muzzlePos = eyePos + fireDir * 0.5f;

			if (aimBulletType == 0)
			{
				StopAndRemovePoisonParticles(componentMiner);
			}
			else
			{
				StopAndRemoveFireParticles(componentMiner);
			}

			if (ammo > 0 && switchOn)
			{
				if (aimBulletType == 0)
				{
					if (!m_fireParticleSystems.ContainsKey(componentMiner))
					{
						var particleSystem = new FireFlameThrowerParticleSystem(muzzlePos, fireDir, 0.2f, 20f);
						particleSystem.IsStopped = false;
						m_subsystemParticles.AddParticleSystem(particleSystem, false);
						m_fireParticleSystems[componentMiner] = particleSystem;
					}
					else
					{
						var ps = m_fireParticleSystems[componentMiner];
						ps.Position = muzzlePos;
						ps.Direction = fireDir;
					}
				}
				else
				{
					if (!m_poisonParticleSystems.ContainsKey(componentMiner))
					{
						var particleSystem = new PoisonFlameThrowerParticleSystem(muzzlePos, fireDir, 0.2f, 20f);
						particleSystem.IsStopped = false;
						m_subsystemParticles.AddParticleSystem(particleSystem, false);
						m_poisonParticleSystems[componentMiner] = particleSystem;
					}
					else
					{
						var ps = m_poisonParticleSystems[componentMiner];
						ps.Position = muzzlePos;
						ps.Direction = fireDir;
					}
				}
			}
			else
			{
				StopAndRemoveAllParticles(componentMiner);
			}

			if (aimDuration > 0.5f && !switchOn)
			{
				m_aimSwitchOn[componentMiner] = true;
				m_aimData[componentMiner] = FlameThrowerBlock.SetSwitchState(aimData, true);
				m_subsystemAudio.PlaySound("Audio/Hammer Cock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}

			if (ammo > 0 && m_aimSwitchOn[componentMiner])
			{
				if (!m_lastFireTimes.ContainsKey(componentMiner))
				{
					m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
				}
				double lastFireTime = m_lastFireTimes[componentMiner];
				float fireInterval = 0.2f;
				if (m_subsystemTime.GameTime - lastFireTime >= fireInterval)
				{
					if (TryFire(componentMiner, aim, aimBulletType))
					{
						m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
						m_shotsFired[componentMiner]++;
						m_aimDamageAccumulator[componentMiner]++;
					}
				}
			}

			ComponentFirstPersonModel firstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
			if (firstPersonModel != null)
			{
				ComponentPlayer player = componentMiner.ComponentPlayer;
				if (player != null)
					player.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);

				firstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
				firstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
			}

			componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
			componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
			componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

			return false;
		}

		private bool HandleAimCompleted(ComponentMiner componentMiner, Ray3 aim, AimState state, int contents, int inventoryData, int activeSlotIndex, IInventory inventory)
		{
			StopAndRemoveAllParticles(componentMiner);

			if (!m_aimData.ContainsKey(componentMiner))
			{
				return false;
			}

			int aimData = m_aimData[componentMiner];
			int aimBulletType = m_aimBulletTypes[componentMiner];
			int ammo = m_aimAmmo[componentMiner];
			int shots = m_shotsFired[componentMiner];
			int damageToApply = m_aimDamageAccumulator[componentMiner];
			bool switchOn = m_aimSwitchOn[componentMiner];
			bool wasEmptyShown = m_emptyMessageShown.ContainsKey(componentMiner) && m_emptyMessageShown[componentMiner];

			if (shots == 0 && ammo == 0 && !wasEmptyShown)
			{
				componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
					LanguageControl.Get("SubsystemFlameThrowerBlockBehavior", 0),
					Color.White, true, false);
			}

			int finalData = aimData;

			if (switchOn)
			{
				if (state == AimState.Completed)
				{
					m_subsystemAudio.PlaySound("Audio/Hammer Release Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
				}
				else
				{
					m_subsystemAudio.PlaySound("Audio/Hammer Uncock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
				}

				if (shots > 0 && ammo > 0)
				{
					int ammoToUse = Math.Min(shots, ammo);
					ammo -= ammoToUse;
					finalData = FlameThrowerBlock.SetAmmoCount(finalData, ammo);
					if (ammo == 0)
					{
						finalData = FlameThrowerBlock.SetLoadState(finalData, FlameThrowerBlock.LoadState.Empty);
					}
				}

				finalData = FlameThrowerBlock.SetSwitchState(finalData, false);
			}

			if (damageToApply > 0)
			{
				for (int i = 0; i < damageToApply; i++)
				{
					componentMiner.DamageActiveTool(1);
				}
			}

			if (finalData != inventoryData)
			{
				int newValue = Terrain.MakeBlockValue(contents, 0, finalData);
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}

			ForceResetAnimations(componentMiner);
			return false;
		}

		private bool TryFire(ComponentMiner componentMiner, Ray3 aim, int bulletType)
		{
			Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 fireDir = Vector3.Normalize(aim.Direction);
			Vector3 right = Vector3.Normalize(Vector3.Cross(fireDir, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(fireDir, right));

			int bulletValue = Terrain.MakeBlockValue(m_flameBulletBlockIndex, 0, FlameBulletBlock.SetBulletType(0, (FlameBulletBlock.FlameBulletType)bulletType));
			float speed = 120f;
			Vector3 spread = new Vector3(0.06f, 0.06f, 0f);

			Vector3 randomOffset = m_random.Float(-spread.X, spread.X) * right
								 + m_random.Float(-spread.Y, spread.Y) * up
								 + m_random.Float(-spread.Z, spread.Z) * fireDir;
			Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + speed * (fireDir + randomOffset);

			Projectile projectile = m_subsystemProjectiles.FireProjectile(
				bulletValue,
				eyePos,
				velocity,
				Vector3.Zero,
				componentMiner.ComponentCreature);

			if (projectile != null)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				projectile.AttackPower = BlocksManager.Blocks[m_flameBulletBlockIndex].GetProjectilePower(bulletValue);
			}

			string sound = bulletType == 0 ? "Audio/Flamethrower/Flamethrower Fire" : "Audio/Flamethrower/PoisonSmoke";
			m_subsystemAudio.PlaySound(sound, 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
			m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

			return true;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotValue = inventory.GetSlotValue(slotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			if (contents != FlameThrowerBlock.Index)
				return 0;

			int data = Terrain.ExtractData(slotValue);
			int ammo = FlameThrowerBlock.GetAmmoCount(data);

			if (ammo < 15 && Terrain.ExtractContents(value) == m_flameBulletBlockIndex)
				return 1;

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount == 1)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int data = Terrain.ExtractData(slotValue);
				int ammo = FlameThrowerBlock.GetAmmoCount(data);

				if (ammo < 15 && Terrain.ExtractContents(value) == m_flameBulletBlockIndex)
				{
					int bulletType = (int)FlameBulletBlock.GetBulletType(Terrain.ExtractData(value));
					int newData = data;
					newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetAmmoCount(newData, 15);
					newData = SetBulletType(newData, bulletType);

					inventory.RemoveSlotItems(slotIndex, 1);
					inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, newData), 1);

					processedValue = 0;
					processedCount = 0;
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
			base.Load(valuesDictionary);
		}
	}
}
