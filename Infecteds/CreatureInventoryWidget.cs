using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	// Token: 0x02000XXX RID: XXXX
	public class CreatureInventoryWidget : CanvasWidget
	{
		// Token: 0x0600XXXX RID: XXXX RVA: 0xXXXXXXXX File Offset: 0xXXXXXXXX
		public CreatureInventoryWidget(IInventory inventory, ComponentCreatureInventory componentCreatureInventory)
		{
			this.m_componentCreatureInventory = componentCreatureInventory;

			// Cargamos el XML que creamos
			XElement node = ContentManager.Get<XElement>("Widgets/CreatureInventory");
			this.LoadContents(this, node);

			// Buscamos las grillas por su nombre en el XML
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_creatureGrid = this.Children.Find<GridPanelWidget>("CreatureGrid", true);

			// Generamos los slots de la criatura
			int num = 0;
			for (int i = 0; i < this.m_creatureGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_creatureGrid.ColumnsCount; j++)
				{
					InventorySlotWidget inventorySlotWidget = new InventorySlotWidget();
					inventorySlotWidget.AssignInventorySlot(componentCreatureInventory, num++);
					this.m_creatureGrid.Children.Add(inventorySlotWidget);
					this.m_creatureGrid.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
				}
			}

			// Generamos los slots del inventario del jugador (empezando desde el slot 10, igual que el cofre)
			num = 10;
			for (int k = 0; k < this.m_inventoryGrid.RowsCount; k++)
			{
				for (int l = 0; l < this.m_inventoryGrid.ColumnsCount; l++)
				{
					InventorySlotWidget inventorySlotWidget2 = new InventorySlotWidget();
					inventorySlotWidget2.AssignInventorySlot(inventory, num++);
					this.m_inventoryGrid.Children.Add(inventorySlotWidget2);
					this.m_inventoryGrid.SetWidgetCell(inventorySlotWidget2, new Point2(l, k));
				}
			}
		}

		// Token: 0x0600XXXX RID: XXXX RVA: 0xXXXXXXXX File Offset: 0xXXXXXXXX
		public override void Update()
		{
			// Si la criatura muere o se elimina del mundo, cerramos el inventario
			if (!this.m_componentCreatureInventory.IsAddedToProject)
			{
				base.ParentWidget.Children.Remove(this);
			}
		}

		// Token: 0x0400XXXX RID: XXXX
		public ComponentCreatureInventory m_componentCreatureInventory;

		// Token: 0x0400XXXX RID: XXXX
		public GridPanelWidget m_inventoryGrid;

		// Token: 0x0400XXXX RID: XXXX
		public GridPanelWidget m_creatureGrid;
	}
}
