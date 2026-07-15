using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFlameThrowerBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { FlameThrowerBlock.Index };

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
				return false;

			int newData = data;
			bool changed = false;

			double gameTime;
			if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
			{
				gameTime = m_subsystemTime.GameTime;
				m_aimStartTimes[componentMiner] = gameTime;
			}

			float aimDuration = (float)(m_subsystemTime.GameTime - gameTime);

			// Temblor al apuntar
			float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) * new Vector3
			{
				X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
				Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
				Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
			};
			aim.Direction = Vector3.Normalize(aim.Direction + v);

			switch (state)
			{
				case AimState.InProgress:
					if (aimDuration >= 10f)
					{
						componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
						return true;
					}

					if (aimDuration > 0.5f && !FlameThrowerBlock.GetSwitchState(newData))
					{
						newData = FlameThrowerBlock.SetSwitchState(newData, true);
						m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
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

					changed = true;
					break;

				case AimState.Cancelled:
					if (FlameThrowerBlock.GetSwitchState(newData))
					{
						newData = FlameThrowerBlock.SetSwitchState(newData, false);
						m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}
					m_aimStartTimes.Remove(componentMiner);
					changed = true;
					break;

				case AimState.Completed:
					bool shouldFire = false;
					int bulletValue = 0;
					int bulletCount = 0;
					Vector3 spread = Vector3.Zero;
					float speed = 0f;

					FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(newData);
					BulletBlock.BulletType? bulletType = FlameThrowerBlock.GetBulletType(newData);

					if (FlameThrowerBlock.GetSwitchState(newData))
					{
						if (loadState == FlameThrowerBlock.LoadState.Empty)
						{
							componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("FlameThrower is empty", Color.White, true, false);
						}
						else if (loadState == FlameThrowerBlock.LoadState.Loaded)
						{
							shouldFire = true;
							if (bulletType.HasValue)
							{
								bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, BulletBlock.SetBulletType(0, bulletType.Value));
								bulletCount = 1;
								speed = 120f;
							}
							else
							{
								bulletValue = Terrain.MakeBlockValue(m_bulletBlockIndex, 0, 0);
								bulletCount = 1;
								speed = 120f;
							}
							spread = new Vector3(0.06f, 0.06f, 0f);
						}
					}

					if (shouldFire)
					{
						if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor > 0.4f)
						{
							m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);
						}
						else
						{
							Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
							Vector3 fireDir = Vector3.Normalize(aim.Direction);
							Vector3 right = Vector3.Normalize(Vector3.Cross(fireDir, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(fireDir, right));

							for (int i = 0; i < bulletCount; i++)
							{
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
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
							}

							m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
							m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, eyePos + 0.3f * fireDir, fireDir), false);
							m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);
							componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * fireDir);
						}

						// Vaciar el lanzallamas después de disparar
						newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Empty);
						newData = FlameThrowerBlock.SetBulletType(newData, null);
						componentMiner.DamageActiveTool(1);
					}

					if (FlameThrowerBlock.GetSwitchState(newData))
					{
						newData = FlameThrowerBlock.SetSwitchState(newData, false);
						m_subsystemAudio.PlaySound("Audio/HammerRelease", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}

					m_aimStartTimes.Remove(componentMiner);
					changed = true;
					break;
			}

			if (changed && newData != data)
			{
				int newValue = Terrain.MakeBlockValue(contents, 0, newData);
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}

			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotValue = inventory.GetSlotValue(slotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			if (contents != FlameThrowerBlock.Index)
				return 0;

			int data = Terrain.ExtractData(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);

			// Solo acepta balas si está vacío
			if (loadState == FlameThrowerBlock.LoadState.Empty && Terrain.ExtractContents(value) == m_bulletBlockIndex)
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
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);

				if (loadState == FlameThrowerBlock.LoadState.Empty && Terrain.ExtractContents(value) == m_bulletBlockIndex)
				{
					// Cargar directamente con la bala
					BulletBlock.BulletType? bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(value));
					int newData = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetBulletType(newData, bulletType);

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
			m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			base.Load(valuesDictionary);
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();
		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		private int m_bulletBlockIndex;
	}
}