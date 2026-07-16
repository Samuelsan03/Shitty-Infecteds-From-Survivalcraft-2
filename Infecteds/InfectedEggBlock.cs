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
			/// <summary>
			/// Infectados normales/comunes
			/// </summary>
			Common,
			// Espacio para agregar más tipos en el futuro
		}

		public static int Index = 514;
		public const string FName = "InfectedEggBlock";

		private BlockMesh m_standaloneBlockMesh;
		private Texture2D m_eggTexture;
		private const float EggScale = 1.0f;

		private static readonly Color CommonEggColor = new Color(0, 255, 0);

		/// <summary>
		/// Criaturas asociadas a cada tipo de infectado.
		/// Al spawnear, se elige una aleatoriamente del tipo correspondiente.
		/// </summary>
		private static readonly string[][] TypeCreatures = new string[][]
		{
			new string[] { "InfectedNormal1", "InfectedNormal2" }, // Common
            // Agregar más tipos aquí cuando se necesiten
        };

		public override void Initialize()
		{
			// Cargar el modelo base del huevo
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			// Crear el BlockMesh aplicando el color directamente a la malla (sin alterar UVs)
			m_standaloneBlockMesh = new BlockMesh();
			m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("Egg", true).MeshParts[0],
				boneTransform,
				makeEmissive: false,
				flipWindingOrder: false,
				doubleSided: false,
				flipNormals: false,
				CommonEggColor // <--- AQUÍ SE APLICA EL COLOR
			);

			// Cargar la textura directa
			m_eggTexture = ContentManager.Get<Texture2D>("Textures/alerta");

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No genera geometría de terreno
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(
				primitivesRenderer,
				m_standaloneBlockMesh,
				m_eggTexture,
				color, // El color que pasa el motor (afecta iluminación general del mundo)
				EggScale * size,
				ref matrix,
				environmentData
			);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
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
			return EggScale;
		}

		/// <summary>
		/// Obtiene el tipo de infectado del valor de datos del bloque.
		/// Usa los primeros 4 bits (permite hasta 15 tipos).
		/// </summary>
		public static InfectedType GetInfectedType(int data)
		{
			return (InfectedType)(data & 0xF);
		}

		/// <summary>
		/// Establece el tipo de infectado en el valor de datos del bloque.
		/// </summary>
		public static int SetInfectedType(int data, InfectedType type)
		{
			return (data & ~0xF) | ((int)type & 0xF);
		}

		/// <summary>
		/// Obtiene los nombres de templates de criaturas para un tipo dado.
		/// </summary>
		public static string[] GetCreaturesForType(InfectedType type)
		{
			int index = (int)type;
			if (index >= 0 && index < TypeCreatures.Length)
			{
				return TypeCreatures[index];
			}
			return Array.Empty<string>();
		}
	}
}
