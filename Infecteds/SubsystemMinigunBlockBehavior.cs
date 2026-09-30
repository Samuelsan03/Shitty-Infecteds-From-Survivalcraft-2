using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemMinigunBlockBehavior : SubsystemBlockBehavior
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
		private int m_minigunBlockIndex;
		private int m_minigunAmmunitionBlockIndex;

		private const float FireRate = 0.025f; // Cadencia extrema
		private const int MaxAmmo = 200;
		private const float EmptySoundCooldown = 0.5f;
		private const float EmptyMessageCooldown = 0.5f;
		private const float MuzzleOffset = 1.2f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_minigunBlockIndex = BlocksManager.GetBlockIndex<MinigunBlock>(false, false);
			m_minigunAmmunitionBlockIndex = BlocksManager.GetBlockIndex<MinigunAmmunitionBlock>(false, false);
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

					if (num == m_minigunBlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
							m_lastFireTimes[componentMiner] = gameTime - FireRate;
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

						float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);

						// Retroceso masivo de la Minigun, reducido al agacharse
						float swayBase = componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.04f : 0.15f;
						float swayAmp = 0.3f * MathUtils.Saturate((num4 - 0.5f) / 3f); // El sway máximo se siente después del spin-up
						Vector3 v = (swayBase + swayAmp) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num5, 3f, 4, 3f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num5 + 100f, 3f, 4, 3f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num5 + 200f, 3f, 4, 3f, 0.5f, false)
						};
						aim.Direction = Vector3.Normalize(aim.Direction + v);

						MinigunBlock.LoadState loadState = MinigunBlock.GetLoadState(data);
						int ammoCount = MinigunBlock.GetAmmoCount(data);
						ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;

						switch (state)
						{
							case AimState.InProgress:
								{
									if (num4 >= 15f)
									{
										componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
										return true;
									}

									if (loadState == MinigunBlock.LoadState.Loaded && ammoCount > 0)
									{
										// ✅ LÓGICA DE SPIN-UP DE LA M134
										if (num4 >= 0.5f)
										{
											if (componentPlayer != null)
											{
												componentPlayer.ComponentGui.DisplaySmallMessage($"{ammoCount}/{MaxAmmo}", Color.White, false, false);
											}

											if (timeSinceLastFire >= FireRate)
											{
												Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
													+ componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f
													- componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f
													+ aim.Direction * MuzzleOffset;
												Vector3 vector2 = aim.Direction;

												int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.MinigunBullet));
												Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + 160f * vector2;

												Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
												if (projectile != null)
												{
													projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
												}

												m_subsystemAudio.PlaySound("Audio/Armas/Chaingun fuego", 1f, m_random.Float(-0.1f, 0.1f), vector, 15f, true);
												m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, vector, vector2), false);
												m_subsystemNoise.MakeNoise(vector, 1.5f, 60f); // Mucho ruido

												int newAmmoCount = ammoCount - 1;
												int newData = MinigunBlock.SetAmmoCount(Terrain.ExtractData(num2), newAmmoCount);

												if (newAmmoCount <= 0)
												{
													newData = MinigunBlock.SetLoadState(newData, MinigunBlock.LoadState.Empty);
												}

												num2 = Terrain.MakeBlockValue(num, 0, newData);
												num3 = 1;

												m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
											}
										}
										else
										{
											// Mensaje durante el spin-up
											if (componentPlayer != null && timeSinceEmptyMessage >= 0.1f)
											{
												componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get("SubsystemMinigunBlockBehavior", 0), Color.Yellow, false, false);
												m_lastEmptyMessageTimes[componentMiner] = m_subsystemTime.GameTime;
											}
										}
									}
									else
									{
										if (componentPlayer != null && timeSinceEmptyMessage >= EmptyMessageCooldown)
										{
											string ammoName = LanguageControl.GetBlock("MinigunAmmunitionBlock", "DisplayName");
											string message = LanguageControl.Get("Firearms", 1);
											componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(message, ammoName), Color.White, true, false);
											m_lastEmptyMessageTimes[componentMiner] = m_subsystemTime.GameTime;
										}

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
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.25f, 0.15f, 0.08f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.1f, 0.07f);
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

			if (slotContents != m_minigunBlockIndex) return 0;

			int ammoCount = MinigunBlock.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_minigunAmmunitionBlockIndex)
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
				int newData = MinigunBlock.SetLoadState(data, MinigunBlock.LoadState.Loaded);
				newData = MinigunBlock.SetAmmoCount(newData, MaxAmmo);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_minigunBlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}