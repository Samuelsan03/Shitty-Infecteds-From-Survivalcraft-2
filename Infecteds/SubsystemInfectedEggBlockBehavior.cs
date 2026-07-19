using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemInfectedEggBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => Array.Empty<int>();

		/// <summary>
		/// Diccionario que vincula cada tipo de huevo con sus criaturas posibles.
		/// </summary>
		public static readonly Dictionary<InfectedEggBlock.InfectedType, string[]> CreatureTemplatesByType = new Dictionary<InfectedEggBlock.InfectedType, string[]>
		{
			{
				InfectedEggBlock.InfectedType.Common, new string[] { "InfectedNormal1", "InfectedNormal2", "InfectedFast1", "InfectedFast2" }
			},
			{
				InfectedEggBlock.InfectedType.Ghost, new string[] { "GhostNormal" }
			}
		};

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

			string[] creatures = GetCreaturesForType(type);

			if (creatures.Length > 0)
			{
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

			return true;
		}

		public static string[] GetCreaturesForType(InfectedEggBlock.InfectedType type)
		{
			if (CreatureTemplatesByType.TryGetValue(type, out string[] data))
			{
				return data;
			}
			return Array.Empty<string>();
		}

		public static IEnumerable<string> GetAllCreatureTemplates()
		{
			HashSet<string> templates = new HashSet<string>();
			foreach (string[] list in CreatureTemplatesByType.Values)
			{
				foreach (string t in list)
				{
					templates.Add(t);
				}
			}
			return templates;
		}
	}
}
