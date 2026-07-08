using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGreenNightSky : Subsystem, IUpdateable
	{
		public static SubsystemGreenNightSky Instance { get; private set; }

		public bool IsGreenNightActive => m_isGreenNightActive;

		private int m_greenNightIntervalDays = 4;
		private bool m_isGreenNightActive = false;
		private double m_lastGreenNightDay = -1;
		private bool m_greenNightTriggeredThisCycle = false;

		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemTime m_subsystemTime;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool IsNightTime
		{
			get
			{
				if (m_subsystemTimeOfDay == null) return false;
				float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
				float midday = m_subsystemTimeOfDay.Midday;
				float midnight = m_subsystemTimeOfDay.Midnight;
				float halfDayDuration = 0.25f;
				float eventStart = IntervalUtils.Add(midday, halfDayDuration);
				float eventEnd = IntervalUtils.Add(midnight, halfDayDuration);
				return IntervalUtils.IsBetween(timeOfDay, eventStart, eventEnd);
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
				return MathUtils.Saturate(1f - distFromMidnight / 0.25f);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			Instance = this;

			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);

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
			SubsystemPlayers subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>();
			if (subsystemPlayers == null) return;

			foreach (ComponentPlayer player in subsystemPlayers.ComponentPlayers)
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
