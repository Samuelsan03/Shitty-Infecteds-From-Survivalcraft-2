using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class RepeatingBoltBlock : Block
	{
		public static int Index = 301;

		public List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();

		public static int[] m_order = new int[] { 0, 1, 2, 3 };

		public static string[] m_tipNames = new string[] { "ArrowTip", "ArrowTip", "ArrowTip", "ArrowTip" };
		public static int[] m_tipTextureSlots = new int[] { 79, 63, 182, 225 };

		public static string[] m_shaftNames = new string[] { "ArrowShaft", "ArrowShaft", "ArrowShaft", "ArrowShaft" };
		public static int[] m_shaftTextureSlots = new int[] { 51, 51, 51, 51 };

		public static string[] m_stabilizerNames = new string[] { "ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer" };
		public static int[] m_stabilizerTextureSlots = new int[] { 15, 15, 15, 15 };

		public static float[] m_offsets = new float[] { -0.45f, -0.45f, -0.45f, -0.45f };

		public static float[] m_weaponPowers = new float[] { 16f, 24f, 36f, 8f };
		public static float[] m_iconViewScales = new float[] { 0.8f, 0.8f, 0.8f, 0.8f };
		public static float[] m_explosionPressures = new float[] { 0f, 0f, 0f, 50f };

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/repeat bolt");

			for (int num = 0; num < 4; num++)
			{
				Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(m_shaftNames[num], true).ParentBone);
				Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(m_stabilizerNames[num], true).ParentBone);
				Matrix boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(m_tipNames[num], true).ParentBone);

				BlockMesh blockMesh = new BlockMesh();
				blockMesh.AppendModelMeshPart(model.FindMesh(m_tipNames[num], true).MeshParts[0], boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, m_offsets[num], 0f), false, false, false, false, Color.White);
				blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((float)(m_tipTextureSlots[num] % 16) / 16f, (float)(m_tipTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh2 = new BlockMesh();
				blockMesh2.AppendModelMeshPart(model.FindMesh(m_shaftNames[num], true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, m_offsets[num], 0f), false, false, false, false, Color.White);
				blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation((float)(m_shaftTextureSlots[num] % 16) / 16f, (float)(m_shaftTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh3 = new BlockMesh();
				blockMesh3.AppendModelMeshPart(model.FindMesh(m_stabilizerNames[num], true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, m_offsets[num], 0f), false, false, true, false, Color.White);
				blockMesh3.TransformTextureCoordinates(Matrix.CreateTranslation((float)(m_stabilizerTextureSlots[num] % 16) / 16f, (float)(m_stabilizerTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh4 = new BlockMesh();
				blockMesh4.AppendBlockMesh(blockMesh);
				blockMesh4.AppendBlockMesh(blockMesh2);
				blockMesh4.AppendBlockMesh(blockMesh3);

				m_standaloneBlockMeshes.Add(blockMesh4);
			}

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int arrowType = GetArrowType(Terrain.ExtractData(value));
			if (arrowType >= 0 && arrowType < m_standaloneBlockMeshes.Count)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[arrowType], color, 2f * size, ref matrix, environmentData);
			}
		}

		public override float GetProjectilePower(int value)
		{
			int arrowType = GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= m_weaponPowers.Length)
				return 0f;
			return m_weaponPowers[arrowType];
		}

		public override float GetExplosionPressure(int value)
		{
			int arrowType = GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= m_explosionPressures.Length)
				return 0f;
			return m_explosionPressures[arrowType];
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			int arrowType = GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= m_iconViewScales.Length)
				return 1f;
			return m_iconViewScales[arrowType];
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			for (int i = 0; i < 4; i++)
			{
				yield return Terrain.MakeBlockValue(Index, 0, SetArrowType(0, i));
			}
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int arrowType = GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= 4)
				return string.Empty;
			return LanguageControl.Get(GetType().Name, arrowType);
		}

		public static int GetArrowType(int data)
		{
			return data & 15;
		}

		public static int SetArrowType(int data, int arrowType)
		{
			return (data & -16) | (arrowType & 15);
		}

		public enum ArrowType
		{
			CopperRepeatingBolt,
			IronRepeatingBolt,
			DiamondRepeatingBolt,
			ExplosiveRepeatingBolt,
		}
	}
}