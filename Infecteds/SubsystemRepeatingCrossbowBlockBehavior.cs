using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatingCrossbowBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { RepeatingCrossbowBlock.Index };

		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemAudio m_subsystemAudio;
		private Random m_random = new Random();
		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			base.Load(valuesDictionary);
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = (componentPlayer.ComponentGui.ModalPanelWidget == null)
				? new RepeatingCrossbowWidget(inventory, slotIndex)
				: null;
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
			if (slotCount <= 0) return false;

			int contents = Terrain.ExtractContents(slotValue);
			if (contents != RepeatingCrossbowBlock.Index) return false;

			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatingCrossbowBlock.GetDraw(data);
			int boltCount = RepeatingCrossbowBlock.GetBoltCount(data);
			RepeatingBoltBlock.RepeatingBoltType boltType = RepeatingCrossbowBlock.GetBoltType(data);

			double gameTime = m_subsystemTime.GameTime;

			switch (state)
			{
				case AimState.InProgress:
					if (!m_aimStartTimes.ContainsKey(componentMiner))
					{
						m_aimStartTimes[componentMiner] = gameTime;
					}

					float aimDuration = (float)(gameTime - m_aimStartTimes[componentMiner]);
					if (aimDuration >= 10f)
					{
						componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
						return true;
					}

					float num2 = (float)MathUtils.Remainder(gameTime, 1000.0);
					Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.15f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) * new Vector3
					{
						X = SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false),
						Y = SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false),
						Z = SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false)
					};
					aim.Direction = Vector3.Normalize(aim.Direction + v);

					ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
					if (componentFirstPersonModel != null)
					{
						ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
						if (componentPlayer != null)
						{
							componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
						}
						componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.22f, 0.15f, 0.1f);
						componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
					}
					componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.3f;
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
					return false;

				case AimState.Cancelled:
					m_aimStartTimes.Remove(componentMiner);
					return false;

				case AimState.Completed:
					if (draw == 15 && boltCount > 0)
					{
						Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
						Vector3 v2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
						int boltValue = Terrain.MakeBlockValue(RepeatingBoltBlock.Index, 0, RepeatingBoltBlock.SetBoltType(0, boltType));
						float s = 38f;
						Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + s * v2;
						if (m_subsystemProjectiles.FireProjectile(boltValue, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature) != null)
						{
							m_subsystemAudio.PlaySound("Audio/Bow", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);
						}

						boltCount--;
						int newData = RepeatingCrossbowBlock.SetBoltCount(data, boltCount);
						if (boltCount == 0)
						{
							newData = RepeatingCrossbowBlock.SetDraw(newData, 0);
							m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
						}

						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, newData), 1);
					}
					else if (draw != 15)
					{
						ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
						if (componentPlayer3 != null)
						{
							componentPlayer3.ComponentGui.DisplaySmallMessage("Cuerda no tensada", Color.White, true, false);
						}
						// Destensar si no estaba tensada (igual que el original)
						int newData2 = RepeatingCrossbowBlock.SetDraw(data, 0);
						if (newData2 != data)
						{
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, newData2), 1);
							m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
						}
					}
					else if (boltCount == 0)
					{
						ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
						if (componentPlayer2 != null)
						{
							componentPlayer2.ComponentGui.DisplaySmallMessage("Sin virotes", Color.White, true, false);
						}
						// Destensar la ballesta al intentar disparar sin virotes
						int newData2 = RepeatingCrossbowBlock.SetDraw(data, 0);
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, newData2), 1);
						m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
					}
					m_aimStartTimes.Remove(componentMiner);
					return false;
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			if (Terrain.ExtractContents(value) != RepeatingBoltBlock.Index)
				return 0;

			int crossbowData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			int draw = RepeatingCrossbowBlock.GetDraw(crossbowData);

			if (draw != 15)
				return 0;

			int currentCount = RepeatingCrossbowBlock.GetBoltCount(crossbowData);
			if (currentCount >= 8)
				return 0;

			RepeatingBoltBlock.RepeatingBoltType incomingType = RepeatingBoltBlock.GetBoltType(Terrain.ExtractData(value));
			if (currentCount == 0)
				return 1;

			RepeatingBoltBlock.RepeatingBoltType currentType = RepeatingCrossbowBlock.GetBoltType(crossbowData);
			return (incomingType == currentType) ? 1 : 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			if (processCount == 1)
			{
				int currentData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				int boltCount = RepeatingCrossbowBlock.GetBoltCount(currentData);
				RepeatingBoltBlock.RepeatingBoltType boltType = RepeatingBoltBlock.GetBoltType(Terrain.ExtractData(value));

				if (boltCount == 0)
					currentData = RepeatingCrossbowBlock.SetBoltType(currentData, boltType);

				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, 0,
					RepeatingCrossbowBlock.SetBoltCount(currentData, boltCount + 1)), 1);
				processedValue = 0;
				processedCount = 0;
			}
			else
			{
				processedValue = value;
				processedCount = count;
			}
		}
	}
}