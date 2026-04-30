using System;
using System.Xml.Linq;
using Engine;
using Game;

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
		public Game.Random m_random = new Game.Random();
		public static string fName = "RepeatCrossbowWidget";

		public RepeatCrossbowWidget(IInventory inventory, int slotIndex)
		{
			m_inventory = inventory;
			m_slotIndex = slotIndex;
			LoadContents(this, ContentManager.Get<XElement>("Widgets/RepeatingCrossbowWidget"));

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

			int num = 10;
			foreach (Widget widget2 in m_inventoryGrid.Children)
			{
				InventorySlotWidget inventorySlotWidget = widget2 as InventorySlotWidget;
				if (inventorySlotWidget != null)
				{
					inventorySlotWidget.AssignInventorySlot(inventory, num++);
				}
			}

			m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, 0.2f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = m_inventory.GetSlotValue(m_slotIndex);
			int slotCount = m_inventory.GetSlotCount(m_slotIndex);
			int num = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatingCrossbowBlock.GetDraw(data);
			int? arrowType = RepeatingCrossbowBlock.GetArrowType(data);

			if (!(BlocksManager.Blocks[num] is RepeatingCrossbowBlock) || slotCount <= 0)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			if (draw < 15)
			{
				m_instructionsLabel.Text = LanguageControl.GetContentWidgets("RepeatingCrossbowWidget", 0);
			}
			else if (arrowType == null)
			{
				m_instructionsLabel.Text = LanguageControl.GetContentWidgets("RepeatingCrossbowWidget", 1);
			}
			else
			{
				int loadCount = RepeatingCrossbowBlock.GetLoadCount(slotValue);
				string baseText = LanguageControl.GetContentWidgets("RepeatingCrossbowWidget", 2);
				m_instructionsLabel.Text = baseText.Replace("{0}", loadCount.ToString());
			}

			if ((draw < 15 || arrowType == null) && Input.Tap != null && HitTestGlobal(Input.Tap.Value, null) == m_inventorySlotWidget)
			{
				InventorySlotWidget inventorySlotWidget = m_inventorySlotWidget;
				Vector2? press = Input.Press;
				if (press != null)
				{
					Vector2 vector = inventorySlotWidget.ScreenToWidget(press.Value);
					float value = vector.Y - DrawToPosition(draw);
					if (Math.Abs(vector.X - m_inventorySlotWidget.ActualSize.X / 2f) < 25.0 && Math.Abs(value) < 25.0)
					{
						m_dragStartOffset = new float?(value);
					}
				}
			}

			if (m_dragStartOffset == null)
				return;

			if (Input.Press != null)
			{
				InventorySlotWidget inventorySlotWidget2 = m_inventorySlotWidget;
				Vector2? press = Input.Press;
				if (press != null)
				{
					int num2 = PositionToDraw(inventorySlotWidget2.ScreenToWidget(press.Value).Y - m_dragStartOffset.Value);
					SetDraw(num2);
					if (draw > 9 || num2 <= 9)
						return;
				}
				AudioManager.PlaySound("Audio/CrossbowDraw", 1f, m_random.Float(-0.2f, 0.2f), 0f);
				return;
			}

			m_dragStartOffset = null;
			if (draw == 15)
			{
				AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
				return;
			}

			SetDraw(0);
			AudioManager.PlaySound("Audio/CrossbowBoing", MathUtils.Saturate((float)(draw - 3) / 10f), m_random.Float(-0.1f, 0.1f), 0f);
		}

		public void SetDraw(int draw)
		{
			int slotValue = m_inventory.GetSlotValue(m_slotIndex);
			int currentLoad = RepeatingCrossbowBlock.GetLoadCount(slotValue);
			int currentData = Terrain.ExtractData(slotValue);
			int? currentArrowType = RepeatingCrossbowBlock.GetArrowType(currentData);

			int newData = RepeatingCrossbowBlock.SetDraw(currentData, draw);

			// Preservar el tipo de flecha si existe
			if (currentArrowType != null)
			{
				newData = RepeatingCrossbowBlock.SetArrowType(newData, currentArrowType);
			}

			// Si se está destensando (draw=0), perder la munición
			if (draw == 0)
			{
				m_inventory.RemoveSlotItems(m_slotIndex, 1);
				m_inventory.AddSlotItems(m_slotIndex, Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, 0, newData), 1);
			}
			else
			{
				// Al tensar, mantener la carga actual
				m_inventory.RemoveSlotItems(m_slotIndex, 1);
				m_inventory.AddSlotItems(m_slotIndex, Terrain.MakeBlockValue(RepeatingCrossbowBlock.Index, currentLoad, newData), 1);
			}
		}

		public static float DrawToPosition(int draw)
		{
			return (float)((double)draw * 5.4 + 85.0);
		}

		public static int PositionToDraw(float position)
		{
			return (int)Math.Clamp(Math.Round((position - 85f) / 5.4f), 0.0, 15.0);
		}
	}
}