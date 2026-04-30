using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RepeatingCrossbowBlock : Block
	{
		public static int Index = 300;

		private BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];
		private Block m_boltBlock;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/repeat crossbow");

			Matrix body = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix bowRelaxed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowRelaxed", true).ParentBone);
			Matrix stringRelaxed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringRelaxed", true).ParentBone);
			Matrix bowTensed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowTensed", true).ParentBone);
			Matrix stringTensed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringTensed", true).ParentBone);

			BlockMesh relaxedMesh = new BlockMesh();
			relaxedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], body * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);
			relaxedMesh.AppendModelMeshPart(model.FindMesh("BowRelaxed", true).MeshParts[0], bowRelaxed * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);
			relaxedMesh.AppendModelMeshPart(model.FindMesh("StringRelaxed", true).MeshParts[0], stringRelaxed * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);

			BlockMesh tensedMesh = new BlockMesh();
			tensedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], body * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);
			tensedMesh.AppendModelMeshPart(model.FindMesh("BowTensed", true).MeshParts[0], bowTensed * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);
			tensedMesh.AppendModelMeshPart(model.FindMesh("StringTensed", true).MeshParts[0], stringTensed * Matrix.CreateTranslation(0, 0, 0), false, false, false, false, Color.White);

			for (int i = 0; i < 16; i++)
			{
				float factor = (float)i / 15f;
				m_standaloneBlockMeshes[i] = new BlockMesh();
				m_standaloneBlockMeshes[i].AppendBlockMesh(relaxedMesh);
				m_standaloneBlockMeshes[i].BlendBlockMesh(tensedMesh, factor);
			}

			m_boltBlock = BlocksManager.GetBlock<RepeatingBoltBlock>(false);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			int draw = GetDraw(data);
			int boltCount = GetBoltCount(data);
			RepeatingBoltBlock.RepeatingBoltType boltType = GetBoltType(data);

			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[draw], color, 2f * size, ref matrix, environmentData);

			if (boltCount > 0)
			{
				Matrix boltMatrix = Matrix.CreateRotationX(-MathF.PI / 2f) * Matrix.CreateTranslation(0f, 0.2f * size, -0.09f * size) * matrix;
				int boltValue = Terrain.MakeBlockValue(m_boltBlock.BlockIndex, 0, RepeatingBoltBlock.SetBoltType(0, boltType));
				m_boltBlock.DrawBlock(primitivesRenderer, boltValue, color, size, ref boltMatrix, environmentData);
			}
		}

		public override int GetDamage(int value)
		{
			return 0;
		}

		public override int SetDamage(int value, int damage)
		{
			return value;
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			int oldContents = Terrain.ExtractContents(oldValue);
			int newContents = Terrain.ExtractContents(newValue);

			if (oldContents != newContents)
				return true;

			int oldData = Terrain.ExtractData(oldValue);
			int newData = Terrain.ExtractData(newValue);
			int oldCount = GetBoltCount(oldData);
			int newCount = GetBoltCount(newData);

			if ((oldCount == 0) != (newCount == 0))
				return true;

			if (oldCount > 0 && newCount > 0 && GetBoltType(oldData) != GetBoltType(newData))
				return true;

			return false;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, 0);
		}

		public static int GetDraw(int data) => data & 0xF;
		public static int SetDraw(int data, int draw) => (data & ~0xF) | (draw & 0xF);

		public static int GetBoltCount(int data) => (data >> 4) & 0xF;
		public static int SetBoltCount(int data, int count) => (data & ~0xF0) | ((count & 0xF) << 4);

		public static RepeatingBoltBlock.RepeatingBoltType GetBoltType(int data) => (RepeatingBoltBlock.RepeatingBoltType)((data >> 8) & 0xF);
		public static int SetBoltType(int data, RepeatingBoltBlock.RepeatingBoltType type) => (data & ~0xF00) | (((int)type & 0xF) << 8);
	}
}