using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class GreenNightConfigDialog : Dialog
	{
		private ComponentPlayer m_player;
		public int SelectedDays { get; private set; }

		public GreenNightConfigDialog(ComponentPlayer player)
		{
			m_player = player;
			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightConfigDialog");
			this.LoadContents(this, node);

			m_okButton = this.Children.Find<ButtonWidget>("GreenNightConfig.OkButton", true);
			m_cancelButton = this.Children.Find<ButtonWidget>("GreenNightConfig.CancelButton", true);
			m_daysButton = this.Children.Find<ButtonWidget>("GreenNightConfig.DaysButton", true);
			m_lifeDaysLabel = this.Children.Find<LabelWidget>("GreenNightConfig.LifeDaysLabel", true);
			m_description = this.Children.Find<LabelWidget>("GreenNightConfig.Description", true);

			SelectedDays = 4;
		}

		public override void Update()
		{
			if (m_okButton.IsClicked)
			{
				if (m_player != null && m_player.ComponentGui != null)
				{
					m_player.ComponentGui.DisplaySmallMessage(
						"La noche verde se estableció en " + SelectedDays + " días",
						new Color(0, 255, 94),
						false,
						true
					);
				}
				DialogsManager.HideDialog(this);
			}
			if (m_cancelButton.IsClicked || base.Input.Cancel)
			{
				DialogsManager.HideDialog(this);
			}
		}

		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private ButtonWidget m_daysButton;
		private LabelWidget m_lifeDaysLabel;
		private LabelWidget m_description;
	}
}
