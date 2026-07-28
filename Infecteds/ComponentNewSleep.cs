using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewSleep : ComponentSleep
	{
		public static string fName = "ComponentNewSleep";

		private SubsystemGreenNightSky m_subsystemGreenNight;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemGreenNight = Project.FindSubsystem<SubsystemGreenNightSky>(false);
		}

		public override void Sleep(bool allowManualWakeup)
		{
			if (allowManualWakeup && m_subsystemGreenNight != null && m_subsystemGreenNight.IsGreenNightActive)
			{
				if (m_componentPlayer?.ComponentGui != null)
				{
					m_componentPlayer.ComponentGui.DisplaySmallMessage(
						LanguageControl.Get(ComponentNewSleep.fName, 1),
						new Color(0, 255, 94),
						true,
						true
					);
				}
				return;
			}

			base.Sleep(allowManualWakeup);
		}
	}
}
