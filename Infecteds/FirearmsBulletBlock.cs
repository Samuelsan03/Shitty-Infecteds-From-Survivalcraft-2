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

			IsCollidable = false;
			IsTransparent = true;
			IsPlaceable = false;
			DisintegratesOnHit = true;
			Durability = 1;
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

		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 4 & 4095;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= 15;
			num |= Math.Clamp(damage, 0, 4095) << 4;
			return Terrain.ReplaceData(value, num);
		}

		public override int GetDamageDestructionValue(int value)
		{
			return 0;
		}

		public override float GetBlockHealth(int value)
		{
			int durability = GetDurability(value);
			int damage = GetDamage(value);
			if (durability > 0)
			{
				return (float)(durability - damage) / (float)durability;
			}
			return -1f;
		}

		public override int GetDurability(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDurability(type);
		}

		public override float GetProjectilePower(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDamage(type);
		}

		public override float GetProjectileDamping(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDamping(type);
		}

		public override float GetProjectileResilience(int value)
		{
			return 0f;
		}

		// ENUM Y MÉTODOS ESTÁTICOS
		public enum FirearmsBulletType
		{
			AK47Bullet,
			DesertEagleBullet,
			SPAS12Bullet,
			SniperBullet,
			RevolverBullet,
			IZH43Bullet,
			Mac10Bullet,
			M4Bullet,
			UziBullet,
			BK93Bullet,
			Master308Bullet,
			MP5SSDBullet  // ✅ NUEVA BALA MP5SSD
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
					return new Color(255, 180, 0);
				case FirearmsBulletType.DesertEagleBullet:
					return new Color(220, 220, 230);
				case FirearmsBulletType.SPAS12Bullet:
					return new Color(200, 150, 50);
				case FirearmsBulletType.SniperBullet:
					return new Color(180, 180, 190);
				case FirearmsBulletType.RevolverBullet:
					return new Color(200, 180, 100);
				case FirearmsBulletType.IZH43Bullet:
					return new Color(180, 140, 60);
				case FirearmsBulletType.Mac10Bullet:
					return new Color(255, 200, 50);
				case FirearmsBulletType.M4Bullet:
					return new Color(230, 190, 100);
				case FirearmsBulletType.UziBullet:
					return new Color(255, 190, 60);
				case FirearmsBulletType.BK93Bullet:
					return new Color(190, 145, 55);
				case FirearmsBulletType.Master308Bullet:
					return new Color(200, 170, 120);
				case FirearmsBulletType.MP5SSDBullet:  // ✅ COLOR BALA MP5SSD
					return new Color(200, 200, 210);
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
					return 15f;
				case FirearmsBulletType.SniperBullet:
					return 150f;
				case FirearmsBulletType.RevolverBullet:
					return 45f;
				case FirearmsBulletType.IZH43Bullet:
					return 15f;
				case FirearmsBulletType.Mac10Bullet:
					return 18f;
				case FirearmsBulletType.M4Bullet:
					return 22f;
				case FirearmsBulletType.UziBullet:
					return 15f;
				case FirearmsBulletType.BK93Bullet:
					return 15f;
				case FirearmsBulletType.Master308Bullet:
					return 120f;
				case FirearmsBulletType.MP5SSDBullet:  // ✅ DAÑO MP5SSD (9mm subsonic)
					return 20f;
				default:
					return 10f;
			}
		}

		public static int GetBulletDurability(FirearmsBulletType type)
		{
			switch (type)
			{
				case FirearmsBulletType.AK47Bullet:
					return 1;
				case FirearmsBulletType.DesertEagleBullet:
					return 1;
				case FirearmsBulletType.SPAS12Bullet:
					return 1;
				case FirearmsBulletType.SniperBullet:
					return 1;
				case FirearmsBulletType.RevolverBullet:
					return 1;
				case FirearmsBulletType.IZH43Bullet:
					return 1;
				case FirearmsBulletType.Mac10Bullet:
					return 1;
				case FirearmsBulletType.M4Bullet:
					return 1;
				case FirearmsBulletType.UziBullet:
					return 1;
				case FirearmsBulletType.BK93Bullet:
					return 1;
				case FirearmsBulletType.Master308Bullet:
					return 1;
				case FirearmsBulletType.MP5SSDBullet:
					return 1;
				default:
					return 1;
			}
		}

		public static float GetBulletDamping(FirearmsBulletType type)
		{
			switch (type)
			{
				case FirearmsBulletType.AK47Bullet:
					return 0.95f;
				case FirearmsBulletType.DesertEagleBullet:
					return 0.97f;
				case FirearmsBulletType.SPAS12Bullet:
					return 0.90f;
				case FirearmsBulletType.SniperBullet:
					return 0.99f;
				case FirearmsBulletType.RevolverBullet:
					return 0.96f;
				case FirearmsBulletType.IZH43Bullet:
					return 0.88f;
				case FirearmsBulletType.Mac10Bullet:
					return 0.93f;
				case FirearmsBulletType.M4Bullet:
					return 0.96f;
				case FirearmsBulletType.UziBullet:
					return 0.95f;
				case FirearmsBulletType.BK93Bullet:
					return 0.88f;
				case FirearmsBulletType.Master308Bullet:
					return 0.98f;
				case FirearmsBulletType.MP5SSDBullet:  // ✅ DAMPING MP5SSD (subsonic)
					return 0.94f;
				default:
					return 0.8f;
			}
		}
	}
}