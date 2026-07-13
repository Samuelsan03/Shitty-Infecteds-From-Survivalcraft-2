using System;
using System.Xml.Linq;
using Engine;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class RepeatCrossbowWidget : CanvasWidget
	{
		public IInventory m_inventory;
		public int m_slotIndex;
		public float? m_dragStartOffset;
		public GridPanelWidget m_inventoryGrid;
		public InventorySlotWidget m_inventorySlotWidget;
		public LabelWidget m_instructionsLabel;
		public Random m_random = new Random();

		public RepeatCrossbowWidget(IInventory inventory, int slotIndex)
		{
			m_inventory = inventory;
			m_slotIndex = slotIndex;

			XElement node = ContentManager.Get<XElement>("Widgets/RepeatCrossbowWidget");
			LoadContents(this, node);

			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot", true);
			m_instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel", true);

			for (int i = 0; i < m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget widget = new InventorySlotWidget();
					m_inventoryGrid.Children.Add(widget);
					m_inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
				}
			}

			int slotNum = 10;
			foreach (Widget widget in m_inventoryGrid.Children)
			{
				InventorySlotWidget slotWidget = widget as InventorySlotWidget;
				if (slotWidget != null)
				{
					slotWidget.AssignInventorySlot(inventory, slotNum++);
				}
			}

			m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, 0.2f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = m_inventory.GetSlotValue(m_slotIndex);
			int slotCount = m_inventory.GetSlotCount(m_slotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatBoltType? boltType = RepeatCrossbowBlock.GetRepeatBoltType(data);
			int count = RepeatCrossbowBlock.GetCount(data);

			if (contents == RepeatCrossbowBlock.Index && slotCount > 0)
			{
				// Actualizar la etiqueta según el estado
				if (draw < 15)
				{
					m_instructionsLabel.Text = "Draw the string";
				}
				else // draw == 15
				{
					if (count > 0)
						m_instructionsLabel.Text = $"{count}/8 Bolts";
					else
						m_instructionsLabel.Text = "Insert bolts"; // Nuestro mensaje personalizado
				}

				// Lógica de arrastre para tensar (solo si no está tensado o está vacío)
				if ((draw < 15 || count == 0) && Input.Tap != null && HitTestGlobal(Input.Tap.Value, null) == m_inventorySlotWidget)
				{
					Vector2 pos = m_inventorySlotWidget.ScreenToWidget(Input.Press.Value);
					float offset = pos.Y - DrawToPosition(draw);
					if (MathF.Abs(pos.X - m_inventorySlotWidget.ActualSize.X / 2f) < 25f && MathF.Abs(offset) < 25f)
					{
						m_dragStartOffset = offset;
					}
				}

				if (m_dragStartOffset == null)
					return;

				if (Input.Press != null)
				{
					int newDraw = PositionToDraw(m_inventorySlotWidget.ScreenToWidget(Input.Press.Value).Y - m_dragStartOffset.Value);
					SetDraw(newDraw);
					if (draw <= 9 && newDraw > 9)
					{
						AudioManager.PlaySound("Audio/Crossbow Remake/Crossbow Loading Remake", 1f, m_random.Float(-0.2f, 0.2f), 0f);
					}
				}
				else
				{
					m_dragStartOffset = null;
					if (draw == 15)
					{
						// Si ya estaba tensado, solo sonido de carga (no se destensa)
						AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
					}
					else
					{
					}
				}
			}
			else
			{
				ParentWidget.Children.Remove(this);
			}
		}

		public void SetDraw(int draw)
		{
			int data = Terrain.ExtractData(m_inventory.GetSlotValue(m_slotIndex));
			int value = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, RepeatCrossbowBlock.SetDraw(data, draw));
			m_inventory.RemoveSlotItems(m_slotIndex, 1);
			m_inventory.AddSlotItems(m_slotIndex, value, 1);
		}

		public static float DrawToPosition(int draw) => draw * 5.4f + 85f;
		public static int PositionToDraw(float position) => (int)Math.Clamp(MathF.Round((position - 85f) / 5.4f), 0f, 15f);
	}
}
