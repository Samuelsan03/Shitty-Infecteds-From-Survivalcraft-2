using System;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureInventory : ComponentInventory
	{
		public ComponentBody m_componentBody;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
		}
	}
}
