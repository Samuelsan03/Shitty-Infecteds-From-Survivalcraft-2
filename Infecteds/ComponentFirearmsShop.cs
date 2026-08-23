using System;
using System.Collections.Generic;
using System.Globalization;
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
		private SubsystemTime m_subsystemTime;
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

		public bool TryPurchaseItem(ComponentPlayer player, int itemIndex)
		{
			IInventory inventory = GetPlayerInventory(player);
			if (inventory == null)
			{
				return false;
			}
			if (itemIndex < 0 || itemIndex >= m_shopItems.Count)
			{
				return false;
			}
			ShopItem shopItem = m_shopItems[itemIndex];
			int playerCoinCount = GetPlayerCoinCount(player);
			if (playerCoinCount < shopItem.Price)
			{
				return false;
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
			return true;
		}

		public string GetRestorationTimeFormatted()
		{
			if (m_restorationTime <= 0f)
			{
				return "N/A";
			}
			double num = m_subsystemTime.GameTime - m_lastRestorationTime;
			double num2 = (double)m_restorationTime - num;
			if (num2 <= 0.0)
			{
				return "Ahora";
			}
			int num3 = (int)(num2 / 60.0);
			int num4 = (int)(num2 % 60.0);
			return string.Format("{0}:{1:D2}", num3, num4);
		}

		public void OpenShop(ComponentPlayer player)
		{
			if (player == null || player.ComponentGui == null)
			{
				return;
			}
			player.ComponentGui.ModalPanelWidget = new FirearmsShopWidget(player, this);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_coinBlockIndex = BlocksManager.GetBlockIndex("CoinBlock");
			m_restorationTime = valuesDictionary.GetValue<float>("RestorationTime", 300f);
			m_lastRestorationTime = valuesDictionary.GetValue<double>("LastRestorationTime", 0.0);
			m_itemsSellString = valuesDictionary.GetValue<string>("ItemsSell", "");
			m_itemDefinitions = ParseItemDefinitions(m_itemsSellString);
			bool flag = m_lastRestorationTime == 0.0 || (m_subsystemTime != null && m_subsystemTime.GameTime - m_lastRestorationTime >= (double)m_restorationTime);
			if (flag)
			{
				RefreshShopItems();
				m_lastRestorationTime = ((m_subsystemTime != null) ? m_subsystemTime.GameTime : 0.0);
			}
			else
			{
				LoadShopItems(valuesDictionary);
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<float>("RestorationTime", m_restorationTime);
			valuesDictionary.SetValue<double>("LastRestorationTime", m_lastRestorationTime);
			valuesDictionary.SetValue<string>("ItemsSell", m_itemsSellString);
			ValuesDictionary valuesDictionary2 = new ValuesDictionary();
			valuesDictionary.SetValue<ValuesDictionary>("ShopItems", valuesDictionary2);
			for (int i = 0; i < m_shopItems.Count; i++)
			{
				ValuesDictionary valuesDictionary3 = new ValuesDictionary();
				valuesDictionary3.SetValue<int>("BlockValue", m_shopItems[i].BlockValue);
				valuesDictionary3.SetValue<int>("Price", m_shopItems[i].Price);
				valuesDictionary2.SetValue<ValuesDictionary>("Item" + i.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
			}
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
			int num = m_itemDefinitions.Count;
			int num2 = Math.Min(3, num);
			int num3 = m_random.Int(num2, num);
			List<string> list = m_itemDefinitions.OrderBy((string x) => m_random.Int()).ToList();
			for (int i = 0; i < num3; i++)
			{
				ShopItem shopItem = ParseShopItem(list[i]);
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

		private void LoadShopItems(ValuesDictionary valuesDictionary)
		{
			m_shopItems.Clear();
			ValuesDictionary valuesDictionary2 = valuesDictionary.GetValue<ValuesDictionary>("ShopItems", null);
			if (valuesDictionary2 == null)
			{
				RefreshShopItems();
				return;
			}
			int num = 0;
			while (true)
			{
				ValuesDictionary valuesDictionary3 = valuesDictionary2.GetValue<ValuesDictionary>("Item" + num.ToString(CultureInfo.InvariantCulture), null);
				if (valuesDictionary3 == null)
				{
					break;
				}
				m_shopItems.Add(new ShopItem
				{
					BlockValue = valuesDictionary3.GetValue<int>("BlockValue"),
					Price = valuesDictionary3.GetValue<int>("Price")
				});
				num++;
			}
		}
	}
}
