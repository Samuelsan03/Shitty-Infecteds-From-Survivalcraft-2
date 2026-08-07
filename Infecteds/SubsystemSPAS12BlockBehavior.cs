using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemSPAS12BlockBehavior : SubsystemBlockBehavior
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
		private Dictionary<ComponentMiner, bool> m_firedThisAim = new Dictionary<ComponentMiner, bool>(); // NUEVO
		private int m_bulletBlockIndex;
		private int m_spas12BlockIndex;
		private int m_spas12AmmunitionBlockIndex;

		// Constantes del SPAS-12 - SEMIAUTOMÁTICO REAL
		private const float FireRate = 0.3f; // Solo controla el mínimo entre clics
		private const int MaxAmmo = 8;
		private const float EmptySoundCooldown = 0.5f;
		private const float EmptyMessageCooldown = 0.5f;
		private const float MuzzleOffset = 0.9f;
		private const int PelletCount = 8;
		private const float PelletSpread = 0.15f;
		private const float PelletVelocity = 100f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<FirearmsBulletBlock>(false, false);
			m_spas12BlockIndex = BlocksManager.GetBlockIndex<SPAS12Block>(false, false);
			m_spas12AmmunitionBlockIndex = BlocksManager.GetBlockIndex<SPAS12AmmunitionBlock>(false, false);
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

					if (num == m_spas12BlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
							m_lastFireTimes[componentMiner] = gameTime - FireRate; // Permitir disparar inmediatamente
							m_lastEmptySoundTimes[componentMiner] = gameTime - EmptySoundCooldown;
							m_lastEmptyMessageTimes[componentMiner] = gameTime - EmptyMessageCooldown;
							m_firedThisAim[componentMiner] = false; // NUEVO: Resetear
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

						// Dispersión base para escopeta
						float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
						Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.02f : 0.04f) + 0.1f * MathUtils.Saturate(num4 / 3f)) * new Vector3
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

									SPAS12Block.LoadState loadState = SPAS12Block.GetLoadState(data);
									int ammoCount = SPAS12Block.GetAmmoCount(data);

									ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;

									if (loadState == SPAS12Block.LoadState.Loaded && ammoCount > 0)
									{
										if (componentPlayer != null)
										{
											componentPlayer.ComponentGui.DisplaySmallMessage($"{ammoCount}/{MaxAmmo}", Color.White, false, false);
										}

										// SEMIAUTOMÁTICO: Solo disparar si NO ha disparado este clic
										if (!alreadyFiredThisAim && timeSinceLastFire >= FireRate)
										{
											if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor <= 0.4f)
											{
												Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
													+ componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f
													- componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f
													+ aim.Direction * MuzzleOffset;
												Vector3 vector2 = aim.Direction;

												for (int i = 0; i < PelletCount; i++)
												{
													float spreadX = m_random.Float(-PelletSpread, PelletSpread);
													float spreadY = m_random.Float(-PelletSpread, PelletSpread);
													float spreadZ = m_random.Float(-PelletSpread, PelletSpread);

													Vector3 pelletDirection = Vector3.Normalize(vector2 + new Vector3(spreadX, spreadY, spreadZ));

													int bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, FirearmsBulletBlock.SetFirearmsBulletType(0, FirearmsBulletBlock.FirearmsBulletType.SPAS12Bullet));
													Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + PelletVelocity * pelletDirection;

													Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
													if (projectile != null)
													{
														projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
													}
												}

												m_subsystemAudio.PlaySound("Audio/Armas/SPAS 12 fuego", 1f, m_random.Float(-0.1f, 0.1f), vector, 12f, true);
												m_subsystemParticles.AddParticleSystem(new TestGunFireParticleSystem(m_subsystemTerrain, vector, vector2), false);
												m_subsystemNoise.MakeNoise(vector, 1.2f, 45f);

												int newAmmoCount = ammoCount - 1;
												int newData = SPAS12Block.SetAmmoCount(Terrain.ExtractData(num2), newAmmoCount);

												if (newAmmoCount <= 0)
												{
													newData = SPAS12Block.SetLoadState(newData, SPAS12Block.LoadState.Empty);
												}

												num2 = Terrain.MakeBlockValue(num, 0, newData);
												num3 = 1;

												m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
												m_firedThisAim[componentMiner] = true; // NUEVO: Marcar como ya disparó
											}
										}
									}
									else
									{
										// No tiene munición - también semiautomático
										if (componentPlayer != null && !alreadyFiredThisAim && timeSinceEmptyMessage >= EmptyMessageCooldown)
										{
											string ammoName = LanguageControl.GetBlock("SPAS12AmmunitionBlock", "DisplayName");
											string message = LanguageControl.Get("Firearms", 1);
											componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(message, ammoName), Color.White, true, false);
											m_lastEmptyMessageTimes[componentMiner] = m_subsystemTime.GameTime;
										}

										if (!alreadyFiredThisAim && timeSinceEmptySound >= EmptySoundCooldown)
										{
											m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
											m_lastEmptySoundTimes[componentMiner] = m_subsystemTime.GameTime;
											m_firedThisAim[componentMiner] = true; // NUEVO
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
								m_firedThisAim.Remove(componentMiner); // NUEVO
								break;
							case AimState.Completed:
								m_aimStartTimes.Remove(componentMiner);
								m_lastFireTimes.Remove(componentMiner);
								m_lastEmptySoundTimes.Remove(componentMiner);
								m_lastEmptyMessageTimes.Remove(componentMiner);
								m_firedThisAim.Remove(componentMiner); // NUEVO
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

			if (slotContents != m_spas12BlockIndex) return 0;

			int ammoCount = SPAS12Block.GetAmmoCount(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));

			if (ammoCount >= MaxAmmo) return 0;

			int itemContents = Terrain.ExtractContents(value);
			if (itemContents == m_spas12AmmunitionBlockIndex)
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
				int newData = SPAS12Block.SetLoadState(data, SPAS12Block.LoadState.Loaded);
				newData = SPAS12Block.SetAmmoCount(newData, MaxAmmo);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_spas12BlockIndex, 0, newData), 1);

				m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
			}
		}
	}
}
