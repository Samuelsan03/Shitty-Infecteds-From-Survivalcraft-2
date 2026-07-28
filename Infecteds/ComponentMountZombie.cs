using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentMountZombie : ComponentMount
	{
		public bool CanRiderBeMounted { get; set; }

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Carga el booleano del diccionario, por defecto es true
			CanRiderBeMounted = valuesDictionary.GetValue<bool>("CanRiderBeMounted", true);
		}
	}
}
