using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class BestiaryInfectedDescriptionScreen : Screen
	{
		private ModelWidget m_modelWidget;
		private LabelWidget m_nameWidget;
		private ButtonWidget m_leftButtonWidget, m_rightButtonWidget;
		private LabelWidget m_descriptionWidget;
		private LabelWidget m_propertyNames1Widget, m_propertyValues1Widget;
		private LabelWidget m_propertyNames2Widget, m_propertyValues2Widget;
		private ContainerWidget m_dropsPanel;
		private LabelWidget m_dropsLabel;
		private BevelledRectangleWidget m_rainbowBar;
		private BevelledRectangleWidget m_buttonRect;
		private RectangleWidget m_arrowImage;
		private int m_index;
		private IList<BestiaryCreatureInfo> m_infoList;
		private static float s_hue = 0f;

		public BestiaryInfectedDescriptionScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/BestiaryInfectedDescriptionScreen");
			LoadContents(this, node);
			m_modelWidget = Children.Find<ModelWidget>("Model", true);
			m_nameWidget = Children.Find<LabelWidget>("Name", true);
			m_leftButtonWidget = Children.Find<ButtonWidget>("Left", true);
			m_rightButtonWidget = Children.Find<ButtonWidget>("Right", true);
			m_descriptionWidget = Children.Find<LabelWidget>("Description", true);
			m_propertyNames1Widget = Children.Find<LabelWidget>("PropertyNames1", true);
			m_propertyValues1Widget = Children.Find<LabelWidget>("PropertyValues1", true);
			m_propertyNames2Widget = Children.Find<LabelWidget>("PropertyNames2", true);
			m_propertyValues2Widget = Children.Find<LabelWidget>("PropertyValues2", true);
			m_dropsPanel = Children.Find<ContainerWidget>("Drops", true);

			// Obtener el label por su nombre en lugar de por su texto
			m_dropsLabel = Children.Find<LabelWidget>("DropsLabel", true);

			m_rainbowBar = Children.Find<BevelledRectangleWidget>("RainbowBar", true);

			ButtonWidget backButton = Children.Find<ButtonWidget>("TopBar.Back", true);
			m_buttonRect = backButton?.Children.Find<BevelledRectangleWidget>("BevelledButton.Rectangle", true);
			m_arrowImage = backButton?.Children.Find<RectangleWidget>("BevelledButton.Image", true);
		}

		public override void Enter(object[] parameters)
		{
			BestiaryCreatureInfo item = (BestiaryCreatureInfo)parameters[0];
			m_infoList = (IList<BestiaryCreatureInfo>)parameters[1];
			m_index = m_infoList.IndexOf(item);
			UpdateCreatureProperties();
		}

		public override void Update()
		{
			m_leftButtonWidget.IsEnabled = m_index > 0;
			m_rightButtonWidget.IsEnabled = m_index < m_infoList.Count - 1;

			if (m_leftButtonWidget.IsClicked || Input.Left)
			{
				m_index = Math.Max(m_index - 1, 0);
				UpdateCreatureProperties();
			}
			if (m_rightButtonWidget.IsClicked || Input.Right)
			{
				m_index = Math.Min(m_index + 1, m_infoList.Count - 1);
				UpdateCreatureProperties();
			}
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
				ScreensManager.GoBack(Array.Empty<object>());

			// Efecto arcoíris
			s_hue += 0.005f;
			if (s_hue >= 1f) s_hue -= 1f;
			Vector3 hsv = new Vector3(s_hue * 360f, 1f, 1f);
			Vector3 rgb = Color.HsvToRgb(hsv);
			Color rainbow = new Color(rgb);

			if (m_rainbowBar != null)
			{
				m_rainbowBar.CenterColor = rainbow;
				m_rainbowBar.BevelColor = rainbow;
			}
			if (m_buttonRect != null)
			{
				m_buttonRect.CenterColor = rainbow;
				m_buttonRect.BevelColor = rainbow;
			}
			if (m_arrowImage != null)
			{
				m_arrowImage.FillColor = rainbow;
			}
		}

		private void UpdateCreatureProperties()
		{
			BestiaryCreatureInfo info = m_infoList[m_index];

			m_modelWidget.AutoRotationVector = new Vector3(0f, 1f, 0f);
			BestiaryScreen.SetupBestiaryModelWidget(info, m_modelWidget, new Vector3(-1f, 0f, -1f), true, true);

			m_nameWidget.Text = info.DisplayName;
			m_descriptionWidget.Text = info.Description;

			// Título (clave "1")
			LabelWidget topBarLabel = Children.Find<LabelWidget>("TopBar.Label", true);
			if (topBarLabel != null)
			{
				topBarLabel.Text = LanguageControl.Get("BestiaryInfectedDescriptionScreen", 1);
			}

			// Botín (clave "2") - AHORA CON EL LABEL ENCONTRADO POR NOMBRE
			if (m_dropsLabel != null)
			{
				m_dropsLabel.Text = LanguageControl.Get("BestiaryInfectedDescriptionScreen", 2);
			}

			// --- Columna 1 (etiquetas) ---
			m_propertyNames1Widget.Text =
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "resilience") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "attack") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "herding") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "mount") + ":";

			// --- Columna 1 (valores) ---
			string attackStr = info.AttackPower > 0f ? info.AttackPower.ToString("0.0") : LanguageControl.None;
			string herdingStr = info.IsHerding ? LanguageControl.Yes : LanguageControl.No;
			string mountStr = info.CanBeRidden ? LanguageControl.Yes : LanguageControl.No;

			m_propertyValues1Widget.Text =
				$"{info.AttackResilience:F1}\n" +
				$"{attackStr}\n" +
				$"{herdingStr}\n" +
				$"{mountStr}";

			// --- Columna 2 (etiquetas) ---
			m_propertyNames2Widget.Text =
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "speed") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "jump") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "weight") + ":\n" +
				LanguageControl.Get("BestiaryInfectedDescriptionScreen", "egg") + ":";

			// --- Columna 2 (valores) ---
			string speedUnit = LanguageControl.Get("BestiaryInfectedDescriptionScreen", "speed_unit");
			string jumpUnit = LanguageControl.Get("BestiaryInfectedDescriptionScreen", "length_unit");
			string weightUnit = LanguageControl.Get("BestiaryInfectedDescriptionScreen", "weight_unit");
			string eggStr = info.HasSpawnerEgg ? LanguageControl.Exists : LanguageControl.None;

			m_propertyValues2Widget.Text =
				$"{(info.MovementSpeed * 3.6):F0} {speedUnit}\n" +
				$"{info.JumpHeight:F1} {jumpUnit}\n" +
				$"{info.Mass:F1} {weightUnit}\n" +
				$"{eggStr}";

			// --- Botín (valores) ---
			m_dropsPanel.Children.Clear();
			if (info.Loot != null && info.Loot.Count > 0)
			{
				foreach (var loot in info.Loot)
				{
					if (loot.MaxCount == 0 || loot.Probability == 0f) continue;
					string countText;
					if (loot.MinCount == loot.MaxCount)
					{
						countText = $"{loot.MinCount}";
					}
					else
					{
						countText = string.Format(LanguageControl.Get("BestiaryInfectedDescriptionScreen", "range"), loot.MinCount, loot.MaxCount);
					}
					if (loot.Probability < 1f)
					{
						string probFormat = LanguageControl.Get("BestiaryInfectedDescriptionScreen", "probability");
						countText += string.Format(probFormat, (loot.Probability * 100f).ToString("0"));
					}

					m_dropsPanel.Children.Add(new StackPanelWidget
					{
						Margin = new Vector2(20f, 0f),
						Children =
						{
							new BlockIconWidget
							{
								Size = new Vector2(32f),
								Scale = 1.2f,
								VerticalAlignment = WidgetAlignment.Center,
								Value = loot.Value
							},
							new CanvasWidget { Size = new Vector2(10f, 0f) },
							new LabelWidget
							{
								VerticalAlignment = WidgetAlignment.Center,
								Text = countText
							}
						}
					});
				}
			}
			else
			{
				m_dropsPanel.Children.Add(new LabelWidget
				{
					Margin = new Vector2(20f, 0f),
					Text = LanguageControl.Nothing
				});
			}
		}
	}
}
