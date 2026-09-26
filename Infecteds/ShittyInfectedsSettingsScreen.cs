using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class ShittyInfectedsSettingsScreen : Screen
	{
		private ButtonWidget m_enableCreatureAttacksButton;
		private ButtonWidget m_attackOnHitCreativeButton;
		private ButtonWidget m_showCoordinatesButton;
		private ButtonWidget m_showCreatureHealthBarsButton;
		private ButtonWidget m_enableCreatureBleedingButton;
		private ButtonWidget m_enableFreeCameraButton;
		private ButtonWidget m_enableBossChaseMusicButton;
		private ButtonWidget m_enableDeathSpawnButton;
		private ButtonWidget m_enableGhostChaseMusicButton;
		private ButtonWidget m_enableDeathMusicButton;

		public ShittyInfectedsSettingsScreen()
		{
			ShittyInfectedsSettingsManager.Load();

			XElement node = ContentManager.Get<XElement>("Screens/ShittyInfectedsSettingsScreen");
			LoadContents(this, node);

			Children.Find<LabelWidget>("TopBar.Label", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 1);

			Children.Find<LabelWidget>("EnableCreatureAttacksLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 2);
			m_enableCreatureAttacksButton = Children.Find<ButtonWidget>("EnableCreatureAttacks", true);

			Children.Find<LabelWidget>("AttackOnHitCreativeLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 3);
			m_attackOnHitCreativeButton = Children.Find<ButtonWidget>("AttackOnHitCreative", true);

			Children.Find<LabelWidget>("ShowCoordinatesLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 4);
			m_showCoordinatesButton = Children.Find<ButtonWidget>("ShowCoordinates", true);

			Children.Find<LabelWidget>("ShowCreatureHealthBarsLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 5);
			m_showCreatureHealthBarsButton = Children.Find<ButtonWidget>("ShowCreatureHealthBars", true);

			Children.Find<LabelWidget>("EnableCreatureBleedingLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 6);
			m_enableCreatureBleedingButton = Children.Find<ButtonWidget>("EnableCreatureBleeding", true);

			Children.Find<LabelWidget>("EnableFreeCameraLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 7);
			m_enableFreeCameraButton = Children.Find<ButtonWidget>("EnableFreeCamera", true);

			Children.Find<LabelWidget>("EnableBossChaseMusicLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 8);
			m_enableBossChaseMusicButton = Children.Find<ButtonWidget>("EnableBossChaseMusic", true);
			m_enableBossChaseMusicButton.ColorTransform = new Color(255, 140, 0);

			Children.Find<LabelWidget>("EnableDeathSpawnLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 9);
			m_enableDeathSpawnButton = Children.Find<ButtonWidget>("EnableDeathSpawn", true);
			m_enableDeathSpawnButton.ColorTransform = new Color(180, 40, 60);

			Children.Find<LabelWidget>("EnableGhostChaseMusicLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 10);
			m_enableGhostChaseMusicButton = Children.Find<ButtonWidget>("EnableGhostChaseMusic", true);
			m_enableGhostChaseMusicButton.ColorTransform = new Color(100, 220, 220);

			Children.Find<LabelWidget>("EnableDeathMusicLabel", true).Text = LanguageControl.Get("ShittyInfectedsSettingsScreen", 11);
			m_enableDeathMusicButton = Children.Find<ButtonWidget>("EnableDeathMusic", true);
			m_enableDeathMusicButton.ColorTransform = new Color(200, 30, 30);
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
					InfectedsMusicManager.Stop(InfectedsMusicManager.MusicType.BossChase);
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

			if (m_enableGhostChaseMusicButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableGhostChaseMusic = !ShittyInfectedsSettings.EnableGhostChaseMusic;
				ShittyInfectedsSettingsManager.Save();

				if (!ShittyInfectedsSettings.EnableGhostChaseMusic)
				{
					InfectedsMusicManager.Stop(InfectedsMusicManager.MusicType.Chase);
				}
			}
			m_enableGhostChaseMusicButton.Text = ShittyInfectedsSettings.EnableGhostChaseMusic
				? LanguageControl.On
				: LanguageControl.Off;

			if (m_enableDeathMusicButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableDeathMusic = !ShittyInfectedsSettings.EnableDeathMusic;
				ShittyInfectedsSettingsManager.Save();

				if (!ShittyInfectedsSettings.EnableDeathMusic)
				{
					InfectedsMusicManager.Stop(InfectedsMusicManager.MusicType.Death);
				}
			}
			m_enableDeathMusicButton.Text = ShittyInfectedsSettings.EnableDeathMusic
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
