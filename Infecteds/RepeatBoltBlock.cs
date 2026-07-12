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

		// Nombres de mallas (todos usan el mismo modelo, solo cambian texturas)
		private static string[] m_tipNames = new string[]
		{
			"ArrowTip",     // RepeatCopperBolt
            "ArrowTip",     // RepeatIronBolt
            "ArrowTip",     // RepeatDiamondBolt
            "ArrowTip",     // RepeatExplosiveBolt
            "ArrowTip",     // RepeatFireBolt
        };

		private static string[] m_shaftNames = new string[]
		{
			"ArrowShaft",   // RepeatCopperBolt
            "ArrowShaft",   // RepeatIronBolt
            "ArrowShaft",   // RepeatDiamondBolt
            "ArrowShaft",   // RepeatExplosiveBolt
            "ArrowShaft",   // RepeatFireBolt
        };

		private static string[] m_stabilizerNames = new string[]
		{
			"ArrowStabilizer", // RepeatCopperBolt
            "ArrowStabilizer", // RepeatIronBolt
            "ArrowStabilizer", // RepeatDiamondBolt
            "ArrowStabilizer", // RepeatExplosiveBolt
            "ArrowStabilizer", // RepeatFireBolt
        };

		// Slots de textura (la punta de fuego usa 62, igual que FireArrow)
		private static int[] m_tipTextureSlots = new int[]
		{
			79,    // RepeatCopperBolt (igual que CopperArrow)
            63,    // RepeatIronBolt (igual que IronBolt)
            182,   // RepeatDiamondBolt (igual que DiamondBolt)
            229,   // RepeatExplosiveBolt (según tu indicación)
            62,    // RepeatFireBolt (igual que FireArrow)
        };

		private static int[] m_shaftTextureSlots = new int[]
		{
			63,     // RepeatCopperBolt (igual que CopperArrow)
            63,    // RepeatIronBolt
            63,    // RepeatDiamondBolt
            63,    // RepeatExplosiveBolt
            63,    // RepeatFireBolt (puedes poner 4 si quieres estilo flecha, pero lo dejo 63)
        };

		private static int[] m_stabilizerTextureSlots = new int[]
		{
			15,    // RepeatCopperBolt (igual que CopperArrow)
            63,    // RepeatIronBolt
            63,    // RepeatDiamondBolt
            63,    // RepeatExplosiveBolt
            63,    // RepeatFireBolt
        };

		// Offsets de posición (todos -0.45 como tú configuraste)
		private static float[] m_offsets = new float[]
		{
			-0.45f, // RepeatCopperBolt
            -0.45f, // RepeatIronBolt
            -0.45f, // RepeatDiamondBolt
            -0.45f, // RepeatExplosiveBolt
            -0.45f, // RepeatFireBolt
        };

		// Potencia de proyectil (valores ajustados para diferenciarse de los originales)
		private static float[] m_weaponPowers = new float[]
		{
			12f,    // RepeatCopperBolt (original 10, ahora 12)
            30f,    // RepeatIronBolt (original 28, ahora 30)
            40f,    // RepeatDiamondBolt (original 36, ahora 40)
            12f,    // RepeatExplosiveBolt (original 8, ahora 12)
            8f,     // RepeatFireBolt (similar a FireArrow: 4, pero subimos a 8)
        };

		// Escala de icono (0.8 para todos, como tú pusiste)
		private static float[] m_iconViewScales = new float[]
		{
			0.8f,   // RepeatCopperBolt
            0.8f,   // RepeatIronBolt
            0.8f,   // RepeatDiamondBolt
            0.8f,   // RepeatExplosiveBolt
            0.8f,   // RepeatFireBolt
        };

		// Presión explosiva (solo para el explosivo)
		private static float[] m_explosionPressures = new float[]
		{
			0f,     // RepeatCopperBolt
            0f,     // RepeatIronBolt
            0f,     // RepeatDiamondBolt
            40f,    // RepeatExplosiveBolt (igual que ExplosiveBolt vanilla)
            0f,     // RepeatFireBolt
        };

		// Nombres para mostrar (sin LanguageControl)
		private static string[] m_displayNames = new string[]
		{
			"Repeat Copper Bolt",
			"Repeat Iron Bolt",
			"Repeat Diamond Bolt",
			"Repeat Explosive Bolt",
			"Repeat Fire Bolt",
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

				Matrix tipTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(tipName, true).ParentBone);
				Matrix shaftTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(shaftName, true).ParentBone);
				Matrix stabilizerTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(stabilizerName, true).ParentBone);

				// Punta
				BlockMesh tipMesh = new BlockMesh();
				tipMesh.AppendModelMeshPart(model.FindMesh(tipName, true).MeshParts[0],
					tipTransform * Matrix.CreateTranslation(0f, m_offsets[typeIndex], 0f),
					false, false, false, false, Color.White);
				tipMesh.TransformTextureCoordinates(
					Matrix.CreateTranslation((float)(m_tipTextureSlots[typeIndex] % 16) / 16f,
					(float)(m_tipTextureSlots[typeIndex] / 16) / 16f, 0f), -1);

				// Asta
				BlockMesh shaftMesh = new BlockMesh();
				shaftMesh.AppendModelMeshPart(model.FindMesh(shaftName, true).MeshParts[0],
					shaftTransform * Matrix.CreateTranslation(0f, m_offsets[typeIndex], 0f),
					false, false, false, false, Color.White);
				shaftMesh.TransformTextureCoordinates(
					Matrix.CreateTranslation((float)(m_shaftTextureSlots[typeIndex] % 16) / 16f,
					(float)(m_shaftTextureSlots[typeIndex] / 16) / 16f, 0f), -1);

				// Estabilizador
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
		}
	}
}
