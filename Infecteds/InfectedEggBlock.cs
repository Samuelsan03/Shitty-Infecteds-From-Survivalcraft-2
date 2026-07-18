using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class InfectedEggBlock : Block
	{
		/// <summary>
		/// Categorías de infectados que pueden spawnear desde los huevos.
		/// </summary>
		public enum InfectedType
		{
			Common,
			Ghost,
			// Espacio para agregar más tipos en el futuro
		}

		/// <summary>
		/// Clase contenedora para los datos específicos de cada tipo de huevo.
		/// </summary>
		public class InfectedEggData
		{
			public float Scale;
			public Color EggColor;
			public string[] CreatureTemplates;
		}

		public static int Index = 515;
		public const string FName = "InfectedEggBlock";

		private Dictionary<InfectedType, BlockMesh> m_standaloneBlockMeshes;
		private Texture2D m_eggTexture;

		/// <summary>
		/// Diccionario estrictamente vinculado al ENUM. Sin números mágicos.
		/// </summary>
		public static readonly Dictionary<InfectedType, InfectedEggData> EggData = new Dictionary<InfectedType, InfectedEggData>
		{
			{
				InfectedType.Common, new InfectedEggData
				{
					Scale = 1.0f,
					EggColor = new Color(0, 255, 0),
					CreatureTemplates = new string[] { "InfectedNormal1", "InfectedNormal2", "InfectedFast1", "InfectedFast2" }
				}
			},
			{
				InfectedType.Ghost, new InfectedEggData
				{
					Scale = 0.8f,
					EggColor = new Color(180, 180, 255),
					CreatureTemplates = new string[] { "GhostNormal" }
				}
			}
		};

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			m_eggTexture = ContentManager.Get<Texture2D>("Textures/alerta");
			m_standaloneBlockMeshes = new Dictionary<InfectedType, BlockMesh>();

			// Generar una malla única por cada valor del ENUM
			foreach (var kvp in EggData)
			{
				InfectedType type = kvp.Key;
				InfectedEggData data = kvp.Value;

				BlockMesh mesh = new BlockMesh();
				mesh.AppendModelMeshPart(
					model.FindMesh("Egg", true).MeshParts[0],
					boneTransform,
					makeEmissive: false,
					flipWindingOrder: false,
					doubleSided: false,
					flipNormals: false,
					data.EggColor
				);

				// Guardar la malla usando el ENUM como llave
				m_standaloneBlockMeshes[type] = mesh;
			}

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			InfectedType type = GetInfectedType(Terrain.ExtractData(value));

			// Buscar por ENUM
			if (m_standaloneBlockMeshes.TryGetValue(type, out BlockMesh mesh) && EggData.TryGetValue(type, out InfectedEggData data))
			{
				BlocksManager.DrawMeshBlock(
					primitivesRenderer,
					mesh,
					m_eggTexture,
					color,
					data.Scale * size,
					ref matrix,
					environmentData
				);
			}
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			// Iterar directamente los valores del ENUM
			foreach (InfectedType type in Enum.GetValues(typeof(InfectedType)))
			{
				yield return Terrain.MakeBlockValue(Index, 0, SetInfectedType(0, type));
			}
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			InfectedType type = GetInfectedType(Terrain.ExtractData(value));
			return LanguageControl.Get(FName, type.ToString());
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			InfectedType type = GetInfectedType(Terrain.ExtractData(value));

			// Buscar escala por ENUM
			if (EggData.TryGetValue(type, out InfectedEggData data))
			{
				return data.Scale;
			}
			return 1f;
		}

		public static InfectedType GetInfectedType(int data)
		{
			return (InfectedType)(data & 0xF);
		}

		public static int SetInfectedType(int data, InfectedType type)
		{
			return (data & ~0xF) | ((int)type & 0xF);
		}

		public static string[] GetCreaturesForType(InfectedType type)
		{
			// Buscar criaturas por ENUM
			if (EggData.TryGetValue(type, out InfectedEggData data))
			{
				return data.CreatureTemplates;
			}
			return Array.Empty<string>();
		}

		public static IEnumerable<string> GetAllCreatureTemplates()
		{
			HashSet<string> templates = new HashSet<string>();
			foreach (var data in EggData.Values)
				foreach (string t in data.CreatureTemplates)
					templates.Add(t);
			return templates;
		}

		public static int GetBlockValue(InfectedType type)
		{
			return Terrain.MakeBlockValue(Index, 0, SetInfectedType(0, type));
		}
	}
}
