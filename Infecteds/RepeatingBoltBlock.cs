using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RepeatingBoltBlock : Block
	{
		public static int Index = 301;

		private List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/repeat bolt");

			foreach (ArrowBlock.ArrowType type in new[] { ArrowBlock.ArrowType.IronBolt, ArrowBlock.ArrowType.DiamondBolt, ArrowBlock.ArrowType.ExplosiveBolt })
			{
				int idx = (int)type;
				Matrix tipBone = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("ArrowTip", true).ParentBone);
				Matrix shaftBone = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("ArrowShaft", true).ParentBone);
				Matrix stabBone = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("ArrowStabilizer", true).ParentBone);

				float offset = -0.3f;

				BlockMesh tipMesh = new BlockMesh();
				tipMesh.AppendModelMeshPart(model.FindMesh("ArrowTip", true).MeshParts[0],
					tipBone * Matrix.CreateTranslation(0, offset, 0), false, false, false, false, Color.White);
				tipMesh.TransformTextureCoordinates(Matrix.CreateTranslation((m_tipTextureSlots[idx] % 16) / 16f, (m_tipTextureSlots[idx] / 16) / 16f, 0f), -1);

				BlockMesh shaftMesh = new BlockMesh();
				shaftMesh.AppendModelMeshPart(model.FindMesh("ArrowShaft", true).MeshParts[0],
					shaftBone * Matrix.CreateTranslation(0, offset, 0), false, false, false, false, Color.White);
				shaftMesh.TransformTextureCoordinates(Matrix.CreateTranslation((m_shaftTextureSlots[idx] % 16) / 16f, (m_shaftTextureSlots[idx] / 16) / 16f, 0f), -1);

				BlockMesh stabMesh = new BlockMesh();
				stabMesh.AppendModelMeshPart(model.FindMesh("ArrowStabilizer", true).MeshParts[0],
					stabBone * Matrix.CreateTranslation(0, offset, 0), false, false, true, false, Color.White);
				stabMesh.TransformTextureCoordinates(Matrix.CreateTranslation((m_stabTextureSlots[idx] % 16) / 16f, (m_stabTextureSlots[idx] / 16) / 16f, 0f), -1);

				BlockMesh combined = new BlockMesh();
				combined.AppendBlockMesh(tipMesh);
				combined.AppendBlockMesh(shaftMesh);
				combined.AppendBlockMesh(stabMesh);
				m_standaloneBlockMeshes.Add(combined);
			}

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// IDÉNTICO al ArrowBlock original: usa directamente el ArrowType como índice
			int arrowType = (int)ArrowBlock.GetArrowType(Terrain.ExtractData(value));
			// Los virotes están en las posiciones 5, 6, 7 del enum; las mapeamos a 0,1,2
			int index = arrowType - 5;
			if (index >= 0 && index < m_standaloneBlockMeshes.Count)
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[index], color, 2f * size, ref matrix, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			return (ArrowBlock.GetArrowType(Terrain.ExtractData(value))) switch
			{
				ArrowBlock.ArrowType.IronBolt => 28f,
				ArrowBlock.ArrowType.DiamondBolt => 36f,
				ArrowBlock.ArrowType.ExplosiveBolt => 8f,
				_ => 0f
			};
		}

		public override float GetExplosionPressure(int value)
		{
			return (ArrowBlock.GetArrowType(Terrain.ExtractData(value))) switch
			{
				ArrowBlock.ArrowType.ExplosiveBolt => 40f,
				_ => 0f
			};
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			return 1.1f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, ArrowBlock.SetArrowType(0, ArrowBlock.ArrowType.IronBolt));
			yield return Terrain.MakeBlockValue(Index, 0, ArrowBlock.SetArrowType(0, ArrowBlock.ArrowType.DiamondBolt));
			yield return Terrain.MakeBlockValue(Index, 0, ArrowBlock.SetArrowType(0, ArrowBlock.ArrowType.ExplosiveBolt));
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			return (ArrowBlock.GetArrowType(Terrain.ExtractData(value))) switch
			{
				ArrowBlock.ArrowType.IronBolt => "Virote de Hierro",
				ArrowBlock.ArrowType.DiamondBolt => "Virote de Diamante",
				ArrowBlock.ArrowType.ExplosiveBolt => "Virote Explosivo",
				_ => "Virote"
			};
		}

		private static int[] m_tipTextureSlots = { 0, 0, 0, 0, 0, 63, 182, 183, 0 };
		private static int[] m_shaftTextureSlots = { 0, 0, 0, 0, 0, 63, 63, 63, 0 };
		private static int[] m_stabTextureSlots = { 0, 0, 0, 0, 0, 63, 63, 63, 0 };
	}
}