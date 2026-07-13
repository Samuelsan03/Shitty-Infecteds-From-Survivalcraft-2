using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class ImprovedMusketWidget : CanvasWidget
	{
		public ImprovedMusketWidget(IInventory inventory, int slotIndex)
		{
			this.m_inventory = inventory;
			this.m_slotIndex = slotIndex;
			XElement node = ContentManager.Get<XElement>("Widgets/ImprovedMusketWidget");
			this.LoadContents(this, node);
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
			this.m_instructionsLabel = this.Children.Find<LabelWidget>("InstructionsLabel", true);
			for (int i = 0; i < this.m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget widget = new InventorySlotWidget();
					this.m_inventoryGrid.Children.Add(widget);
					this.m_inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
				}
			}
			int num = 10;
			foreach (Widget widget2 in this.m_inventoryGrid.Children)
			{
				InventorySlotWidget inventorySlotWidget = widget2 as InventorySlotWidget;
				if (inventorySlotWidget != null)
				{
					inventorySlotWidget.AssignInventorySlot(inventory, num++);
				}
			}
			this.m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			this.m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			int slotCount = this.m_inventory.GetSlotCount(this.m_slotIndex);
			if (Terrain.ExtractContents(slotValue) != ImprovedMusketBlock.Index || slotCount <= 0)
			{
				base.ParentWidget.Children.Remove(this);
				return;
			}
			int ammoCount = ImprovedMusketBlock.GetAmmoCount(Terrain.ExtractData(slotValue));
			if (ammoCount == 0)
			{
				this.m_instructionsLabel.Text = LanguageControl.GetContentWidgets("ImprovedMusketWidget", 2);
			}
			else
			{
				this.m_instructionsLabel.Text = string.Format(LanguageControl.GetContentWidgets("ImprovedMusketWidget", 3), ammoCount);
			}
		}

		public IInventory m_inventory;

		public int m_slotIndex;

		public GridPanelWidget m_inventoryGrid;

		public InventorySlotWidget m_inventorySlotWidget;

		public LabelWidget m_instructionsLabel;
	}
}
