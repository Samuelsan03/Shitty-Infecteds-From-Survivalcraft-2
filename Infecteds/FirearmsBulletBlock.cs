using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FirearmsBulletBlock : Block
	{
		public const int Index = 1002;

		private Texture2D m_texture;

		public override void Initialize()
		{
			base.Initialize();
			m_texture = ContentManager.Get<Texture2D>("Textures/Experience");
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override int GetTextureSlotCount(int value)
		{
			return 1;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 0;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			Color bulletColor = GetBulletColor(type);

			float drawSize = (environmentData.SubsystemTerrain != null) ? 0.04f : size;

			BlocksManager.DrawFlatBlock(primitivesRenderer, value, drawSize, ref matrix, m_texture, bulletColor, true, environmentData);
		}

		public enum FirearmsBulletType
		{
			AK47Bullet,
			DesertEagleBullet,
			SPAS12Bullet, // Añadido para el SPAS-12
		}

		public static FirearmsBulletType GetFirearmsBulletType(int data)
		{
			return (FirearmsBulletType)(data & 0xF);
		}

		public static int SetFirearmsBulletType(int data, FirearmsBulletType type)
		{
			return (data & ~0xF) | (int)type;
		}

		public static Color GetBulletColor(FirearmsBulletType type)
		{
			switch (type)
			{
				case FirearmsBulletType.AK47Bullet:
					return new Color(255, 180, 0); // Naranja para AK
				case FirearmsBulletType.DesertEagleBullet:
					return new Color(220, 220, 230); // Plateado para .50 AE
				case FirearmsBulletType.SPAS12Bullet:
					return new Color(200, 150, 50); // Marrón/dorado para perdigones
				default:
					return Color.White;
			}
		}

		public static float GetBulletDamage(FirearmsBulletType type)
		{
			switch (type)
			{
				case FirearmsBulletType.AK47Bullet:
					return 25f;
				case FirearmsBulletType.DesertEagleBullet:
					return 60f;
				case FirearmsBulletType.SPAS12Bullet:
					return 15f; // Cada perdigón hace menos daño, pero hay 8
				default:
					return 10f;
			}
		}
	}
}