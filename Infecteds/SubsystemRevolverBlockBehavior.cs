using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRevolverBlockBehavior : SubsystemBlockBehavior
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
		private Dictionary<ComponentMiner, bool> m_firedThisAim = new Dictionary<ComponentMiner, bool>();
		private Dictionary<ComponentMiner, bool> m_justEmptiedThisAim = new Dictionary<ComponentMiner, bool>();
		private int m_bulletBlockIndex;
		private int m_revolverBlockIndex;
		private int m_revolverAmmunitionBlockIndex;

		private const float FireRate = 0.45f;
		private const int MaxAmmo = 6;
		private const float EmptySoundCooldown = 0.5f;
		private const float EmptyMessageCooldown = 0.5f;
		private const float MuzzleOffset = 0.55f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_revolverBlockIndex = BlocksManager.GetBlockIndex<RevolverBlock>(false, false);
			m_revolverAmmunitionBlockIndex = BlocksManager.GetBlockIndex<RevolverAmmunitionBlock>(false, false);
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

					if (num == m_revolverBlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
							m_lastFireTimes[componentMiner] = gameTime - FireRate;
							m_lastEmptySoundTimes[componentMiner] = gameTime - EmptySoundCooldown;
							m_lastEmptyMessageTimes[componentMiner] = gameTime - EmptyMessageCooldown;
							m_firedThisAim[componentMiner] = false;
							m_justEmptiedThisAim[componentMiner] = false;
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

						bool alreadyFiredThisAim;
						m_firedThisAim.TryGetValue(componentMiner, out alreadyFiredThisAim);

						bool justEmptiedThisAim;
						m_justEmptiedThisAim.TryGetValue(componentMiner, out justEmptiedThisAim);

						float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
						Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.004f : 0.012f) + 0.06f * MathUtils.Saturate(num4 / 5f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
						};
						aim.Direction = Vector3.Normalize(aim.Direction + v);

						RevolverBlock.LoadState loadState = RevolverBlock.GetLoadState(data);
						int ammoCount = RevolverBlock.GetAmmoCount(data);
						ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;

						switch (state)
						{
							case AimState.InProgress:
								{
									if (num4 >= 10f)
									{
										componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
										return true;
									}

									if (loadState == RevolverBlock.LoadState.Loaded && ammoCount > 0)
									{
										// ✅ Mostrar contador cuando hay munición
										if (componentPlayer != null)
										{
											componentPlayer.ComponentGui.DisplaySmallMessage($"{ammoCount}/{MaxAmmo}", Color.White, false, false);
										}

										if (!alreadyFiredThisAim && timeSinceLastFire >= FireRate)
										{
											Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
												+ componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.2f
												- componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.12f
												+ aim.Direction * MuzzleOffset;
											Vector3 vector2 = aim.Direction;

											int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.RevolverBullet));
											Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + 100f * vector2;

											Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
											if (projectile != null)
											{
												projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
											}

											m_subsystemAudio.PlaySound("Audio/Armas/Revolver fuego", 1f, m_random.Float(-0.1f, 0.1f), vector, 10f, true);
											m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, vector, vector2), false);
											m_subsystemNoise.MakeNoise(vector, 1.0f, 45f);

											int newAmmoCount = ammoCount - 1;
											int newData = RevolverBlock.SetAmmoCount(Terrain.ExtractData(num2), newAmmoCount);

											// ✅ Marcar si acabamos de vaciar el arma
											if (newAmmoCount <= 0)
											{
												newData = RevolverBlock.SetLoadState(newData, RevolverBlock.LoadState.Empty);
												m_justEmptiedThisAim[componentMiner] = true;
											}

											num2 = Terrain.MakeBlockValue(num, 0, newData);
											num3 = 1;

											m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
											m_firedThisAim[componentMiner] = true;
										}
									}
									else
									{
										// ✅ SIN MUNICIÓN
										if (justEmptiedThisAim)
										{
											// Acabamos de disparar la última bala - mostrar "0/X"
											if (componentPlayer != null)
											{
												componentPlayer.ComponentGui.DisplaySmallMessage($"0/{MaxAmmo}", Color.White, false, false);
											}
										}
										else
										{
											// El arma YA ESTABA vacía antes de apuntar - mostrar "necesitas X munición"
											if (componentPlayer != null && !alreadyFiredThisAim && timeSinceEmptyMessage >= EmptyMessageCooldown)
											{
												string ammoName = LanguageControl.GetBlock("RevolverAmmunitionBlock", "DisplayName");
												string message = LanguageControl.Get("Firearms", 1);
												componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(message, ammoName), Color.White, true, false);
												m_lastEmptyMessageTimes[componentMiner] = m_subsystemTime.GameTime;
											}

											if (!alreadyFiredThisAim && timeSinceEmptySound >= EmptySoundCooldown)
											{
												m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
												m_lastEmptySoundTimes[componentMiner] = m_subsystemTime.GameTime;
												m_firedThisAim[componentMiner] = true;
											}
										}
									}

									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									if (componentFirstPersonModel != null)
									{
										if (componentPlayer != null)
										{
											componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
										}
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.15f, 0.1f, 0.05f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.5f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.0f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.05f, -0.05f, 0.04f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.3f, 0f, 0f);
									break;
								}
							case AimState.Cancelled:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								m_firedThisAim.Remove(componentMiner);
								m_justEmptiedThisAim.Remove(componentMiner);
								break;
							case AimState.Completed:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								m_firedThisAim.Remove(componentMiner);
								m_justEmptiedThisAim.Remove(componentMiner);
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

			if (slotContents != m_revolverBlockIndex) return 0;

			int ammoCount = RevolverBlock.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_revolverAmmunitionBlockIndex)
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
				int newData = RevolverBlock.SetLoadState(data, RevolverBlock.LoadState.Loaded);
				newData = RevolverBlock.SetAmmoCount(newData, MaxAmmo);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_revolverBlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}