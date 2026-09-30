using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class MinigunBlock : Block
	{
		public const int Index = 1048;

		private BlockMesh m_standaloneBlockMeshUnloaded;
		private BlockMesh m_standaloneBlockMeshLoaded;
		private Texture2D m_texture;

		public override void Initialize()
		{
			m_texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/Ametralladora");

			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/Ametralladora");

			ModelMesh mainMesh = model.FindMesh("Gun", true);
			Matrix mainTransform = BlockMesh.GetBoneAbsoluteTransform(mainMesh.ParentBone);

			m_standaloneBlockMeshUnloaded = new BlockMesh();
			m_standaloneBlockMeshUnloaded.AppendModelMeshPart(mainMesh.MeshParts[0], mainTransform, false, false, false, false, Color.White);

			m_standaloneBlockMeshLoaded = new BlockMesh();
			m_standaloneBlockMeshLoaded.AppendModelMeshPart(mainMesh.MeshParts[0], mainTransform, false, false, false, false, Color.White);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			bool loaded = GetLoadState(Terrain.ExtractData(value)) == LoadState.Loaded;

			BlockMesh mesh = loaded ? m_standaloneBlockMeshLoaded : m_standaloneBlockMeshUnloaded;

			BlocksManager.DrawMeshBlock(primitivesRenderer, mesh, m_texture, color, 2f * size, ref matrix, environmentData);
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != BlockIndex)
			{
				return true;
			}
			int data = Terrain.ExtractData(oldValue);
			return SetLoadState(Terrain.ExtractData(newValue), LoadState.Loaded) != SetLoadState(data, LoadState.Loaded);
		}

		public enum LoadState
		{
			Empty,
			Loaded
		}

		// Bit 0: Estado (Empty=0, Loaded=1)
		public static LoadState GetLoadState(int data)
		{
			return (LoadState)(data & 1);
		}

		public static int SetLoadState(int data, LoadState loadState)
		{
			return (data & ~1) | (int)loadState;
		}

		// Bits 1-8: Cantidad de balas (0-200) - Ampliado para soportar más munición
		public static int GetAmmoCount(int data)
		{
			return (data >> 1) & 0xFF;
		}

		public static int SetAmmoCount(int data, int count)
		{
			return (data & ~0x1FE) | ((Math.Clamp(count, 0, 200) & 0xFF) << 1);
		}

		// Bits 9-16: Daño
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 9 & 255;
		}

		public override int SetDamage(int value, int damage)
		{
			int data = Terrain.ExtractData(value);
			data = (data & ~0xFE00) | (Math.Clamp(damage, 0, 255) << 9);
			return Terrain.ReplaceData(value, data);
		}
	}
}