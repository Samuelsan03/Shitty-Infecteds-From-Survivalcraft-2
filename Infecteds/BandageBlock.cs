using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class BandageBlock : Block
	{
		private Texture2D[] m_textures;

		public BandageBlock()
		{
		}

		public override void Initialize()
		{
			base.Initialize();
			// Cargamos ambas texturas. El orden debe coincidir con el Enum (0 = Large, 1 = Small)
			m_textures = new Texture2D[2];
			m_textures[0] = ContentManager.Get<Texture2D>("Textures/bendaje");          // Large
			m_textures[1] = ContentManager.Get<Texture2D>("Textures/bendaje small");   // Small
		}

		// --- Lógica de Texturas Personalizadas (Estilo ShittyInfectedsFlatBlock) ---

		public override int GetTextureSlotCount(int value)
		{
			return 1;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 0;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			BandageType type = GetBandageType(Terrain.ExtractData(value));
			TerrainGeometry customGeometry = geometry.GetGeometry(m_textures[(int)type]);

			int data = Terrain.ExtractData(value);
			int rotation = data & 3;

			generator.GenerateFlatVertices(this, value, x, y, z, rotation, Color.White, customGeometry.OpaqueSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BandageType type = GetBandageType(Terrain.ExtractData(value));
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, m_textures[(int)type], color, false, environmentData);
		}

		// --- Datos y Enum ---

		public static BandageType GetBandageType(int data)
		{
			return (BandageType)(data & 15);
		}

		public static int SetBandageType(int data, BandageType type)
		{
			return (data & -16) | (int)type;
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int type = (int)GetBandageType(Terrain.ExtractData(value));
			// Fallback al índice si sale de rango, aunque el enum lo previene
			if (type < 0 || type >= 2) return string.Empty;
			return LanguageControl.Get("BandageBlock", type);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(BlockIndex, 0, SetBandageType(0, BandageType.Large));
			yield return Terrain.MakeBlockValue(BlockIndex, 0, SetBandageType(0, BandageType.Small));
		}

		public enum BandageType
		{
			Large,
			Small
		}

		public const int Index = 520; // Usamos el índice más bajo de los dos originales
	}
}
