using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAK47BlockBehavior : SubsystemBlockBehavior
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
		private Dictionary<ComponentMiner, double> m_lastFireTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, double> m_lastEmptySoundTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, double> m_lastEmptyMessageTimes = new Dictionary<ComponentMiner, double>();
		private int m_bulletBlockIndex;
		private int m_ak47BlockIndex;
		private int m_ak47AmmunitionBlockIndex;

		// Constantes del AK-47
		private const float FireRate = 0.1f;
		private const int MaxAmmo = 30;
		private const float EmptySoundCooldown = 0.5f;
		private const float EmptyMessageCooldown = 0.5f;
		private const float MuzzleOffset = 0.8f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_ak47BlockIndex = BlocksManager.GetBlockIndex<AK47Block>(false, false);
			m_ak47AmmunitionBlockIndex = BlocksManager.GetBlockIndex<AK47AmmunitionBlock>(false, false);
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
					int num3 = 0;

					if (num == m_ak47BlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
							m_lastFireTimes[componentMiner] = gameTime;
							m_lastEmptySoundTimes[componentMiner] = gameTime - EmptySoundCooldown;
							m_lastEmptyMessageTimes[componentMiner] = gameTime - EmptyMessageCooldown;
						}
						float num4 = (float)(m_subsystemTime.GameTime - gameTime);

						double lastFireTime;
						m_lastFireTimes.TryGetValue(componentMiner, out lastFireTime);
						float timeSinceLastFire = (float)(m_subsystemTime.GameTime - lastFireTime);

						double lastEmptySoundTime;
						m_lastEmptySoundTimes.TryGetValue(componentMiner, out lastEmptySoundTime);
						float timeSinceEmptySound = (float)(m_subsystemTime.GameTime - lastEmptySoundTime);

						double lastEmptyMessageTime;
						m_lastEmptyMessageTimes.TryGetValue(componentMiner, out lastEmptyMessageTime);
						float timeSinceEmptyMessage = (float)(m_subsystemTime.GameTime - lastEmptyMessageTime);

						// Dispersión para arma automática (como el mosquete)
						float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
						Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.15f * MathUtils.Saturate(num4 / 5f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
						};
						aim.Direction = Vector3.Normalize(aim.Direction + v);

						switch (state)
						{
							case AimState.InProgress:
								{
									if (num4 >= 10f)
									{
										componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
										return true;
									}

									AK47Block.LoadState loadState = AK47Block.GetLoadState(data);
									int ammoCount = AK47Block.GetAmmoCount(data);

									ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;

									if (loadState == AK47Block.LoadState.Loaded && ammoCount > 0)
									{
										// Tiene munición - mostrar contador
										if (componentPlayer != null)
										{
											componentPlayer.ComponentGui.DisplaySmallMessage($"{ammoCount}/{MaxAmmo}", Color.White, false, false);
										}

										// Disparo automático - verificar tasa de fuego
										if (timeSinceLastFire >= FireRate)
										{
											if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor <= 0.4f)
											{
												// Posición de la boca del arma
												Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
													+ componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f
													- componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f
													+ aim.Direction * MuzzleOffset;
												Vector3 vector2 = aim.Direction;

												int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.AK47Bullet));
												Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + 120f * vector2;

												Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
												if (projectile != null)
												{
													projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
												}

												m_subsystemAudio.PlaySound("Audio/Armas/ak 47 fuego", 1f, m_random.Float(-0.1f, 0.1f), vector, 10f, true);
												m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, vector, vector2), false);
												m_subsystemNoise.MakeNoise(vector, 1f, 40f);

												// Reducir munición
												int newAmmoCount = ammoCount - 1;
												int newData = AK47Block.SetAmmoCount(Terrain.ExtractData(num2), newAmmoCount);

												if (newAmmoCount <= 0)
												{
													newData = AK47Block.SetLoadState(newData, AK47Block.LoadState.Empty);
												}

												num2 = Terrain.MakeBlockValue(num, 0, newData);
												num3 = 1;

												m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
											}
										}
									}
									else
									{
										// No tiene munición - mostrar mensaje de que necesita munición
										if (componentPlayer != null && timeSinceEmptyMessage >= EmptyMessageCooldown)
										{
											string ammoName = LanguageControl.GetBlock("AK47AmmunitionBlock", "DisplayName");
											string message = LanguageControl.Get("Firearms", 1);
											componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(message, ammoName), Color.White, true, false);
											m_lastEmptyMessageTimes[componentMiner] = m_subsystemTime.GameTime;
										}

										// Sonido empty fire con cooldown
										if (timeSinceEmptySound >= EmptySoundCooldown)
										{
											m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
											m_lastEmptySoundTimes[componentMiner] = m_subsystemTime.GameTime;
										}
									}

									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									if (componentFirstPersonModel != null)
									{
										if (componentPlayer != null)
										{
											componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
										}
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
									break;
								}
							case AimState.Cancelled:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								break;
							case AimState.Completed:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								break;
						}
					}
					if (num2 != slotValue)
					{
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, num2, 1);
					}
					if (num3 > 0)
					{
						componentMiner.DamageActiveTool(num3);
					}
				}
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotContents = Terrain.ExtractContents(inventory.GetSlotValue(slotIndex));

			if (slotContents != m_ak47BlockIndex) return 0;

			int ammoCount = AK47Block.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			// Solo no permitir recargar si ya está lleno (30/30)
			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_ak47AmmunitionBlockIndex)
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
				int newData = AK47Block.SetLoadState(data, AK47Block.LoadState.Loaded);
				newData = AK47Block.SetAmmoCount(newData, MaxAmmo);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_ak47BlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}
