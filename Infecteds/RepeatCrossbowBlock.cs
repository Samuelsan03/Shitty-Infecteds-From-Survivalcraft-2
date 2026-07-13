using System;
using Engine;
using Engine.Graphics;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class RepeatCrossbowBlock : Block
	{
		public static int Index = 512;

		public BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];
		private Block m_repeatBoltBlock;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/RepeatCrossbow");

			Matrix bodyTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix bowRelaxedTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowRelaxed", true).ParentBone);
			Matrix stringRelaxedTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringRelaxed", true).ParentBone);
			Matrix bowTensedTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowTensed", true).ParentBone);
			Matrix stringTensedTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringTensed", true).ParentBone);

			BlockMesh relaxedMesh = new BlockMesh();
			relaxedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], bodyTransform, false, false, false, false, Color.White);
			relaxedMesh.AppendModelMeshPart(model.FindMesh("BowRelaxed", true).MeshParts[0], bowRelaxedTransform, false, false, false, false, Color.White);
			relaxedMesh.AppendModelMeshPart(model.FindMesh("StringRelaxed", true).MeshParts[0], stringRelaxedTransform, false, false, false, false, Color.White);

			BlockMesh tensedMesh = new BlockMesh();
			tensedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], bodyTransform, false, false, false, false, Color.White);
			tensedMesh.AppendModelMeshPart(model.FindMesh("BowTensed", true).MeshParts[0], bowTensedTransform, false, false, false, false, Color.White);
			tensedMesh.AppendModelMeshPart(model.FindMesh("StringTensed", true).MeshParts[0], stringTensedTransform, false, false, false, false, Color.White);

			for (int i = 0; i < 16; i++)
			{
				float factor = i / 15f;
				m_standaloneBlockMeshes[i] = new BlockMesh();
				m_standaloneBlockMeshes[i].AppendBlockMesh(relaxedMesh);
				m_standaloneBlockMeshes[i].BlendBlockMesh(tensedMesh, factor);
			}

			m_repeatBoltBlock = BlocksManager.GetBlockGeneral<RepeatBoltBlock>(false);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			int draw = GetDraw(data);
			RepeatBoltType? boltType = GetRepeatBoltType(data);
			int count = GetCount(data);

			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[draw], color, 2f * size, ref matrix, environmentData);

			if (boltType != null && count > 0)
			{
				Matrix boltMatrix = Matrix.CreateRotationX(-1.5707964f) * Matrix.CreateTranslation(0f, 0.2f * size, -0.09f * size) * matrix;
				int boltValue = Terrain.MakeBlockValue(m_repeatBoltBlock.BlockIndex, 0, RepeatBoltBlock.SetRepeatBoltType(0, boltType.Value));
				m_repeatBoltBlock.DrawBlock(primitivesRenderer, boltValue, color, size, ref boltMatrix, environmentData);
			}
		}

		public override int GetDamage(int value) => 0; // Sin daño
		public override int SetDamage(int value, int damage) => value;

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			int oldContents = Terrain.ExtractContents(oldValue);
			int oldData = Terrain.ExtractData(oldValue);
			int newData = Terrain.ExtractData(newValue);

			if (oldContents == Index)
			{
				RepeatBoltType? oldBolt = GetRepeatBoltType(oldData);
				RepeatBoltType? newBolt = GetRepeatBoltType(newData);
				if (oldBolt.GetValueOrDefault() == newBolt.GetValueOrDefault() && oldBolt != null == (newBolt != null))
					return false;
			}
			return true;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, 0);
		}

		public static RepeatBoltType? GetRepeatBoltType(int data)
		{
			int type = data >> 4 & 15;
			if (type != 0)
				return (RepeatBoltType)(type - 1);
			return null;
		}

		public static int SetRepeatBoltType(int data, RepeatBoltType? boltType)
		{
			int type = boltType.HasValue ? (int)boltType.Value + 1 : 0;
			return (data & -241) | (type & 15) << 4;
		}

		public static int GetDraw(int data) => data & 15;
		public static int SetDraw(int data, int draw) => (data & -16) | (draw & 15);

		// Contador de munición (0..8) almacenado en los bits 8..15
		public static int GetCount(int data) => (data >> 8) & 0xFF;
		public static int SetCount(int data, int count) => (data & ~0xFF00) | ((Math.Clamp(count, 0, 8) & 0xFF) << 8);
	}
}
