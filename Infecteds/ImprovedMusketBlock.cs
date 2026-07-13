using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ImprovedMusketBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/ShotGun2");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Hammer", true).ParentBone);

			this.m_standaloneBlockMeshUnloaded = new BlockMesh();
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], boneAbsoluteTransform2, false, false, false, false, Color.White);

			this.m_standaloneBlockMeshLoaded = new BlockMesh();
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], Matrix.CreateRotationX(0.7f) * boneAbsoluteTransform2, false, false, false, false, Color.White);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (ImprovedMusketBlock.GetHammerState(Terrain.ExtractData(value)))
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshLoaded, color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshUnloaded, color, 2f * size, ref matrix, environmentData);
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}
			int data = Terrain.ExtractData(oldValue);
			return ImprovedMusketBlock.SetHammerState(Terrain.ExtractData(newValue), true) != ImprovedMusketBlock.SetHammerState(data, true);
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

		public static int GetAmmoCount(int data)
		{
			return data & 3;
		}

		public static int SetAmmoCount(int data, int count)
		{
			return (data & -4) | (Math.Clamp(count, 0, 2) & 3);
		}

		public static bool GetHammerState(int data)
		{
			return (data & 4) != 0;
		}

		public static int SetHammerState(int data, bool state)
		{
			if (state)
			{
				return data | 4;
			}
			return data & -5;
		}

		public static int Index = 513;

		public BlockMesh m_standaloneBlockMeshUnloaded;

		public BlockMesh m_standaloneBlockMeshLoaded;
	}
}