using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentInfectedFatExplosion : Component, IUpdateable
	{
		// Enum definitivo para los tipos de explosión
		public enum FatExplosionType
		{
			Normal,
			Incendiary,
			Poisonous,
			Frozen
		}
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemExplosions m_subsystemExplosions;
		private SubsystemPoisonExplosions m_subsystemPoisonExplosions;
		private SubsystemFrozenExplosions m_subsystemFrozenExplosions;
		private SubsystemNoiseAttraction m_subsystemNoiseAttraction;
		private SubsystemTime m_subsystemTime; // Para el retraso al desaparecer

		private ComponentHealth m_componentHealth;
		private ComponentBody m_componentBody;
		private ComponentCreature m_componentCreature; // Para acceder al Despawn

		private bool m_hasExploded;
		private FatExplosionType m_explosionType;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemPoisonExplosions = Project.FindSubsystem<SubsystemPoisonExplosions>(true);
			m_subsystemFrozenExplosions = Project.FindSubsystem<SubsystemFrozenExplosions>(true);
			m_subsystemNoiseAttraction = Project.FindSubsystem<SubsystemNoiseAttraction>(true);

			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			m_hasExploded = false;

			// Diferenciamos la explosión automáticamente según el nombre exacto de la criatura en la base de datos.
			string entityName = Entity.ValuesDictionary.DatabaseObject.Name;

			if (entityName == "FatInfectedArsonist" || entityName == "FatInfectedArsonistTamed")
			{
				m_explosionType = FatExplosionType.Incendiary;
			}
			else if (entityName == "FatInfectedPoisonous" || entityName == "FatInfectedPoisonousTamed")
			{
				m_explosionType = FatExplosionType.Poisonous;
			}
			else if (entityName == "FatInfectedFrozen" || entityName == "FatInfectedFrozenTamed")
			{
				m_explosionType = FatExplosionType.Frozen;
			}
			else
			{
				// Cualquier otra variante (como "FatInfected") usará la normal
				m_explosionType = FatExplosionType.Normal;
			}
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

			// Derivación de la explosión según el enum
			if (m_explosionType == FatExplosionType.Poisonous)
			{
				m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, explosionPower);
			}
			else if (m_explosionType == FatExplosionType.Frozen)
			{
				// Tu SubsystemFrozenExplosions usa la firma estándar de AddExplosion
				m_subsystemFrozenExplosions.AddExplosion(x, y, z, explosionPower, false, false);
			}
			else
			{
				bool isIncendiary = (m_explosionType == FatExplosionType.Incendiary);
				bool noSound = false;
				m_subsystemExplosions.AddExplosion(x, y, z, explosionPower, isIncendiary, noSound);
			}

			// Emitir ruido atractivo para los zombies en todos los casos
			m_subsystemNoiseAttraction.MakeAttractionNoise(position, 1.0f, 30f);

			// Hacer desaparecer el cadáver inmediatamente (con 0.1s de margen técnico para que 
			// ComponentHealth termine de soltar los orbes de experiencia y el loot sin errores).
			m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.1, delegate
			{
				if (m_componentCreature != null && m_componentCreature.ComponentSpawn != null)
				{
					m_componentCreature.ComponentSpawn.Despawn();
				}
			});
		}
	}
}
