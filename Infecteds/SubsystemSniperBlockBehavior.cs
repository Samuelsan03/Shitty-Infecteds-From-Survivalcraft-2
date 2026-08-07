using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Input;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemSniperBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => Array.Empty<int>();

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();

		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, Camera> m_savedCameras = new Dictionary<ComponentMiner, Camera>();

		private int m_bulletBlockIndex;
		private int m_sniperBlockIndex;
		private int m_sniperAmmunitionBlockIndex;

		private const int MaxAmmo = 1;
		private const float MuzzleOffset = 1.5f;
		private const float BulletVelocity = 250f;
		private const float NoiseRange = 80f;
		private const float NoiseLoudness = 2f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_sniperBlockIndex = BlocksManager.GetBlockIndex<SniperBlock>(false, false);
			m_sniperAmmunitionBlockIndex = BlocksManager.GetBlockIndex<SniperAmmunitionBlock>(false, false);
			base.Load(valuesDictionary);
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				if (activeSlotIndex >= 0)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int num = Terrain.ExtractContents(slotValue);
					int data = Terrain.ExtractData(slotValue);
					int num2 = slotValue;

					if (num == m_sniperBlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
						}

						float aimDuration = (float)(m_subsystemTime.GameTime - gameTime);

						SniperBlock.LoadState loadState = SniperBlock.GetLoadState(data);
						int ammoCount = SniperBlock.GetAmmoCount(data);
						ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;

						switch (state)
						{
							case AimState.InProgress:
								{
									// Fatiga por apuntar largo tiempo
									if (aimDuration >= 10f)
									{
										componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
										return true;
									}

									// Activar scope
									if (componentPlayer != null)
									{
										Camera currentCamera = componentPlayer.GameWidget.ActiveCamera;

										if (!(currentCamera is SniperScopeCamera))
										{
											m_savedCameras[componentMiner] = currentCamera;

											SniperScopeCamera scopeCamera = new SniperScopeCamera(componentPlayer.GameWidget);
											componentPlayer.GameWidget.ActiveCamera = scopeCamera;
											scopeCamera.Activate(currentCamera);
										}
									}

									// Solo mostrar contador de munición mientras apuntas
									if (componentPlayer != null)
									{
										componentPlayer.ComponentGui.DisplaySmallMessage($"{ammoCount}/{MaxAmmo}", Color.White, false, false);
									}

									// Posición del arma
									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									if (componentFirstPersonModel != null)
									{
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.3f, 0.1f, 0.15f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.6f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.1f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
									break;
								}
							case AimState.Cancelled:
								{
									// Canceló el aim sin disparar
									RestoreCamera(componentMiner, componentPlayer);

									m_aimStartTimes.Remove(componentMiner);
									m_savedCameras.Remove(componentMiner);
									break;
								}
							case AimState.Completed:
								{
									// BOLT-ACTION: Disparar al soltar el botón
									RestoreCamera(componentMiner, componentPlayer);

									if (loadState == SniperBlock.LoadState.Loaded && ammoCount > 0)
									{
										if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor <= 0.4f)
										{
											FireShot(aim, componentMiner, num, data, ref num2);
										}
									}
									else
									{
										// Sin munición - sonido y mensaje SOLO al intentar disparar (soltar)
										if (componentPlayer != null)
										{
											string ammoName = LanguageControl.GetBlock("SniperAmmunitionBlock", "DisplayName");
											string message = LanguageControl.Get("Firearms", 1);
											componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(message, ammoName), Color.White, true, false);
										}

										m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
									}

									m_aimStartTimes.Remove(componentMiner);
									m_savedCameras.Remove(componentMiner);
									break;
								}
						}
					}

					if (num2 != slotValue)
					{
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, num2, 1);
					}
				}
			}
			return false;
		}

		private void RestoreCamera(ComponentMiner componentMiner, ComponentPlayer componentPlayer)
		{
			if (componentPlayer != null && componentPlayer.GameWidget.ActiveCamera is SniperScopeCamera)
			{
				Camera savedCamera;
				if (m_savedCameras.TryGetValue(componentMiner, out savedCamera) && savedCamera != null)
				{
					componentPlayer.GameWidget.ActiveCamera = savedCamera;

					if (savedCamera is FppCamera fppCamera)
					{
						fppCamera.Activate(componentPlayer.GameWidget.ActiveCamera);
					}
				}
				else
				{
					componentPlayer.GameWidget.ActiveCamera = new FppCamera(componentPlayer.GameWidget);
				}
			}
		}

		private void FireShot(Ray3 aim, ComponentMiner componentMiner, int blockContents, int data, ref int newValue)
		{
			Vector3 muzzlePosition = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
				+ componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.35f
				- componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.15f
				+ aim.Direction * MuzzleOffset;

			Vector3 fireDirection = aim.Direction;

			int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0,
				FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.SniperBullet));

			Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + BulletVelocity * fireDirection;

			Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, muzzlePosition, velocity, Vector3.Zero, componentMiner.ComponentCreature);
			if (projectile != null)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}

			m_subsystemAudio.PlaySound("Audio/Armas/Sniper fuego", 1f, m_random.Float(-0.05f, 0.05f), muzzlePosition, 15f, true);

			m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, muzzlePosition, fireDirection), false);

			m_subsystemNoise.MakeNoise(muzzlePosition, NoiseLoudness, NoiseRange);

			int currentAmmo = SniperBlock.GetAmmoCount(data);
			int newAmmoCount = currentAmmo - 1;
			int newData = SniperBlock.SetAmmoCount(data, newAmmoCount);

			if (newAmmoCount <= 0)
			{
				newData = SniperBlock.SetLoadState(newData, SniperBlock.LoadState.Empty);
			}

			newValue = Terrain.MakeBlockValue(blockContents, 0, newData);
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotContents = Terrain.ExtractContents(inventory.GetSlotValue(slotIndex));

			if (slotContents != m_sniperBlockIndex) return 0;

			int ammoCount = SniperBlock.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_sniperAmmunitionBlockIndex)
				return 1;

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount == 1)
			{
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				int newData = SniperBlock.SetLoadState(data, SniperBlock.LoadState.Loaded);
				newData = SniperBlock.SetAmmoCount(newData, MaxAmmo);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_sniperBlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}
