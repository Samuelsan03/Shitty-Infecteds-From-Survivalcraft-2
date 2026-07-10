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

		public ShittyInfectedsSettingsScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/ShittyInfectedsSettingsScreen");
			LoadContents(this, node);

			m_settingsContainer = Children.Find<StackPanelWidget>("SettingsContainer", true);

			// Configuración 1: Solo descripción
			m_enableCreatureAttacksButton = AddToggleButton(
				"EnableCreatureAttacks",
				"When hitting a creature, your herd allies will attack it"
			);

			// Configuración 2: Solo descripción
			m_attackOnHitCreativeButton = AddToggleButton(
				"AttackOnHitCreative",
				"When a creature hits you in creative, your allies will attack it"
			);
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
				// SOLUCIÓN: Activar el ajuste de línea automático del motor
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
			// Toggle Enable Creature Attacks
			if (m_enableCreatureAttacksButton.IsClicked)
			{
				ShittyInfectedsSettings.EnableCreatureAttacks = !ShittyInfectedsSettings.EnableCreatureAttacks;

				// GUARDAR INMEDIATAMENTE AL CAMBIAR
				ShittyInfectedsSettingsManager.Save();
			}
			m_enableCreatureAttacksButton.Text = ShittyInfectedsSettings.EnableCreatureAttacks
				? LanguageControl.On
				: LanguageControl.Off;

			// Toggle Attack On Hit Creative
			if (m_attackOnHitCreativeButton.IsClicked)
			{
				ShittyInfectedsSettings.AttackOnHitCreative = !ShittyInfectedsSettings.AttackOnHitCreative;

				// GUARDAR INMEDIATAMENTE AL CAMBIAR
				ShittyInfectedsSettingsManager.Save();
			}
			m_attackOnHitCreativeButton.Text = ShittyInfectedsSettings.AttackOnHitCreative
				? LanguageControl.On
				: LanguageControl.Off;

			// Botón de volver
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.GoBack();
			}
		}

		public const string fName = "ShittyInfectedsSettingsScreen";
	}
}
