using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class RepeatingCrossbowWidget : CanvasWidget
	{
		private IInventory m_inventory;
		private int m_slotIndex;
		private GridPanelWidget m_inventoryGrid;
		private InventorySlotWidget m_inventorySlotWidget;
		private LabelWidget m_instructionsLabel;
		private LabelWidget m_ammoLabel;
		private float? m_dragStartOffset;
		private Random m_random = new Random();

		public RepeatingCrossbowWidget(IInventory inventory, int slotIndex)
		{
			m_inventory = inventory;
			m_slotIndex = slotIndex;

			XElement node = ContentManager.Get<XElement>("Widgets/RepeatingCrossbowWidget");
			LoadContents(this, node);

			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot", true);
			m_instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel", true);
			m_ammoLabel = Children.Find<LabelWidget>("AmmoLabel", true);

			for (int y = 0; y < m_inventoryGrid.RowsCount; y++)
				for (int x = 0; x < m_inventoryGrid.ColumnsCount; x++)
				{
					var slot = new InventorySlotWidget();
					m_inventoryGrid.Children.Add(slot);
					m_inventoryGrid.SetWidgetCell(slot, new Point2(x, y));
				}

			int slotNumber = 10;
			foreach (var slot in m_inventoryGrid.Children)
				if (slot is InventorySlotWidget isSlot)
					isSlot.AssignInventorySlot(inventory, slotNumber++);

			m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			m_inventorySlotWidget.CustomViewMatrix = Matrix.CreateLookAt(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, 0.2f), -Vector3.UnitZ);
		}

		public override void Update()
		{
			int slotValue = m_inventory.GetSlotValue(m_slotIndex);
			int slotCount = m_inventory.GetSlotCount(m_slotIndex);
			if (slotCount == 0 || Terrain.ExtractContents(slotValue) != RepeatingCrossbowBlock.Index)
			{
				ParentWidget?.Children.Remove(this);
				return;
			}

			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatingCrossbowBlock.GetDraw(data);
			int boltCount = RepeatingCrossbowBlock.GetBoltCount(data);
			int boltType = RepeatingCrossbowBlock.GetBoltType(data);
			string boltName = boltCount > 0 ? ((ArrowBlock.ArrowType)boltType).ToString() : "Ninguno";

			m_ammoLabel.Text = $"{boltName}: {boltCount}/8";

			// Mismo comportamiento que CrossbowWidget original
			if (draw < 15)
			{
				m_instructionsLabel.Text = "Tensa la ballesta";
			}
			else
			{
				m_instructionsLabel.Text = (boltCount == 0) ? "Arrastra virotes aquí para cargar" : "Lista para disparar";
			}

			// Arrastre para tensar - IDENTICO al CrossbowWidget original
			if ((draw < 15 || boltCount == 0) && base.Input.Tap != null && HitTestGlobal(base.Input.Tap.Value, null) == m_inventorySlotWidget)
			{
				Vector2 localPos = m_inventorySlotWidget.ScreenToWidget(base.Input.Press.Value);
				float drawPosY = CrossbowWidget.DrawToPosition(draw);
				float delta = localPos.Y - drawPosY;
				if (MathF.Abs(localPos.X - m_inventorySlotWidget.ActualSize.X / 2f) < 25f && MathF.Abs(delta) < 25f)
					m_dragStartOffset = delta;
			}

			if (m_dragStartOffset.HasValue)
			{
				if (base.Input.Press != null)
				{
					int newDraw = CrossbowWidget.PositionToDraw(m_inventorySlotWidget.ScreenToWidget(base.Input.Press.Value).Y - m_dragStartOffset.Value);
					SetDraw(newDraw);
					if (draw <= 9 && newDraw > 9)
						AudioManager.PlaySound("Audio/CrossbowDraw", 1f, m_random.Float(-0.2f, 0.2f), 0f);
				}
				else
				{
					m_dragStartOffset = null;
					if (draw == 15)
						AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
					else
					{
						SetDraw(0);
						AudioManager.PlaySound("Audio/CrossbowBoing", MathUtils.Saturate((draw - 3) / 10f), m_random.Float(-0.1f, 0.1f), 0f);
					}
				}
			}
		}

		private void SetDraw(int draw)
		{
			int data = Terrain.ExtractData(m_inventory.GetSlotValue(m_slotIndex));
			int newData = RepeatingCrossbowBlock.SetDraw(data, draw);
			m_inventory.RemoveSlotItems(m_slotIndex, 1);
			m_inventory.AddSlotItems(m_slotIndex, Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, 0, newData), 1);
		}
	}
}