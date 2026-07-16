using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemInfectedEggBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => Array.Empty<int>();

		private Random m_random = new Random();
		private InfectedEggBlock m_block;
		private SubsystemGameInfo m_subsystemGameInfo;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_block = (InfectedEggBlock)BlocksManager.Blocks[InfectedEggBlock.Index];
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			int data = Terrain.ExtractData(worldItem.Value);
			InfectedEggBlock.InfectedType type = InfectedEggBlock.GetInfectedType(data);

			// Obtener las criaturas posibles para este tipo
			string[] creatures = InfectedEggBlock.GetCreaturesForType(type);

			if (creatures.Length > 0)
			{
				// Elegir una criatura aleatoria de la categoría
				string creatureTemplate = creatures[m_random.Int(0, creatures.Length - 1)];

				try
				{
					Entity entity = DatabaseManager.CreateEntity(Project, creatureTemplate, true);
					entity.FindComponent<ComponentBody>(true).Position = worldItem.Position;
					entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
					entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0.25f;
					Project.AddEntity(entity);
				}
				catch (Exception ex)
				{
					Log.Error($"Error spawning infected from egg (type: {type}): {ex.Message}");
				}
			}

			return true; // El proyectil se consume al impactar
		}
	}
}
