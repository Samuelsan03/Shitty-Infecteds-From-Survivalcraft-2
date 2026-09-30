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
			return (Terrain.ExtractData(value) >> 8) & 0xFF;  // daño en bits 8..15
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= 0xFF;                                       // conservar tipo (bits 0..7)
			num |= Math.Clamp(damage, 0, 255) << 8;            // daño en bits 8..15
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
			MP5SSDBullet,
			M249Bullet,
			SCARHBullet,
			FAMASBullet,
			AA12Bullet,
			FNP90Bullet,
			AUGBullet,
			FX05Bullet,
			G3Bullet,
			AK48Bullet,
			KABullet,
			MinigunBullet
		}

		public static FirearmsBulletType GetFirearmsBulletType(int data)
		{
			return (FirearmsBulletType)(data & 0xFF);          // 8 bits (0..255) para el tipo
		}

		public static int SetFirearmsBulletType(int data, FirearmsBulletType type)
		{
			return (data & ~0xFF) | (int)type;                // conservar el resto, poner tipo
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
				case FirearmsBulletType.MP5SSDBullet:
					return new Color(200, 200, 210);
				case FirearmsBulletType.M249Bullet:
					return new Color(255, 170, 30);
				case FirearmsBulletType.SCARHBullet:
					return new Color(210, 60, 30);
				case FirearmsBulletType.FAMASBullet:
					return new Color(240, 190, 80);
				case FirearmsBulletType.AA12Bullet:
					return new Color(220, 160, 60);
				case FirearmsBulletType.FNP90Bullet:
					return new Color(245, 222, 80);
				case FirearmsBulletType.AUGBullet:
					return new Color(255, 200, 50);
				case FirearmsBulletType.FX05Bullet:
					return new Color(80, 220, 240);
				case FirearmsBulletType.G3Bullet:
					return new Color(180, 140, 60);
				case FirearmsBulletType.AK48Bullet: // <-- AÑADIDO
					return new Color(255, 50, 50); // Color rojo brillante para diferenciarlo
				case FirearmsBulletType.KABullet:
					return new Color(180, 0, 0); // Rojo oscuro sangriento, ¡muy letal!
				case FirearmsBulletType.MinigunBullet:
					return new Color(255, 100, 0); // Color naranja fuego
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
				case FirearmsBulletType.MP5SSDBullet:
					return 20f;
				case FirearmsBulletType.M249Bullet:
					return 30f;
				case FirearmsBulletType.SCARHBullet:
					return 45f;
				case FirearmsBulletType.FAMASBullet:
					return 25f;
				case FirearmsBulletType.AA12Bullet:
					return 14f;
				case FirearmsBulletType.FNP90Bullet:
					return 20f;
				case FirearmsBulletType.AUGBullet:
					return 28f;
				case FirearmsBulletType.FX05Bullet:
					return 32f;
				case FirearmsBulletType.G3Bullet:
					return 35f;
				case FirearmsBulletType.AK48Bullet: // <-- AÑADIDO
					return 40f; // ¡Bala mucho más potente que el AK47!
				case FirearmsBulletType.KABullet:
					return 55f; // ¡Daño masivo! Superior al AK47 (25) y AK48 (40)
				case FirearmsBulletType.MinigunBullet:
					return 30f; // Daño moderado-alto por bala (Pero dispara muchísimas)
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
				case FirearmsBulletType.M249Bullet:
					return 1;
				case FirearmsBulletType.SCARHBullet:
					return 1;
				case FirearmsBulletType.FAMASBullet:
					return 1;
				case FirearmsBulletType.AA12Bullet:
					return 1;
				case FirearmsBulletType.FNP90Bullet:
					return 1;
				case FirearmsBulletType.AUGBullet:
					return 1;
				case FirearmsBulletType.FX05Bullet:
					return 1;
				case FirearmsBulletType.G3Bullet:
					return 1;
				case FirearmsBulletType.AK48Bullet: // <-- AÑADIDO
					return 1;
				case FirearmsBulletType.KABullet:
					return 1;
				case FirearmsBulletType.MinigunBullet:
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
				case FirearmsBulletType.MP5SSDBullet:
					return 0.94f;
				case FirearmsBulletType.M249Bullet:
					return 0.96f;
				case FirearmsBulletType.SCARHBullet:
					return 0.97f;
				case FirearmsBulletType.FAMASBullet:
					return 0.97f;
				case FirearmsBulletType.AA12Bullet:
					return 0.90f;
				case FirearmsBulletType.FNP90Bullet:
					return 0.96f;
				case FirearmsBulletType.AUGBullet:
					return 0.97f;
				case FirearmsBulletType.FX05Bullet:
					return 0.98f;
				case FirearmsBulletType.G3Bullet:
					return 0.98f;
				case FirearmsBulletType.AK48Bullet: // <-- AÑADIDO
					return 0.99f; // ¡Mayor alcance y precisión que el AK47!
				case FirearmsBulletType.KABullet:
					return 0.995f; // Casi sin caída, altísima velocidad y precisión
				case FirearmsBulletType.MinigunBullet:
					return 0.98f; // Muy poca caída, alcance letal
				default:
					return 0.8f;
			}
		}
	}
}
