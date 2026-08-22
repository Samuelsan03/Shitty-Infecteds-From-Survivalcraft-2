using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class FirearmsShopWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private ButtonWidget m_closeButton;

		public FirearmsShopWidget(ComponentPlayer componentPlayer)
		{
			m_componentPlayer = componentPlayer;
			XElement node = ContentManager.Get<XElement>("Widgets/FirearmsShopWidget");
			LoadContents(this, node);
			m_closeButton = Children.Find<ButtonWidget>("CloseButton", true);
		}

		public override void Update()
		{
			if (m_closeButton.IsClicked)
			{
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
			}
		}
	}
}
