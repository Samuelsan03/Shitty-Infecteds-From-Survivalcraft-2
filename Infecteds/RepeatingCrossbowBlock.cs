using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class RepeatingCrossbowBlock : Block
	{
		public static int Index = 805;
		public BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];
		private Block arrowBlock;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/repeat crossbow");

			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowRelaxed", true).ParentBone);
			Matrix boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringRelaxed", true).ParentBone);
			Matrix boneAbsoluteTransform4 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowTensed", true).ParentBone);
			Matrix boneAbsoluteTransform5 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringTensed", true).ParentBone);

			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh.AppendModelMeshPart(model.FindMesh("BowRelaxed", true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh.AppendModelMeshPart(model.FindMesh("StringRelaxed", true).MeshParts[0], boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);

			BlockMesh blockMesh2 = new BlockMesh();
			blockMesh2.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh2.AppendModelMeshPart(model.FindMesh("BowTensed", true).MeshParts[0], boneAbsoluteTransform4 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh2.AppendModelMeshPart(model.FindMesh("StringTensed", true).MeshParts[0], boneAbsoluteTransform5 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);

			for (int i = 0; i < 16; i++)
			{
				float factor = (float)i / 15f;
				m_standaloneBlockMeshes[i] = new BlockMesh();
				m_standaloneBlockMeshes[i].AppendBlockMesh(blockMesh);
				m_standaloneBlockMeshes[i].BlendBlockMesh(blockMesh2, factor);
			}

			arrowBlock = BlocksManager.GetBlock<RepeatingBoltBlock>();
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			int draw = GetDraw(data);
			int? arrowType = GetArrowType(data);

			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[draw], color, 2f * size, ref matrix, environmentData);

			if (arrowType != null)
			{
				Matrix matrix2 = Matrix.CreateRotationX(-1.5707964f) * Matrix.CreateTranslation(0f, 0.2f * size, -0.09f * size) * matrix;
				int value2 = Terrain.MakeBlockValue(arrowBlock.BlockIndex, 0, RepeatingBoltBlock.SetArrowType(0, arrowType.Value));
				arrowBlock.DrawBlock(primitivesRenderer, value2, color, size, ref matrix2, environmentData);
			}
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

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			int num = Terrain.ExtractContents(oldValue);
			int data = Terrain.ExtractData(oldValue);
			int data2 = Terrain.ExtractData(newValue);
			if (num == BlockIndex)
			{
				int? arrowType = GetArrowType(data);
				int? arrowType2 = GetArrowType(data2);
				return !(arrowType.GetValueOrDefault() == arrowType2.GetValueOrDefault() && arrowType != null == (arrowType2 != null));
			}
			return true;
		}

		public static int? GetArrowType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0) return num - 1;
			return null;
		}

		public static int SetArrowType(int data, int? arrowType)
		{
			// Si arrowType es null, guardamos 0 (sin flecha)
			// Si tiene valor, guardamos arrowType.Value + 1 (1-4)
			int num = (arrowType != null) ? (arrowType.Value + 1) : 0;
			// Limpiar bits 4-7 y establecer nuevos
			return (data & ~(15 << 4)) | ((num & 15) << 4);
		}

		public static int GetDraw(int data)
		{
			return data & 15;
		}

		public static int SetDraw(int data, int draw)
		{
			return (data & -16) | (draw & 15);
		}

		// CORREGIDOS: operan sobre el valor completo del bloque, no solo los datos
		public static int GetLoadCount(int value)
		{
			return (value >> 8) & 15;
		}

		public static int SetLoadCount(int value, int count)
		{
			return (value & ~(15 << 8)) | ((count & 15) << 8);
		}
	}
}