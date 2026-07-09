using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable, IDrawable
	{
		public static SubsystemGreenNightSky Instance { get; private set; }

		// Solo devuelve true si el sistema está ENCENDIDO y es de noche
		public bool IsGreenNightActive => m_isGreenNightEnabled && m_isGreenNightActive;

		// Expone el estado del interruptor maestro para el Dialog
		public bool IsGreenNightEnabled => m_isGreenNightEnabled;

		public DifficultyModes CurrentDifficulty => (DifficultyModes)m_difficultyModeValue;

		private bool m_isGreenNightEnabled = false; // Interruptor maestro (Guardado)
		private int m_greenNightIntervalDays = 4;
		private bool m_isGreenNightActive = false;  // Estado real de la noche actual
		private double m_lastGreenNightDay = -1;
		private bool m_greenNightTriggeredThisCycle = false;
		private int m_difficultyModeValue = 2;

		private string[] m_difficultyNames = new string[]
		{
			"Sobrevivirás... Quizás",
			"La Muerte te Espera",
			"Tu Tumba ya Está Lista",
			"No Hay Esperanza",
			"Suicidio Asegurado",
			"Ni Dios Te Salva"
		};

		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;

		private LabelWidget m_hudLabel;
		private StackPanelWidget m_hudContainer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public int[] DrawOrders => new int[] { 10 };

		private bool IsGreenNightDay
		{
			get
			{
				if (m_subsystemTimeOfDay == null || m_lastGreenNightDay < 0)
					return false;

				double currentDay = m_subsystemTimeOfDay.Day;
				return currentDay >= m_lastGreenNightDay && currentDay < m_lastGreenNightDay + 1.0;
			}
		}

		public int GetDaysRemaining()
		{
			if (m_subsystemTimeOfDay == null || m_lastGreenNightDay < 0)
				return m_greenNightIntervalDays;

			double currentDay = m_subsystemTimeOfDay.Day;
			double daysRemaining = m_lastGreenNightDay - currentDay;

			return Math.Max(1, (int)Math.Floor(daysRemaining));
		}

		public bool IsNightTime
		{
			get
			{
				if (m_subsystemTimeOfDay == null) return false;
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;

				float eventStart = m_subsystemTimeOfDay.DuskStart;
				float eventEnd = m_subsystemTimeOfDay.DawnStart;

				return IntervalUtils.IsBetween(timeOfDay, eventStart, eventEnd);
			}
		}

		public float NightIntensity
		{
			get
			{
				if (m_subsystemTimeOfDay == null || !IsNightTime) return 0f;
				return 1f;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			Instance = this;

			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			m_greenNightIntervalDays = valuesDictionary.GetValue<int>("GreenNightIntervalDays", 4);
			m_lastGreenNightDay = valuesDictionary.GetValue<double>("LastGreenNightDay", -1);
			m_isGreenNightEnabled = valuesDictionary.GetValue<bool>("IsGreenNightEnabled", false);
			m_isGreenNightActive = valuesDictionary.GetValue<bool>("IsGreenNightActive", false);
			m_greenNightTriggeredThisCycle = valuesDictionary.GetValue<bool>("GreenNightTriggeredThisCycle", false);
			m_difficultyModeValue = valuesDictionary.GetValue<int>("DifficultyMode", 2);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue<int>("GreenNightIntervalDays", m_greenNightIntervalDays);
			valuesDictionary.SetValue<double>("LastGreenNightDay", m_lastGreenNightDay);
			valuesDictionary.SetValue<bool>("IsGreenNightEnabled", m_isGreenNightEnabled);
			valuesDictionary.SetValue<bool>("IsGreenNightActive", m_isGreenNightActive);
			valuesDictionary.SetValue<bool>("GreenNightTriggeredThisCycle", m_greenNightTriggeredThisCycle);
			valuesDictionary.SetValue<int>("DifficultyMode", m_difficultyModeValue);
		}

		public override void Dispose()
		{
			if (m_hudContainer != null && m_hudContainer.ParentWidget != null)
			{
				m_hudContainer.ParentWidget.Children.Remove(m_hudContainer);
			}

			if (Instance == this)
				Instance = null;
			base.Dispose();
		}

		public void Update(float dt)
		{
			// Si el sistema está desactivado, ocultar el HUD y detener toda la lógica
			if (!m_isGreenNightEnabled || m_subsystemTimeOfDay == null)
			{
				HideHudLabel();
				return;
			}

			double currentDay = m_subsystemTimeOfDay.Day;

			if (m_lastGreenNightDay < 0)
			{
				m_lastGreenNightDay = Math.Floor(currentDay) + m_greenNightIntervalDays;
				m_greenNightTriggeredThisCycle = false;
			}

			bool shouldActivate = currentDay >= m_lastGreenNightDay && IsNightTime;

			if (shouldActivate && !m_greenNightTriggeredThisCycle)
			{
				m_greenNightTriggeredThisCycle = true;
				m_isGreenNightActive = true;
			}

			if (m_isGreenNightActive && !IsNightTime)
			{
				m_isGreenNightActive = false;
				m_lastGreenNightDay = Math.Floor(currentDay) + m_greenNightIntervalDays;
				m_greenNightTriggeredThisCycle = false;
			}

			UpdateHudLabel();
		}

		private void UpdateHudLabel()
		{
			if (m_subsystemPlayers == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player == null || player.GuiWidget == null) continue;

				if (m_hudContainer == null || m_hudContainer.ParentWidget != player.GuiWidget)
				{
					CreateHudLabel(player);
				}

				if (m_hudLabel != null && m_hudContainer != null)
				{
					m_hudContainer.IsVisible = true; // Aseguramos que se muestre

					int diffIndex = Math.Min(m_difficultyModeValue, m_difficultyNames.Length - 1);
					string suffix = "\nNivel de sufrimiento: \n" + m_difficultyNames[diffIndex];

					if (IsGreenNightDay)
					{
						if (m_isGreenNightActive)
						{
							m_hudLabel.Text = "Ellos vendrán…" + suffix;
							float pulse = 0.5f + 0.5f * MathF.Sin((float)Time.FrameStartTime * 3f);
							int green = (int)(128 + 127 * pulse);
							int greenLight = (int)(47 + 47 * pulse);
							m_hudLabel.Color = new Color(0, green, greenLight);
						}
						else
						{
							m_hudLabel.Text = "Ellos vendrán…" + suffix;
							m_hudLabel.Color = new Color(0, 255, 94);
						}
					}
					else
					{
						int daysRemaining = GetDaysRemaining();
						if (daysRemaining == 1)
						{
							m_hudLabel.Text = "Ellos vendrán en: 1 día" + suffix;
						}
						else
						{
							m_hudLabel.Text = "Ellos vendrán en: " + daysRemaining + " días" + suffix;
						}
						m_hudLabel.Color = new Color(0, 255, 94);
					}
				}

				break;
			}
		}

		private void HideHudLabel()
		{
			// Simplemente oculta el contenedor en lugar de destruirlo
			if (m_hudContainer != null)
			{
				m_hudContainer.IsVisible = false;
			}
		}

		private void CreateHudLabel(ComponentPlayer player)
		{
			m_hudContainer = new StackPanelWidget
			{
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				Name = "GreenNightHudContainer"
			};

			m_hudLabel = new LabelWidget
			{
				Text = "",
				Color = new Color(0, 255, 94),
				FontScale = 0.8f,
				DropShadow = true,
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Center,
				MarginRight = 15f
			};

			m_hudContainer.Children.Add(m_hudLabel);
			player.GuiWidget.Children.Add(m_hudContainer);
		}

		public void Draw(Camera camera, int drawOrder) { }

		public void SetGreenNightInterval(int days)
		{
			m_greenNightIntervalDays = Math.Max(1, days);
			if (m_subsystemTimeOfDay != null)
			{
				double currentDay = m_subsystemTimeOfDay.Day;
				m_lastGreenNightDay = Math.Floor(currentDay) + m_greenNightIntervalDays;
			}
			else
			{
				m_lastGreenNightDay = -1;
			}
			m_isGreenNightActive = false;
			m_greenNightTriggeredThisCycle = false;
		}

		public void SetDifficultyMode(DifficultyModes mode)
		{
			m_difficultyModeValue = (int)mode;
		}

		private void NotifyGreenNightStart()
		{
			if (m_subsystemPlayers == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentGui != null && player.ComponentHealth?.Health > 0)
				{
					player.ComponentGui.DisplaySmallMessage(
						"Noche Verde activada.",
						new Color(0, 255, 94),
						false,
						true
					);
				}
			}
		}

		private void NotifyGreenNightEnd()
		{
			if (m_subsystemPlayers == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentGui != null && player.ComponentHealth?.Health > 0)
				{
					player.ComponentGui.DisplaySmallMessage(
						"Noche Verde desactivada.",
						new Color(180, 255, 180),
						false,
						true
					);
				}
			}
		}

		public void SetGreenNightActive(bool isEnabled)
		{
			m_isGreenNightEnabled = isEnabled;

			if (isEnabled)
			{
				// Al activar, inicializamos el contador si no existe
				if (m_subsystemTimeOfDay != null && m_lastGreenNightDay < 0)
				{
					m_lastGreenNightDay = Math.Floor(m_subsystemTimeOfDay.Day) + m_greenNightIntervalDays;
				}
				NotifyGreenNightStart();
			}
			else
			{
				// Al desactivar, limpiamos estados y ocultamos el HUD
				m_isGreenNightActive = false;
				m_greenNightTriggeredThisCycle = false;

				if (m_subsystemTimeOfDay != null)
				{
					m_lastGreenNightDay = Math.Floor(m_subsystemTimeOfDay.Day) + m_greenNightIntervalDays;
				}
				else
				{
					m_lastGreenNightDay = -1;
				}

				HideHudLabel();
				NotifyGreenNightEnd();
			}

			// Forzar guardado inmediato al cambiar el estado
			Project.Save();
		}
	}
}
