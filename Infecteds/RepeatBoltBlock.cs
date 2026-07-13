using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RepeatBoltBlock : Block
	{
		public static int Index = 511;

		public List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();

		private static string[] m_tipNames = new string[]
		{
			"ArrowTip",
			"ArrowTip",
			"ArrowTip",
			"ArrowTip",
			"ArrowTip",
			"ArrowTip",
			"ArrowTip",
		};

		private static string[] m_shaftNames = new string[]
		{
			"ArrowShaft",
			"ArrowShaft",
			"ArrowShaft",
			"ArrowShaft",
			"ArrowShaft",
			"ArrowShaft",
			"ArrowShaft",
		};

		private static string[] m_stabilizerNames = new string[]
		{
			"ArrowStabilizer",
			"ArrowStabilizer",
			"ArrowStabilizer",
			"ArrowStabilizer",
			"ArrowStabilizer",
			"ArrowStabilizer",
			"ArrowStabilizer",
		};

		private static int[] m_tipTextureSlots = new int[]
		{
			79,
			63,
			182,
			225,
			62,
			100,
			100,
		};

		private static int[] m_shaftTextureSlots = new int[]
		{
			63,
			63,
			63,
			63,
			63,
			63,
			63,
		};

		private static int[] m_stabilizerTextureSlots = new int[]
		{
			15,
			63,
			63,
			63,
			63,
			63,
			63,
		};

		private static float[] m_offsets = new float[]
		{
			-0.45f,
			-0.45f,
			-0.45f,
			-0.45f,
			-0.45f,
			-0.45f,
			-0.45f,
		};

		private static float[] m_weaponPowers = new float[]
		{
			12f,
			30f,
			40f,
			12f,
			4f,
			2f,
			3f,
		};

		private static float[] m_iconViewScales = new float[]
		{
			0.8f,
			0.8f,
			0.8f,
			0.8f,
			0.8f,
			0.8f,
			0.8f,
		};

		private static float[] m_explosionPressures = new float[]
		{
			0f,
			0f,
			0f,
			60f,
			0f,
			0f,
			0f,
		};

		private static string[] m_displayNames = new string[]
		{
			"Repeat Copper Bolt",
			"Repeat Iron Bolt",
			"Repeat Diamond Bolt",
			"Repeat Explosive Bolt",
			"Repeat Fire Bolt",
			"Poison Bolt",
			"Severely Poisonous Bolt",
		};

		private static Color[] m_tipColors = new Color[]
		{
			Color.White,
			Color.White,
			Color.White,
			Color.White,
			Color.White,
			Color.White,
			new Color(0, 128, 13),
		};

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/RepeatBolt");

			int typeIndex = 0;
			foreach (int num in EnumUtils.GetEnumValues<RepeatBoltType>())
			{
				if (num > 15) throw new InvalidOperationException("Too many bolt types.");

				string tipName = m_tipNames[typeIndex];
				string shaftName = m_shaftNames[typeIndex];
				string stabilizerName = m_stabilizerNames[typeIndex];
				Color tipColor = m_tipColors[typeIndex];

				Matrix tipTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(tipName, true).ParentBone);
				Matrix shaftTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(shaftName, true).ParentBone);
				Matrix stabilizerTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(stabilizerName, true).ParentBone);

				BlockMesh tipMesh = new BlockMesh();
				tipMesh.AppendModelMeshPart(model.FindMesh(tipName, true).MeshParts[0],
					tipTransform * Matrix.CreateTranslation(0f, m_offsets[typeIndex], 0f),
					false, false, false, false, tipColor);
				tipMesh.TransformTextureCoordinates(
					Matrix.CreateTranslation((float)(m_tipTextureSlots[typeIndex] % 16) / 16f,
					(float)(m_tipTextureSlots[typeIndex] / 16) / 16f, 0f), -1);

				BlockMesh shaftMesh = new BlockMesh();
				shaftMesh.AppendModelMeshPart(model.FindMesh(shaftName, true).MeshParts[0],
					shaftTransform * Matrix.CreateTranslation(0f, m_offsets[typeIndex], 0f),
					false, false, false, false, Color.White);
				shaftMesh.TransformTextureCoordinates(
					Matrix.CreateTranslation((float)(m_shaftTextureSlots[typeIndex] % 16) / 16f,
					(float)(m_shaftTextureSlots[typeIndex] / 16) / 16f, 0f), -1);

				BlockMesh stabilizerMesh = new BlockMesh();
				stabilizerMesh.AppendModelMeshPart(model.FindMesh(stabilizerName, true).MeshParts[0],
					stabilizerTransform * Matrix.CreateTranslation(0f, m_offsets[typeIndex], 0f),
					false, false, true, false, Color.White);
				stabilizerMesh.TransformTextureCoordinates(
					Matrix.CreateTranslation((float)(m_stabilizerTextureSlots[typeIndex] % 16) / 16f,
					(float)(m_stabilizerTextureSlots[typeIndex] / 16) / 16f, 0f), -1);

				BlockMesh combined = new BlockMesh();
				combined.AppendBlockMesh(tipMesh);
				combined.AppendBlockMesh(shaftMesh);
				combined.AppendBlockMesh(stabilizerMesh);

				m_standaloneBlockMeshes.Add(combined);
				typeIndex++;
			}

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int boltType = (int)GetRepeatBoltType(Terrain.ExtractData(value));
			if (boltType >= 0 && boltType < m_standaloneBlockMeshes.Count)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[boltType], color, 2f * size, ref matrix, environmentData);
			}
		}

		public override float GetProjectilePower(int value)
		{
			int type = (int)GetRepeatBoltType(Terrain.ExtractData(value));
			if (type >= 0 && type < m_weaponPowers.Length)
				return m_weaponPowers[type];
			return 0f;
		}

		public override float GetExplosionPressure(int value)
		{
			int type = (int)GetRepeatBoltType(Terrain.ExtractData(value));
			if (type >= 0 && type < m_explosionPressures.Length)
				return m_explosionPressures[type];
			return 0f;
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			int type = (int)GetRepeatBoltType(Terrain.ExtractData(value));
			if (type >= 0 && type < m_iconViewScales.Length)
				return m_iconViewScales[type];
			return 1f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			foreach (RepeatBoltType type in EnumUtils.GetEnumValues<RepeatBoltType>())
			{
				yield return Terrain.MakeBlockValue(Index, 0, SetRepeatBoltType(0, type));
			}
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int type = (int)GetRepeatBoltType(Terrain.ExtractData(value));
			if (type >= 0 && type < m_displayNames.Length)
				return m_displayNames[type];
			return type.ToString();
		}

		public static RepeatBoltType GetRepeatBoltType(int data)
		{
			return (RepeatBoltType)(data & 15);
		}

		public static int SetRepeatBoltType(int data, RepeatBoltType boltType)
		{
			return (data & -16) | (int)(boltType & (RepeatBoltType)15);
		}

		public enum RepeatBoltType
		{
			RepeatCopperBolt,
			RepeatIronBolt,
			RepeatDiamondBolt,
			RepeatExplosiveBolt,
			RepeatFireBolt,
			RepeatPoisonBolt,
			RepeatSeverelyPoisonousBolt,
		}
	}
}
