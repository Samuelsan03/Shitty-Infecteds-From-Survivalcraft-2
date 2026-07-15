using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FlameBulletBlock : FlatBlock
	{
		public static int Index = 514;

		public enum FlameBulletType
		{
			Fire,
			Poison
		}

		private static readonly float[] m_sizes = new float[] { 1f, 1f };
		private static readonly int[] m_textureSlots = new int[] { 229, 229 };
		private static readonly Color[] m_colors = new Color[] { new Color(255, 128, 0), Color.Green };
		private static readonly float[] m_weaponPowers = new float[] { 1f, 1f }; // Daño moderado

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int type = (int)GetBulletType(Terrain.ExtractData(value));
			float size2 = (type >= 0 && type < m_sizes.Length) ? (size * m_sizes[type]) : size;
			Color bulletColor = (type >= 0 && type < m_colors.Length) ? m_colors[type] : Color.White;
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size2, ref matrix, null, bulletColor, false, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			int type = (int)GetBulletType(Terrain.ExtractData(value));
			if (type >= 0 && type < m_weaponPowers.Length)
				return m_weaponPowers[type];
			return 0f;
		}

		public override float GetExplosionPressure(int value) => 0f;

		public override IEnumerable<int> GetCreativeValues()
		{
			foreach (FlameBulletType type in Enum.GetValues(typeof(FlameBulletType)))
				yield return Terrain.MakeBlockValue(Index, 0, SetBulletType(0, type));
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int type = (int)GetBulletType(Terrain.ExtractData(value));
			if (type < 0 || type >= Enum.GetValues<FlameBulletType>().Length)
				return string.Empty;
			return type == 0 ? "Flame Bullet" : "Poison Bullet";
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			int type = (int)GetBulletType(Terrain.ExtractData(value));
			if (type < 0 || type >= m_textureSlots.Length)
				return 229;
			return m_textureSlots[type];
		}

		public static FlameBulletType GetBulletType(int data) => (FlameBulletType)(data & 15);
		public static int SetBulletType(int data, FlameBulletType bulletType) => (data & -16) | (int)(bulletType & (FlameBulletType)15);
	}
}
