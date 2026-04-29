using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class ShittyInfectedsSettingsScreen : Screen
	{
		private ButtonWidget m_herdAttackOnPlayerHitButton;
		private ButtonWidget m_herdAttackOnPlayerInjuryCreativeButton;

		public ShittyInfectedsSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyInfectedsSettingsScreen");
			this.LoadContents(this, node);

			m_herdAttackOnPlayerHitButton = this.Children.Find<ButtonWidget>("HerdAttackOnPlayerHit", true);
			m_herdAttackOnPlayerInjuryCreativeButton = this.Children.Find<ButtonWidget>("HerdAttackOnPlayerInjuryCreative", true);
		}

		public override void Update()
		{
			// Toggle Herd Attack on Player Hit
			if (m_herdAttackOnPlayerHitButton.IsClicked)
			{
				ShittyInfectedsModLoader.HerdAttackOnPlayerHitEnabled = !ShittyInfectedsModLoader.HerdAttackOnPlayerHitEnabled;
			}

			// Toggle Herd Attack on Player Injury (Creative)
			if (m_herdAttackOnPlayerInjuryCreativeButton.IsClicked)
			{
				ShittyInfectedsModLoader.HerdAttackOnPlayerInjuryCreativeEnabled = !ShittyInfectedsModLoader.HerdAttackOnPlayerInjuryCreativeEnabled;
			}

			// Update button texts with On/Off
			m_herdAttackOnPlayerHitButton.Text = ShittyInfectedsModLoader.HerdAttackOnPlayerHitEnabled
				? LanguageControl.On
				: LanguageControl.Off;

			m_herdAttackOnPlayerInjuryCreativeButton.Text = ShittyInfectedsModLoader.HerdAttackOnPlayerInjuryCreativeEnabled
				? LanguageControl.On
				: LanguageControl.Off;

			// Back navigation
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.SwitchScreen("MainMenu");
			}
		}
	}
}
