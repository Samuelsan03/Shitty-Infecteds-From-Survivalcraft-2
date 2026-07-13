using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class SubsystemRepeatCrossbowBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemAudio m_subsystemAudio;
		public Random m_random = new Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		public int m_RepeatCrossbowBlockIndex;
		public int m_RepeatBoltBlockIndex;

		public override int[] HandledBlocks => new int[] { RepeatCrossbowBlock.Index };

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = (componentPlayer.ComponentGui.ModalPanelWidget == null) ? new RepeatCrossbowWidget(inventory, slotIndex) : null;
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
					int contents = Terrain.ExtractContents(slotValue);
					int data = Terrain.ExtractData(slotValue);

					if (slotCount > 0 && contents == m_RepeatCrossbowBlockIndex)
					{
						int draw = RepeatCrossbowBlock.GetDraw(data);
						RepeatBoltType? boltType = RepeatCrossbowBlock.GetRepeatBoltType(data);
						int count = RepeatCrossbowBlock.GetCount(data);

						double gameTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = gameTime;
						}

						float num = (float)(m_subsystemTime.GameTime - gameTime);
						float num2 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);

						Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.15f * MathUtils.Saturate((num - 2.5f) / 6f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false)
						};
						aim.Direction = Vector3.Normalize(aim.Direction + v);

						switch (state)
						{
							case AimState.InProgress:
								if (num >= 10f)
								{
									componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
									return true;
								}

								ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
								if (componentFirstPersonModel != null)
								{
									ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
									if (componentPlayer != null)
										componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
									componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.22f, 0.15f, 0.1f);
									componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
								}

								componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.3f;
								componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
								componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
								break;

							case AimState.Cancelled:
								m_aimStartTimes.Remove(componentMiner);
								break;

							case AimState.Completed:
								if (draw != 15)
								{
									componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
										LanguageControl.Get("SubsystemRepeatCrossbowBlockBehavior", "0"),
										Color.White, true, false);
									data = RepeatCrossbowBlock.SetDraw(data, 0);
								}
								else if (count == 0)
								{
									componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
										LanguageControl.Get("SubsystemRepeatCrossbowBlockBehavior", "1"),
										Color.White, true, false);
									// Reproducir sonido de disparo vacío
									m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, m_random.Float(-0.1f, 0.1f),
										componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
									data = RepeatCrossbowBlock.SetDraw(data, 0);
								}
								else if (boltType != null)
								{
									Vector3 position = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
													   componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
													   componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
									Vector3 direction = Vector3.Normalize(position + aim.Direction * 10f - position);

									int boltValue = Terrain.MakeBlockValue(m_RepeatBoltBlockIndex, 0, RepeatBoltBlock.SetRepeatBoltType(0, boltType.Value));
									float speed = 55f;
									Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + speed * direction;

									if (m_subsystemProjectiles.FireProjectile(boltValue, position, velocity, Vector3.Zero, componentMiner.ComponentCreature) != null)
									{
										m_subsystemAudio.PlaySound("Audio/Crossbow Remake/Crossbow Shoot", 1f, m_random.Float(-0.1f, 0.1f),
											componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);

										int newCount = Math.Max(0, count - 1);
										data = RepeatCrossbowBlock.SetCount(data, newCount);
										if (newCount == 0)
										{
											data = RepeatCrossbowBlock.SetRepeatBoltType(data, null);
											data = RepeatCrossbowBlock.SetDraw(data, 0);
										}
										else
										{
											data = RepeatCrossbowBlock.SetDraw(data, 15);
										}
									}
									else
									{
										data = RepeatCrossbowBlock.SetDraw(data, 0);
									}
								}
								else
								{
									data = RepeatCrossbowBlock.SetRepeatBoltType(data, null);
									data = RepeatCrossbowBlock.SetDraw(data, 0);
								}

								inventory.RemoveSlotItems(activeSlotIndex, 1);
								int newValue = Terrain.MakeBlockValue(m_RepeatCrossbowBlockIndex, 0, data);
								inventory.AddSlotItems(activeSlotIndex, newValue, 1);

								m_aimStartTimes.Remove(componentMiner);
								break;
						}
					}
				}
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int contents = Terrain.ExtractContents(value);
			RepeatBoltType boltType = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(value));

			if (contents == m_RepeatBoltBlockIndex)
			{
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				RepeatBoltType? loadedBolt = RepeatCrossbowBlock.GetRepeatBoltType(data);
				int draw = RepeatCrossbowBlock.GetDraw(data);
				int count = RepeatCrossbowBlock.GetCount(data);

				if (draw == 15 && count < 8)
				{
					if (loadedBolt == null || loadedBolt.Value == boltType)
						return 1;
				}
			}
			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount == 1)
			{
				RepeatBoltType boltType = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(value));
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				RepeatBoltType? loadedBolt = RepeatCrossbowBlock.GetRepeatBoltType(data);
				int draw = RepeatCrossbowBlock.GetDraw(data);
				int currentCount = RepeatCrossbowBlock.GetCount(data);

				if (draw == 15 && currentCount < 8)
				{
					if (loadedBolt == null || loadedBolt.Value == boltType)
					{
						int toLoad = Math.Min(8 - currentCount, count);
						if (toLoad > 0)
						{
							data = RepeatCrossbowBlock.SetCount(data, currentCount + toLoad);
							data = RepeatCrossbowBlock.SetRepeatBoltType(data, boltType);

							inventory.RemoveSlotItems(slotIndex, 1);
							int newValue = Terrain.MakeBlockValue(m_RepeatCrossbowBlockIndex, 0, data);
							inventory.AddSlotItems(slotIndex, newValue, 1);

							processedValue = value;
							processedCount = count - toLoad;
							return;
						}
					}
				}
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatBoltType boltType = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(worldItem.Value));
			if (worldItem.Velocity.Length() > 10f)
			{
				float chance = 0f;
				switch (boltType)
				{
					case RepeatBoltType.RepeatIronBolt: chance = 0.05f; break;
					case RepeatBoltType.RepeatDiamondBolt: chance = 0f; break;
					case RepeatBoltType.RepeatCopperBolt: chance = 0.10f; break;
					case RepeatBoltType.RepeatExplosiveBolt: chance = 0f; break;
				}
				if (m_random.Float(0f, 1f) < chance)
					return true;
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_RepeatCrossbowBlockIndex = BlocksManager.GetBlockIndex<RepeatCrossbowBlock>(false, false);
			m_RepeatBoltBlockIndex = BlocksManager.GetBlockIndex<RepeatBoltBlock>(false, false);
			base.Load(valuesDictionary);
		}
	}
}
