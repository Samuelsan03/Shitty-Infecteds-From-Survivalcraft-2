using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemImprovedMusketBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();
			}
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = ((componentPlayer.ComponentGui.ModalPanelWidget == null) ? new ImprovedMusketWidget(inventory, slotIndex) : null);
			return true;
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

					if (num == this.m_ImprovedMusketBlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = this.m_subsystemTime.GameTime;
							this.m_aimStartTimes[componentMiner] = gameTime;
						}

						float num4 = (float)(this.m_subsystemTime.GameTime - gameTime);
						float num5 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
						Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((num4 - 2.5f) / 6f)) * new Vector3
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
									if (num4 > 0.5f && !ImprovedMusketBlock.GetHammerState(Terrain.ExtractData(num2)))
									{
										num2 = Terrain.MakeBlockValue(num, 0, ImprovedMusketBlock.SetHammerState(Terrain.ExtractData(num2), true));
										this.m_subsystemAudio.PlaySound("Audio/Hammer Cock Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
									}
									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									if (componentFirstPersonModel != null)
									{
										ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
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
								if (ImprovedMusketBlock.GetHammerState(Terrain.ExtractData(num2)))
								{
									num2 = Terrain.MakeBlockValue(num, 0, ImprovedMusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
									this.m_subsystemAudio.PlaySound("Audio/Hammer Uncock Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
								}
								this.m_aimStartTimes.Remove(componentMiner);
								break;
							case AimState.Completed:
								{
									bool flag = false;
									int value = 0;
									int num6 = 0;
									float s = 0f;
									Vector3 zero = Vector3.Zero;
									int ammoCount = ImprovedMusketBlock.GetAmmoCount(data);

									if (ImprovedMusketBlock.GetHammerState(Terrain.ExtractData(num2)))
									{
										if (ammoCount == 0)
										{
											ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
											if (componentPlayer2 != null)
											{
												componentPlayer2.ComponentGui.DisplaySmallMessage(LanguageControl.Get("SubsystemImprovedMusketBlockBehavior", 0), Color.White, true, false);
											}
										}
										else
										{
											flag = true;
											value = Terrain.MakeBlockValue(this.m_AntiTankPelletBlockIndex, 0, 0);
											num6 = 1;
											zero = Vector3.Zero;
											s = 120f;
										}
									}

									if (flag)
									{
										Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
										Vector3 vector2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
										Vector3 vector3 = Vector3.Normalize(Vector3.Cross(vector2, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector2, vector3));

										for (int i = 0; i < num6; i++)
										{
											Vector3 v3 = this.m_random.Float(0f - zero.X, zero.X) * vector3 + this.m_random.Float(0f - zero.Y, zero.Y) * v2 + this.m_random.Float(0f - zero.Z, zero.Z) * vector2;
											Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + s * (vector2 + v3);
											Projectile projectile = this.m_subsystemProjectiles.FireProjectile(value, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
											if (projectile != null)
											{
												projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
											}
										}
										this.m_subsystemAudio.PlaySound("Audio/musket improved fire", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
										this.m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(this.m_subsystemTerrain, vector + 0.3f * vector2, vector2), false);
										this.m_subsystemNoise.MakeNoise(vector, 1f, 40f);
										componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * vector2);

										int newAmmoCount = ammoCount - 1;
										int newData = Terrain.ExtractData(num2);
										newData = ImprovedMusketBlock.SetAmmoCount(newData, newAmmoCount);
										num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0, newData);
									}

									if (ImprovedMusketBlock.GetHammerState(Terrain.ExtractData(num2)))
									{
										num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0, ImprovedMusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
										this.m_subsystemAudio.PlaySound("Audio/Hammer Release Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
									}
									this.m_aimStartTimes.Remove(componentMiner);
									break;
								}
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
			int num = Terrain.ExtractContents(value);
			int musketData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			int ammoCount = ImprovedMusketBlock.GetAmmoCount(musketData);

			if (num == this.m_AntiTankPelletBlockIndex && ammoCount < 2)
			{
				return 2 - ammoCount;
			}
			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount > 0)
			{
				int musketData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				int currentAmmo = ImprovedMusketBlock.GetAmmoCount(musketData);
				int newAmmo = Math.Min(currentAmmo + processCount, 2);
				int actualProcessed = newAmmo - currentAmmo;

				if (actualProcessed > 0)
				{
					processedValue = 0;
					processedCount = count - actualProcessed;

					int newData = ImprovedMusketBlock.SetAmmoCount(musketData, newAmmo);

					inventory.RemoveSlotItems(slotIndex, 1);
					inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(this.m_ImprovedMusketBlockIndex, 0, newData), 1);
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_AntiTankPelletBlockIndex = BlocksManager.GetBlockIndex<AntiTankPelletBlock>(false, false);
			this.m_ImprovedMusketBlockIndex = BlocksManager.GetBlockIndex<ImprovedMusketBlock>(false, false);
			base.Load(valuesDictionary);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemNoise m_subsystemNoise;
		public Random m_random = new Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		public int m_AntiTankPelletBlockIndex;
		public int m_ImprovedMusketBlockIndex;
	}
}
