using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemMaster308BlockBehavior : SubsystemBlockBehavior
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
		private int m_bulletBlockIndex;
		private int m_master308BlockIndex;
		private int m_master308AmmunitionBlockIndex;

		// Rifle bolt-action - más lento pero más preciso y potente
		private const float FireRate = 1.8f;
		private const int MaxAmmo = 5;
		private const float EmptySoundCooldown = 0.5f;
		private const float EmptyMessageCooldown = 0.5f;
		private const float MuzzleOffset = 1.3f;
		private const float BulletVelocity = 180f;
		private const float BulletSpread = 0.012f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_master308BlockIndex = BlocksManager.GetBlockIndex<Master308Block>(false, false);
			m_master308AmmunitionBlockIndex = BlocksManager.GetBlockIndex<Master308AmmunitionBlock>(false, false);
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

					if (num == m_master308BlockIndex && slotCount > 0)
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

						float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);

						// Rifle tiene menos sway que escopeta, se estabiliza más tiempo
						float swayAmount = componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.012f : 0.025f;
						swayAmount += 0.04f * MathUtils.Saturate(num4 / 5f);

						Vector3 v = swayAmount * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
						};
						aim.Direction = Vector3.Normalize(aim.Direction + v);

						Master308Block.LoadState loadState = Master308Block.GetLoadState(data);
						int ammoCount = Master308Block.GetAmmoCount(data);
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

									if (loadState == Master308Block.LoadState.Loaded && ammoCount > 0)
									{
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

											// Un solo proyectil, muy preciso
											float spreadX = m_random.Float(-BulletSpread, BulletSpread);
											float spreadY = m_random.Float(-BulletSpread, BulletSpread);
											float spreadZ = m_random.Float(-BulletSpread, BulletSpread);

											Vector3 bulletDirection = Vector3.Normalize(vector2 + new Vector3(spreadX, spreadY, spreadZ));

											int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.Master308Bullet));
											Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + BulletVelocity * bulletDirection;

											Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
											if (projectile != null)
											{
												projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
											}

											m_subsystemAudio.PlaySound("Audio/Armas/308 Master fire", 1f, m_random.Float(-0.03f, 0.03f), vector, 18f, true);
											m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, vector, vector2), false);
											m_subsystemNoise.MakeNoise(vector, 2f, 60f);

											int newAmmoCount = ammoCount - 1;
											ammoCount = newAmmoCount;

											int newData = Master308Block.SetAmmoCount(Terrain.ExtractData(num2), newAmmoCount);

											if (newAmmoCount <= 0)
											{
												newData = Master308Block.SetLoadState(newData, Master308Block.LoadState.Empty);
												loadState = Master308Block.LoadState.Empty;
											}

											num2 = Terrain.MakeBlockValue(num, 0, newData);
											num3 = 1;

											m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
											m_firedThisAim[componentMiner] = true;
										}
									}
									else if (alreadyFiredThisAim)
									{
										if (componentPlayer != null)
										{
											componentPlayer.ComponentGui.DisplaySmallMessage($"0/{MaxAmmo}", Color.White, false, false);
										}
									}
									else
									{
										if (componentPlayer != null && timeSinceEmptyMessage >= EmptyMessageCooldown)
										{
											string ammoName = LanguageControl.GetBlock("Master308AmmunitionBlock", "DisplayName");
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
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.24f, 0.1f, 0.04f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.6f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.2f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.05f, -0.12f, 0.12f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
									break;
								}
							case AimState.Cancelled:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								m_firedThisAim.Remove(componentMiner);
								break;
							case AimState.Completed:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								m_firedThisAim.Remove(componentMiner);
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

			if (slotContents != m_master308BlockIndex) return 0;

			int ammoCount = Master308Block.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_master308AmmunitionBlockIndex)
				return MaxAmmo - ammoCount;

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount > 0)
			{
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				int currentAmmo = Master308Block.GetAmmoCount(data);
				int newAmmo = Math.Min(currentAmmo + processCount, MaxAmmo);

				int newData = Master308Block.SetLoadState(data, Master308Block.LoadState.Loaded);
				newData = Master308Block.SetAmmoCount(newData, newAmmo);

				int actuallyUsed = newAmmo - currentAmmo;

				processedValue = 0;
				processedCount = count - actuallyUsed;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_master308BlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}