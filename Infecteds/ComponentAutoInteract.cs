using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAutoInteract : Component, IUpdateable
	{
		private ComponentCreature m_componentCreature;
		private ComponentMiner m_componentMiner;
		private ComponentHealth m_componentHealth;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemTime m_subsystemTime;
		private Random m_random = new Random();

		public float AutoInteractRate { get; set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public void Update(float dt)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				return;
			}
			if (AutoInteractRate <= 0f || !m_random.Bool(AutoInteractRate) || !m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)((float)(GetHashCode() % 100) / 100f)))
			{
				return;
			}
			ComponentCreatureModel componentCreatureModel = m_componentCreature.ComponentCreatureModel;
			Vector3 eyePosition = componentCreatureModel.EyePosition;
			Vector3 forwardVector = componentCreatureModel.EyeRotation.GetForwardVector();
			for (int i = 0; i < 10; i++)
			{
				TerrainRaycastResult? terrainRaycastResult = m_subsystemTerrain.Raycast(eyePosition, eyePosition + (forwardVector + m_random.Vector3(0.75f)) * 2.0f, true, true, delegate (int value, float distance)
				{
					if (distance > 1.5f)
					{
						return false;
					}
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
					return !block.IsPlacementTransparent_(value) || block.IsInteractive(m_subsystemTerrain, value);
				});
				if (terrainRaycastResult != null && terrainRaycastResult.Value.Distance < 1.5f && Terrain.ExtractContents(terrainRaycastResult.Value.Value) != 57)
				{
					SubsystemBlockBehavior[] blockBehaviors = m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(terrainRaycastResult.Value.Value));
					for (int j = 0; j < blockBehaviors.Length; j++)
					{
						if (blockBehaviors[j].OnInteract(terrainRaycastResult.Value, m_componentMiner))
						{
							if (m_componentCreature.PlayerStats != null)
							{
								m_componentCreature.PlayerStats.BlocksInteracted += 1L;
							}
							m_componentMiner.Poke(false);
							return;
						}
					}
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>();
			AutoInteractRate = valuesDictionary.GetValue<float>("AutoInteractRate");
		}
	}
}
