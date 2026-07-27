using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente de comportamiento de montura mejorado.
	/// Permite manejar la montura sin necesidad de que la entidad termine con "_Saddled"
	/// </summary>
	public class ComponentSteedBehaviorImproved : ComponentSteedBehavior
	{
		/// <summary>
		/// Carga los valores iniciales del componente.
		/// Habilita el comportamiento de montura independientemente del nombre de la entidad.
		/// </summary>
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Llamamos al método base para inicializar todos los componentes y el state machine
			base.Load(valuesDictionary, idToEntityMap);

			// Forzamos la habilitación del comportamiento de montura
			// Esto sobreescribe la línea original: m_isEnabled = base.Entity.ValuesDictionary.DatabaseObject.Name.EndsWith("_Saddled");
			m_isEnabled = true;
		}
	}
}
