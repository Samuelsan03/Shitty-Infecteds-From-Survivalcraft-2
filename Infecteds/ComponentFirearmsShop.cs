using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFirearmsShop : Component
	{
		public class ShopItem
		{
			public int BlockValue;
			public int Price;
		}

		private List<ShopItem> m_shopItems = new List<ShopItem>();
		private SubsystemGameInfo m_subsystemGameInfo;
		private double m_lastRestorationTime;
		private float m_restorationTime;
		private List<string> m_itemDefinitions;
		private Random m_random = new Random();
		private string m_itemsSellString;
		private int m_coinBlockIndex;

		public List<ShopItem> ShopItems
		{
			get
			{
				return m_shopItems;
			}
		}

		public bool IsEntityAlive
		{
			get
			{
				ComponentHealth componentHealth = Entity.FindComponent<ComponentHealth>();
				return componentHealth == null || componentHealth.Health > 0f;
			}
		}

		private double GetWorldTime()
		{
			if (m_subsystemGameInfo != null)
			{
				return m_subsystemGameInfo.TotalElapsedGameTime;
			}
			return 0.0;
		}

		private IInventory GetPlayerInventory(ComponentPlayer player)
		{
			if (player == null || player.ComponentMiner == null)
			{
				return null;
			}
			return player.ComponentMiner.Inventory;
		}

		public int GetPlayerCoinCount(ComponentPlayer player)
		{
			IInventory inventory = GetPlayerInventory(player);
			if (inventory == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == m_coinBlockIndex)
				{
					num += inventory.GetSlotCount(i);
				}
			}
			return num;
		}

		// 0 = éxito, 1 = sin monedas, 2 = inventario lleno
		public int TryPurchaseItemWithStatus(ComponentPlayer player, int itemIndex)
		{
			IInventory inventory = GetPlayerInventory(player);
			if (inventory == null)
			{
				return 3;
			}

			if (itemIndex < 0 || itemIndex >= m_shopItems.Count)
			{
				return 3;
			}

			ShopItem shopItem = m_shopItems[itemIndex];
			int playerCoinCount = GetPlayerCoinCount(player);
			if (playerCoinCount < shopItem.Price)
			{
				return 1;
			}

			if (ComponentInventoryBase.FindAcquireSlotForItem(inventory, shopItem.BlockValue) == -1)
			{
				return 2;
			}

			int num = shopItem.Price;
			for (int i = 0; i < inventory.SlotsCount && num > 0; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				if (slotValue != 0 && Terrain.ExtractContents(slotValue) == m_coinBlockIndex)
				{
					int slotCount = inventory.GetSlotCount(i);
					int num2 = MathUtils.Min(slotCount, num);
					inventory.RemoveSlotItems(i, num2);
					num -= num2;
				}
			}

			ComponentInventoryBase.AcquireItems(inventory, shopItem.BlockValue, 1);
			return 0;
		}

		public bool TryPurchaseItem(ComponentPlayer player, int itemIndex)
		{
			return TryPurchaseItemWithStatus(player, itemIndex) == 0;
		}

		public string GetRestorationTimeFormatted()
		{
			if (m_restorationTime <= 0f)
			{
				return "N/A";
			}

			double currentTime = GetWorldTime();
			double elapsed = currentTime - m_lastRestorationTime;
			double remaining = (double)m_restorationTime - elapsed;

			if (remaining <= 0.0)
			{
				m_lastRestorationTime = GetWorldTime();
				remaining = (double)m_restorationTime;
			}

			int minutes = (int)(remaining / 60.0);
			int seconds = (int)(remaining % 60.0);
			return string.Format("{0}:{1:D2}", minutes, seconds);
		}

		public void OpenShop(ComponentPlayer player)
		{
			if (player == null || player.ComponentGui == null)
			{
				return;
			}

			if (!IsEntityAlive)
			{
				return;
			}

			player.ComponentGui.ModalPanelWidget = new FirearmsShopWidget(player, this);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_coinBlockIndex = BlocksManager.GetBlockIndex("CoinBlock");
			m_restorationTime = valuesDictionary.GetValue<float>("RestorationTime", 300f);
			m_lastRestorationTime = valuesDictionary.GetValue<double>("LastRestorationTime", -1.0);
			m_itemsSellString = valuesDictionary.GetValue<string>("ItemsSell", "");
			m_itemDefinitions = ParseItemDefinitions(m_itemsSellString);

			if (m_lastRestorationTime < 0.0)
			{
				m_lastRestorationTime = GetWorldTime();
			}

			RefreshShopItems();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<float>("RestorationTime", m_restorationTime);
			valuesDictionary.SetValue<double>("LastRestorationTime", m_lastRestorationTime);
		}

		private List<string> ParseItemDefinitions(string itemsSell)
		{
			List<string> list = new List<string>();
			if (string.IsNullOrEmpty(itemsSell))
			{
				return list;
			}
			string[] array = itemsSell.Split(new char[]
			{
				';'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(text);
				}
			}
			return list;
		}

		private void RefreshShopItems()
		{
			m_shopItems.Clear();

			if (m_itemDefinitions == null || m_itemDefinitions.Count == 0)
			{
				return;
			}

			int total = m_itemDefinitions.Count;
			int minItems = Math.Min(8, total);
			int maxItems = m_random.Int(minItems, total);
			List<string> shuffled = m_itemDefinitions.OrderBy(x => m_random.Int()).ToList();

			for (int i = 0; i < maxItems; i++)
			{
				ShopItem shopItem = ParseShopItem(shuffled[i]);
				if (shopItem != null && shopItem.BlockValue != 0)
				{
					m_shopItems.Add(shopItem);
				}
			}
		}

		private ShopItem ParseShopItem(string definition)
		{
			string[] array = definition.Split(':');
			if (array.Length < 3)
			{
				return null;
			}
			string text = array[0].Trim();
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			if (!int.TryParse(array[2].Trim(), out int num) || num <= 0)
			{
				return null;
			}
			Block block = BlocksManager.GetBlock(text, false);
			if (block == null)
			{
				return null;
			}
			return new ShopItem
			{
				BlockValue = Terrain.MakeBlockValue(block.BlockIndex),
				Price = num
			};
		}
	}
}
