using System;
using GameEntitySystem;

namespace Game
{
	public class PoisonInjury : Injury
	{
		public PoisonInjury(float amount, ComponentCreature poisonSource, string poisonCreatureName)
			: base(amount, poisonSource, false, GetPoisonCause(poisonCreatureName))
		{
		}

		private static string GetPoisonCause(string poisonCreatureName)
		{
			if (!string.IsNullOrEmpty(poisonCreatureName))
			{
				return string.Format(LanguageControl.Get("PoisonInfection", 2), poisonCreatureName);
			}
			return LanguageControl.Get("PoisonInfection", 1);
		}
	}
}
