using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class FlameThrowerWidget : CanvasWidget
	{
		private readonly IInventory m_inventory;
		private readonly int m_slotIndex;
		private GridPanelWidget m_inventoryGrid;
		private InventorySlotWidget m_inventorySlotWidget;
		private LabelWidget m_instructionsLabel;

		public FlameThrowerWidget(IInventory inventory, int slotIndex)
		{
			m_inventory = inventory;
			m_slotIndex = slotIndex;

			XElement node = ContentManager.Get<XElement>("Widgets/FlameThrowerWidget");
			LoadContents(this, node);

			m_inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid", true);
			m_inventorySlotWidget = Children.Find<InventorySlotWidget>("InventorySlot", true);
			m_instructionsLabel = Children.Find<LabelWidget>("InstructionsLabel", true);

			int slot = 10;
			for (int r = 0; r < m_inventoryGrid.RowsCount; r++)
			{
				for (int c = 0; c < m_inventoryGrid.ColumnsCount; c++)
				{
					var widget = new InventorySlotWidget();
					m_inventoryGrid.Children.Add(widget);
					m_inventoryGrid.SetWidgetCell(widget, new Point2(c, r));
					widget.AssignInventorySlot(inventory, slot++);
				}
			}

			m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			m_inventorySlotWidget.CustomViewMatrix = Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), Vector3.Zero, -Vector3.UnitZ);
		}

		public override void Update()
		{
			int value = m_inventory.GetSlotValue(m_slotIndex);
			int count = m_inventory.GetSlotCount(m_slotIndex);

			if (Terrain.ExtractContents(value) != FlameThrowerBlock.Index || count <= 0)
			{
				ParentWidget?.Children.Remove(this);
				return;
			}

			int data = Terrain.ExtractData(value);
			var state = FlameThrowerBlock.GetLoadState(data);
			int ammo = FlameThrowerBlock.GetAmmoCount(data);
			int bulletType = (data >> 8) & 3;

			if (state == FlameThrowerBlock.LoadState.Empty || ammo == 0)
			{
				m_instructionsLabel.Text = LanguageControl.GetContentWidgets("FlameThrowerWidget", 2);
			}
			else
			{
				string ammoType = LanguageControl.GetContentWidgets("FlameThrowerWidget", bulletType == 0 ? 4 : 5);
				string format = LanguageControl.GetContentWidgets("FlameThrowerWidget", 3); // "{ammo}/15 {type}"
				m_instructionsLabel.Text = format.Replace("{ammo}", ammo.ToString()).Replace("{type}", ammoType);
			}
		}
	}
}
