using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class FirearmsShopWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private ComponentFirearmsShop m_shopComponent;
		private ButtonWidget m_closeButton;
		private ScrollPanelWidget m_scrollPanel;
		private LabelWidget m_restorationLabel;
		private SubsystemAudio m_subsystemAudio;

		private List<ShopItemWidget> m_itemWidgets = new List<ShopItemWidget>();
		private float m_itemHeight = 70f;
		private float m_itemSpacing = 4f;

		public FirearmsShopWidget(ComponentPlayer componentPlayer, ComponentFirearmsShop shopComponent)
		{
			m_componentPlayer = componentPlayer;
			m_shopComponent = shopComponent;

			XElement node = ContentManager.Get<XElement>("Widgets/FirearmsShopWidget");
			LoadContents(this, node);

			m_closeButton = Children.Find<ButtonWidget>("CloseButton", true);
			m_scrollPanel = Children.Find<ScrollPanelWidget>("ItemsScrollPanel", true);
			m_restorationLabel = Children.Find<LabelWidget>("RestorationLabel", true);

			m_subsystemAudio = componentPlayer?.Project?.FindSubsystem<SubsystemAudio>(true);
			m_subsystemAudio?.PlaySound("Audio/cortina abriendo", 1f, 0f, 0f, 0f);

			PopulateShopItems();
		}

		private void PopulateShopItems()
		{
			m_itemWidgets.Clear();

			if (m_scrollPanel != null)
			{
				m_scrollPanel.Children.Clear();
			}

			if (m_shopComponent == null || m_shopComponent.ShopItems == null)
				return;

			float yPosition = 0f;

			for (int i = 0; i < m_shopComponent.ShopItems.Count; i++)
			{
				var item = m_shopComponent.ShopItems[i];

				ShopItemWidget itemWidget = new ShopItemWidget(m_componentPlayer, m_shopComponent, i, item);
				CanvasWidget.SetPosition(itemWidget, new Vector2(5f, yPosition));

				m_itemWidgets.Add(itemWidget);
				m_scrollPanel?.Children.Add(itemWidget);

				yPosition += m_itemHeight + m_itemSpacing;
			}
		}

		public override void Update()
		{
			if (m_closeButton != null && m_closeButton.IsClicked)
			{
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
				return;
			}

			if (m_restorationLabel != null && m_shopComponent != null)
			{
				m_restorationLabel.Text = $"Restaura en: {m_shopComponent.GetRestorationTimeFormatted()}";
			}

			for (int i = 0; i < m_itemWidgets.Count; i++)
			{
				if (i < m_shopComponent.ShopItems.Count)
				{
					if (m_itemWidgets[i].IsBuyButtonClicked())
					{
						HandlePurchase(i);
					}

					m_itemWidgets[i].UpdateItemData(m_shopComponent.ShopItems[i]);
				}
			}
		}

		private void HandlePurchase(int itemIndex)
		{
			if (m_shopComponent == null || itemIndex >= m_shopComponent.ShopItems.Count)
				return;

			var item = m_shopComponent.ShopItems[itemIndex];

			int playerCoins = m_shopComponent.GetPlayerCoinCount(m_componentPlayer);
			if (playerCoins < item.Price)
				return;

			if (m_shopComponent.TryPurchaseItem(m_componentPlayer, itemIndex))
			{
				m_subsystemAudio?.PlaySound("Audio/cash", 1f, 0f, 0f, 0f);
				m_componentPlayer.ComponentGui.DisplaySmallMessage("¡Compra exitosa!", new Color(100, 255, 100), true, true);
			}
		}
	}

	public class ShopItemWidget : CanvasWidget
	{
		private ComponentPlayer m_player;
		private ComponentFirearmsShop m_shop;
		private int m_itemIndex;

		private BlockIconWidget m_blockIcon;
		private LabelWidget m_nameLabel;
		private LabelWidget m_priceLabel;
		private LabelWidget m_buyLabel;
		private BevelledButtonWidget m_buyButton;
		private BevelledRectangleWidget m_background;

		private static readonly Color ColorDisabled = new Color(112, 112, 112);
		private static readonly Color ColorEnabled = Color.White;

		public ShopItemWidget(ComponentPlayer player, ComponentFirearmsShop shop, int index, ComponentFirearmsShop.ShopItem item)
		{
			m_player = player;
			m_shop = shop;
			m_itemIndex = index;

			Size = new Vector2(525f, 65f);

			m_background = new BevelledRectangleWidget
			{
				Size = new Vector2(525f, 65f),
				BevelSize = 2f
			};
			Children.Add(m_background);

			m_blockIcon = new BlockIconWidget
			{
				Value = item.BlockValue,
				Size = new Vector2(40f, 40f),
				Scale = 1f
			};
			Children.Add(m_blockIcon);
			CanvasWidget.SetPosition(m_blockIcon, new Vector2(50f, 5f));

			string displayName = GetBlockDisplayName(item.BlockValue);

			m_nameLabel = new LabelWidget
			{
				Text = displayName,
				Color = Color.White
			};
			m_nameLabel.Size = new Vector2(150f, 16f);
			Children.Add(m_nameLabel);
			CanvasWidget.SetPosition(m_nameLabel, new Vector2(8f, 35f));

			m_priceLabel = new LabelWidget
			{
				Text = $"{item.Price} monedas",
				Color = new Color(255, 215, 0),
				VerticalAlignment = WidgetAlignment.Center
			};
			m_priceLabel.Size = new Vector2(120f, 20f);
			Children.Add(m_priceLabel);
			CanvasWidget.SetPosition(m_priceLabel, new Vector2(260f, 20f));

			m_buyButton = new BevelledButtonWidget
			{
				Size = new Vector2(107f, 38f),
				IsEnabled = true
			};

			m_buyLabel = new LabelWidget
			{
				Text = "Comprar",
				Color = ColorEnabled,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center
			};
			m_buyButton.Children.Add(m_buyLabel);

			Children.Add(m_buyButton);
			CanvasWidget.SetPosition(m_buyButton, new Vector2(417f, 14f));
		}

		public bool IsBuyButtonClicked()
		{
			return m_buyButton != null && m_buyButton.IsClicked;
		}

		private string GetBlockDisplayName(int blockValue)
		{
			int blockIndex = Terrain.ExtractContents(blockValue);
			if (blockIndex >= 0 && blockIndex < BlocksManager.Blocks.Length)
			{
				Block block = BlocksManager.Blocks[blockIndex];
				if (block != null)
				{
					SubsystemTerrain subsystemTerrain = m_player?.Project?.FindSubsystem<SubsystemTerrain>(true);
					if (subsystemTerrain != null)
					{
						return block.GetDisplayName(subsystemTerrain, blockValue);
					}
					return block.DefaultDisplayName;
				}
			}
			return "Bloque desconocido";
		}

		public void UpdateItemData(ComponentFirearmsShop.ShopItem item)
		{
			if (m_blockIcon != null)
			{
				m_blockIcon.Value = item.BlockValue;
			}

			if (m_nameLabel != null)
			{
				m_nameLabel.Text = GetBlockDisplayName(item.BlockValue);
			}

			if (m_priceLabel != null)
			{
				m_priceLabel.Text = $"{item.Price} monedas";
			}

			if (m_buyButton != null && m_buyLabel != null)
			{
				int playerCoins = m_shop?.GetPlayerCoinCount(m_player) ?? 0;
				bool canBuy = playerCoins >= item.Price;

				m_buyButton.IsEnabled = canBuy;
				m_buyLabel.Color = canBuy ? ColorEnabled : ColorDisabled;
			}
		}
	}
}
