using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class ShittyInfectedsSettingsScreen : Screen
	{
		private StackPanelWidget m_settingsContainer;

		private ButtonWidget m_enableCreatureAttacksButton;
		private ButtonWidget m_attackOnHitCreativeButton;
		private ButtonWidget m_showCoordinatesButton;
		private ButtonWidget m_showCreatureHealthBarsButton;
		private ButtonWidget m_enableCreatureBleedingButton;
		private ButtonWidget m_enableFreeCameraButton;
		private ButtonWidget m_enableBossChaseMusicButton;
		private ButtonWidget m_enableDeathSpawnButton;

		public ShittyInfectedsSettingsScreen()
		{
			ShittyInfectedsSettingsManager.Load();

			XElement node = ContentManager.Get<XElement>("Screens/ShittyInfectedsSettingsScreen");
			LoadContents(this, node);

			Children.Find<LabelWidget>("TopBar.Label", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 1);

			m_settingsContainer = Children.Find<StackPanelWidget>("SettingsContainer", true);

			m_enableCreatureAttacksButton = AddToggleButton(
				"EnableCreatureAttacks",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 2)
			);

			m_attackOnHitCreativeButton = AddToggleButton(
				"AttackOnHitCreative",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 3)
			);

			m_showCoordinatesButton = AddToggleButton(
				"ShowCoordinates",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 4)
			);

			m_showCreatureHealthBarsButton = AddToggleButton(
				"ShowCreatureHealthBars",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 5)
			);

			m_enableCreatureBleedingButton = AddToggleButton(
				"EnableCreatureBleeding",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 6)
			);

			m_enableFreeCameraButton = AddToggleButton(
				"EnableFreeCamera",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 7)
			);

			m_enableBossChaseMusicButton = AddToggleButton(
				"EnableBossChaseMusic",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 8)
			);
			m_enableBossChaseMusicButton.ColorTransform = new Color(255, 140, 0);

			m_enableDeathSpawnButton = AddToggleButton(
				"EnableDeathSpawn",
				LanguageControl.Get("ShittyInfectedsSettingsScreen", 9)
			);
			m_enableDeathSpawnButton.ColorTransform = new Color(180, 40, 60);
		}

		private ButtonWidget AddToggleButton(string name, string descriptionText)
		{
			UniformSpacingPanelWidget row = new UniformSpacingPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				Margin = new Vector2(0, 3)
			};

			LabelWidget descriptionLabel = new LabelWidget
			{
				Text = descriptionText,
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Color = new Color(180, 180, 180),
				Margin = new Vector2(20, 0),
				WordWrap = true
			};

			BevelledButtonWidget button = new BevelledButtonWidget
			{
				Name = name,
				Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"),
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(20, 0),
				Text = LanguageControl.Off
			};

			row.Children.Add(descriptionLabel);
			row.Children.Add(button);

			m_settingsContainer.Children.Add(row);

			return button;
		}

		public override void Update()
		{
			if (m_enableCreatureAttacksButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableCreatureAttacks = !ShittyInfectedsSettings.EnableCreatureAttacks;
				ShittyInfectedsSettingsManager.Save();
			}
			m_enableCreatureAttacksButton.Text = ShittyInfectedsSettings.EnableCreatureAttacks
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_attackOnHitCreativeButton.IsClicked)
			{
				ShittyInfectedsSettings.AttackOnHitCreative = !ShittyInfectedsSettings.AttackOnHitCreative;
				ShittyInfectedsSettingsManager.Save();
			}
			m_attackOnHitCreativeButton.Text = ShittyInfectedsSettings.AttackOnHitCreative
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_showCoordinatesButton.IsClicked)
			{
				ShittyInfectedsSettings.ShowCoordinates = !ShittyInfectedsSettings.ShowCoordinates;
				ShittyInfectedsSettingsManager.Save();
			}
			m_showCoordinatesButton.Text = ShittyInfectedsSettings.ShowCoordinates
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_showCreatureHealthBarsButton.IsClicked)
			{
				ShittyInfectedsSettings.ShowCreatureHealthBars = !ShittyInfectedsSettings.ShowCreatureHealthBars;
				ShittyInfectedsSettingsManager.Save();
			}
			m_showCreatureHealthBarsButton.Text = ShittyInfectedsSettings.ShowCreatureHealthBars
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_enableCreatureBleedingButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableCreatureBleeding = !ShittyInfectedsSettings.EnableCreatureBleeding;
				ShittyInfectedsSettingsManager.Save();
			}
			m_enableCreatureBleedingButton.Text = ShittyInfectedsSettings.EnableCreatureBleeding
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_enableFreeCameraButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableFreeCamera = !ShittyInfectedsSettings.EnableFreeCamera;
				ShittyInfectedsSettingsManager.Save();
			}
			m_enableFreeCameraButton.Text = ShittyInfectedsSettings.EnableFreeCamera
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_enableBossChaseMusicButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableBossChaseMusic = !ShittyInfectedsSettings.EnableBossChaseMusic;
				ShittyInfectedsSettingsManager.Save();

				if (!ShittyInfectedsSettings.EnableBossChaseMusic)
				{
					BossChaseMusicManager.Stop();
				}
			}
			m_enableBossChaseMusicButton.Text = ShittyInfectedsSettings.EnableBossChaseMusic
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_enableDeathSpawnButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableDeathSpawn = !ShittyInfectedsSettings.EnableDeathSpawn;
				ShittyInfectedsSettingsManager.Save();
			}
			m_enableDeathSpawnButton.Text = ShittyInfectedsSettings.EnableDeathSpawn
				? LanguageControl.On
				: LanguageControl.Off;

			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.GoBack();
			}
		}

		public const string fName = "ShittyInfectedsSettingsScreen";
	}
}
