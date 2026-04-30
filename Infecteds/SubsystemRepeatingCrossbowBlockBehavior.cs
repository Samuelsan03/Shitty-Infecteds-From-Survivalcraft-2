using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatingCrossbowBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemAudio m_subsystemAudio;
		public Game.Random m_random = new Game.Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		public const int MCount = 8;

		public override int[] HandledBlocks => new int[] { RepeatingCrossbowBlock.Index };

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = componentPlayer.ComponentGui.ModalPanelWidget == null ? new RepeatCrossbowWidget(inventory, slotIndex) : null;
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
			int num = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);

			if (!(BlocksManager.Blocks[num] is RepeatingCrossbowBlock) || slotCount <= 0) return false;

			int draw = RepeatingCrossbowBlock.GetDraw(data);
			double gameTime;
			if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
			{
				gameTime = m_subsystemTime.GameTime;
				m_aimStartTimes[componentMiner] = gameTime;
			}

			float num2 = (float)(m_subsystemTime.GameTime - gameTime);
			float num3 = componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f;
			float num4 = MathUtils.Saturate((num2 - 2.5f) / 6f);
			float s = num3 + 0.15f * num4;
			float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			float x = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false);
			float y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false);
			float z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false);
			Vector3 v = new Vector3(x, y, z) * s;
			aim.Direction = Vector3.Normalize(aim.Direction + v);

			switch (state)
			{
				case AimState.InProgress:
					if (num2 >= 10.0)
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

					if (m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0) && componentMiner.ComponentPlayer == null)
					{
						int? arrowType = RepeatingCrossbowBlock.GetArrowType(data);
						if (draw != 15)
							data = RepeatingCrossbowBlock.SetDraw(data, 15);
						else if (arrowType == null)
						{
							float rand = m_random.Float(0f, 1f);
							arrowType = rand < 0.1f ? 1 : 0;
							data = RepeatingCrossbowBlock.SetArrowType(data, arrowType);
						}
						inventory.RemoveSlotItems(inventory.ActiveSlotIndex, 1);
						inventory.AddSlotItems(inventory.ActiveSlotIndex, Terrain.MakeBlockValue(num, 0, data), 1);
					}
					break;

				case AimState.Cancelled:
					m_aimStartTimes.Remove(componentMiner);
					break;

				case AimState.Completed:
					int loadCount = RepeatingCrossbowBlock.GetLoadCount(slotValue);
					int? arrowType2 = RepeatingCrossbowBlock.GetArrowType(data);

					if (draw != 15)
					{
						componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get("SubsystemRepeatingCrossbowBlockBehavior", 0), Color.Orange, true, false);
					}
					else if (arrowType2 == null)
					{
						componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get("SubsystemRepeatingCrossbowBlockBehavior", 1), Color.Orange, true, false);
					}
					else
					{
						// Disparar proyectil
						Vector3 eyePosition = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
						Matrix matrix = componentMiner.ComponentCreature.ComponentBody.Matrix;
						Vector3 rightOffset = matrix.Right * 0.3f;
						Vector3 upOffset = matrix.Up * -0.2f;
						Vector3 spawnPoint = eyePosition + rightOffset + upOffset;
						Vector3 direction = Vector3.Normalize(spawnPoint + aim.Direction * 10f - spawnPoint);
						int projectileValue = Terrain.MakeBlockValue(RepeatingBoltBlock.Index, 0, RepeatingBoltBlock.SetArrowType(0, arrowType2.Value));
						Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, spawnPoint, direction * 40f, Vector3.Zero, componentMiner.ComponentCreature);

						if (projectile != null)
						{
							m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);
							if (componentMiner.ComponentPlayer == null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
						}

						// Actualizar estado del arma después del disparo (solo si se disparó)
						int newLoad = loadCount - 1;
						int newData;
						if (newLoad <= 0)
						{
							newData = RepeatingCrossbowBlock.SetArrowType(data, null);
							newData = RepeatingCrossbowBlock.SetDraw(newData, 0);
							newLoad = 0;
						}
						else
						{
							newData = RepeatingCrossbowBlock.SetArrowType(data, arrowType2);
							newData = RepeatingCrossbowBlock.SetDraw(newData, 15);
						}
						int newItemValue = Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, newLoad, newData);
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, newItemValue, 1);

						if (draw > 0)
						{
							componentMiner.DamageActiveTool(1);
							m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
						}
					}

					m_aimStartTimes.Remove(componentMiner);
					break;
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			if (!(BlocksManager.Blocks[Terrain.ExtractContents(value)] is RepeatingBoltBlock))
				return 0;

			int boltArrowType = RepeatingBoltBlock.GetArrowType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatingCrossbowBlock.GetDraw(data);
			int loadCount = RepeatingCrossbowBlock.GetLoadCount(slotValue);
			int? currentArrowType = RepeatingCrossbowBlock.GetArrowType(data);

			if (draw != 15) return 0;
			if (loadCount >= 8) return 0;
			if (currentArrowType != null && currentArrowType.Value != boltArrowType) return 0;

			return 8 - loadCount;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			int boltArrowType = RepeatingBoltBlock.GetArrowType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int currentLoad = RepeatingCrossbowBlock.GetLoadCount(slotValue);
			int currentDraw = RepeatingCrossbowBlock.GetDraw(data);
			int? currentArrowType = RepeatingCrossbowBlock.GetArrowType(data);

			processedCount = count - processCount;
			processedValue = value;
			if (processedCount == 0) processedValue = 0;

			int newLoad = currentLoad + processCount;
			if (newLoad > 8) newLoad = 8;

			int newData;
			if (currentArrowType == null)
				newData = RepeatingCrossbowBlock.SetArrowType(data, boltArrowType);
			else
				newData = RepeatingCrossbowBlock.SetArrowType(data, currentArrowType);

			newData = RepeatingCrossbowBlock.SetDraw(newData, currentDraw);
			if (RepeatingCrossbowBlock.GetDraw(newData) != 15)
				newData = RepeatingCrossbowBlock.SetDraw(newData, 15);

			int newItemValue = Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, newLoad, newData);
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newItemValue, 1);
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			base.Load(valuesDictionary);
		}
	}
}