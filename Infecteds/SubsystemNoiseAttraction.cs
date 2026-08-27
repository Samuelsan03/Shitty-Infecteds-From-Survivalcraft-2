using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemNoiseAttraction : Subsystem
	{
		private SubsystemBodies m_subsystemBodies;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
		}

		/// <summary>
		/// Emite un ruido que atrae a las entidades que implementan INoiseAttraction.
		/// </summary>
		public void MakeAttractionNoise(Vector3 position, float loudness, float range)
		{
			float rangeSq = range * range;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody body = m_componentBodies.Array[i];
				foreach (INoiseAttraction attractionComponent in body.Entity.FindComponents<INoiseAttraction>())
				{
					attractionComponent.AttractNoise(null, position, loudness);
				}
			}
		}
	}
}
