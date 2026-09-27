using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CannonBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/ItemsLauncher");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Cylinder", true).ParentBone);
			this.m_standaloneBlockMesh = new BlockMesh();
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Cylinder", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 1.5f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, defaultTexture, color, 1.5f * size, ref matrix, environmentData);
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}
			return CannonBlock.GetLoadState(Terrain.ExtractData(newValue)) != CannonBlock.GetLoadState(Terrain.ExtractData(oldValue));
		}

		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		public static CannonBlock.LoadState GetLoadState(int data)
		{
			return (CannonBlock.LoadState)(data & 1);
		}

		public static int SetLoadState(int data, CannonBlock.LoadState loadState)
		{
			return (data & -2) | (int)loadState;
		}

		public BlockMesh m_standaloneBlockMesh;

		public static int Index = 530;

		public enum LoadState
		{
			Empty,
			Loaded
		}
	}
}
