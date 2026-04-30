using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RepeatingBoltBlock : Block
	{
		public static int Index = 301;

		public enum RepeatingBoltType
		{
			RepeatingCopperBolt = 0,      // NUEVO: cobre primero
			RepeatingIronBolt = 1,
			RepeatingDiamondBolt = 2,
			RepeatingExplosiveBolt = 3
		}

		private List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/repeat bolt");

			foreach (RepeatingBoltType type in Enum.GetValues(typeof(RepeatingBoltType)))
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
			int index = Terrain.ExtractData(value) & 0x3;
			if (index >= 0 && index < m_standaloneBlockMeshes.Count)
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[index], color, 2f * size, ref matrix, environmentData);
		}

		public static RepeatingBoltType GetBoltType(int data)
		{
			return (RepeatingBoltType)(data & 0x3);
		}

		public static int SetBoltType(int data, RepeatingBoltType boltType)
		{
			return (data & ~0x3) | ((int)boltType & 0x3);
		}

		public override float GetProjectilePower(int value)
		{
			return GetBoltType(Terrain.ExtractData(value)) switch
			{
				RepeatingBoltType.RepeatingCopperBolt => 20f,      // NUEVO: daño de cobre
				RepeatingBoltType.RepeatingIronBolt => 28f,
				RepeatingBoltType.RepeatingDiamondBolt => 36f,
				RepeatingBoltType.RepeatingExplosiveBolt => 8f,
				_ => 0f
			};
		}

		public override float GetExplosionPressure(int value)
		{
			return GetBoltType(Terrain.ExtractData(value)) switch
			{
				RepeatingBoltType.RepeatingExplosiveBolt => 40f,
				_ => 0f
			};
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			return 1.1f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(Index, 0, SetBoltType(0, RepeatingBoltType.RepeatingCopperBolt));   // NUEVO
			yield return Terrain.MakeBlockValue(Index, 0, SetBoltType(0, RepeatingBoltType.RepeatingIronBolt));
			yield return Terrain.MakeBlockValue(Index, 0, SetBoltType(0, RepeatingBoltType.RepeatingDiamondBolt));
			yield return Terrain.MakeBlockValue(Index, 0, SetBoltType(0, RepeatingBoltType.RepeatingExplosiveBolt));
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			return GetBoltType(Terrain.ExtractData(value)) switch
			{
				RepeatingBoltType.RepeatingCopperBolt => "Virote Repetidor de Cobre",     // NUEVO
				RepeatingBoltType.RepeatingIronBolt => "Virote Repetidor de Hierro",
				RepeatingBoltType.RepeatingDiamondBolt => "Virote Repetidor de Diamante",
				RepeatingBoltType.RepeatingExplosiveBolt => "Virote Repetidor Explosivo",
				_ => "Virote Repetidor"
			};
		}

		// Texturas: cobre (punta 181), hierro (63), diamante (182), explosivo (225)
		private static int[] m_tipTextureSlots = { 181, 63, 182, 225 };
		private static int[] m_shaftTextureSlots = { 63, 63, 63, 63 };
		private static int[] m_stabTextureSlots = { 63, 63, 63, 63 };
	}
}