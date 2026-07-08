using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable, IDrawable
	{
		public static SubsystemGreenNightSky Instance { get; private set; }

		public bool IsGreenNightActive => m_isGreenNightActive;

		private int m_greenNightIntervalDays = 4;
		private bool m_isGreenNightActive = false;
		private double m_lastGreenNightDay = -1;
		private bool m_greenNightTriggeredThisCycle = false;

		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;

		private LabelWidget m_hudLabel;
		private StackPanelWidget m_hudContainer;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public int[] DrawOrders => new int[] { 10 };

		public int GetDaysRemaining()
		{
			if (m_subsystemTimeOfDay == null || m_lastGreenNightDay < 0)
				return m_greenNightIntervalDays;

			double currentDay = m_subsystemTimeOfDay.Day;
			double daysRemaining = m_lastGreenNightDay - currentDay;

			return Math.Max(0, (int)Math.Floor(daysRemaining));
		}

		// Inicia cuando el sol está a la mitad de su caída (Middusk) 
		// y termina cuando la luna está a la mitad de su caída (Middawn)
		public bool IsNightTime
		{
			get
			{
				if (m_subsystemTimeOfDay == null) return false;
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
				float middusk = m_subsystemTimeOfDay.Middusk;
				float middawn = m_subsystemTimeOfDay.Middawn;
				return IntervalUtils.IsBetween(timeOfDay, middusk, middawn);
			}
		}

		public float NightIntensity
		{
			get
			{
				if (m_subsystemTimeOfDay == null || !IsNightTime) return 0f;
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
				float midnight = m_subsystemTimeOfDay.Midnight;
				float distFromMidnight = Math.Abs(IntervalUtils.Distance(timeOfDay, midnight));
				float halfDuration = Math.Abs(IntervalUtils.Distance(m_subsystemTimeOfDay.Middusk, midnight));
				if (halfDuration <= 0f) return 1f;
				return MathUtils.Saturate(1f - distFromMidnight / halfDuration);
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
			m_isGreenNightActive = valuesDictionary.GetValue<bool>("IsGreenNightActive", false);
			m_greenNightTriggeredThisCycle = valuesDictionary.GetValue<bool>("GreenNightTriggeredThisCycle", false);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue<int>("GreenNightIntervalDays", m_greenNightIntervalDays);
			valuesDictionary.SetValue<double>("LastGreenNightDay", m_lastGreenNightDay);
			valuesDictionary.SetValue<bool>("IsGreenNightActive", m_isGreenNightActive);
			valuesDictionary.SetValue<bool>("GreenNightTriggeredThisCycle", m_greenNightTriggeredThisCycle);
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
			if (m_subsystemTimeOfDay == null) return;

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

				NotifyGreenNightStart();
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

				if (m_hudLabel != null)
				{
					if (m_isGreenNightActive)
					{
						m_hudLabel.Text = "Ellos vendrán...";
						float pulse = 0.5f + 0.5f * MathF.Sin((float)Time.FrameStartTime * 3f);
						int green = (int)(128 + 127 * pulse);
						int greenLight = (int)(47 + 47 * pulse);
						m_hudLabel.Color = new Color(0, green, greenLight);
					}
					else
					{
						int daysRemaining = GetDaysRemaining();

						if (daysRemaining == 0)
						{
							m_hudLabel.Text = "Ellos vendrán esta noche...";
							float pulse = 0.7f + 0.3f * MathF.Sin((float)Time.FrameStartTime * 2f);
							int green = (int)(200 + 55 * pulse);
							m_hudLabel.Color = new Color(0, green, (int)(74 * pulse));
						}
						else if (daysRemaining == 1)
						{
							m_hudLabel.Text = "Ellos vendrán en: 1 día";
							m_hudLabel.Color = new Color(0, 255, 94);
						}
						else
						{
							m_hudLabel.Text = "Ellos vendrán en: " + daysRemaining + " días";
							m_hudLabel.Color = new Color(0, 255, 94);
						}
					}
				}

				break;
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

		public void Draw(Camera camera, int drawOrder)
		{
		}

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

		private void NotifyGreenNightStart()
		{
			if (m_subsystemPlayers == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentGui != null && player.ComponentHealth?.Health > 0)
				{
					player.ComponentGui.DisplaySmallMessage(
						"¡La Noche Verde ha comenzado!",
						new Color(0, 255, 94),
						false,
						true
					);
				}
			}
		}
	}
}
