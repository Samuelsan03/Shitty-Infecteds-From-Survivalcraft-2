using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentInfectedFatExplosion : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemExplosions m_subsystemExplosions;
		private SubsystemNoiseAttraction m_subsystemNoiseAttraction;
		private ComponentHealth m_componentHealth;
		private ComponentBody m_componentBody;
		private bool m_hasExploded;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemNoiseAttraction = Project.FindSubsystem<SubsystemNoiseAttraction>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

			m_hasExploded = false;
		}

		public void Update(float dt)
		{
			if (m_hasExploded || m_componentHealth.Health > 0f)
				return;

			m_hasExploded = true;
			Vector3 position = m_componentBody.Position;

			int x = Terrain.ToCell(position.X);
			int y = Terrain.ToCell(position.Y);
			int z = Terrain.ToCell(position.Z);

			float explosionPower = 200f;
			bool isIncendiary = false;
			bool noSound = false;

			m_subsystemExplosions.AddExplosion(x, y, z, explosionPower, isIncendiary, noSound);

			// Emitir ruido atractivo para los zombies (radio 30, volumen alto)
			m_subsystemNoiseAttraction.MakeAttractionNoise(position, 1.0f, 30f);
		}
	}
}
