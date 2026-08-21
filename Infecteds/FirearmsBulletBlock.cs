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

			// Configuración base para proyectiles
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

		// ====================================================================
		// SISTEMA DE DAÑO - Siguiendo el patrón del Block original
		// Estructura de datos: Bits 0-3 = Tipo de bala, Bits 4-15 = Daño
		// ====================================================================

		/// <summary>
		/// Obtiene el daño actual almacenado en el bloque (bits 4-15)
		/// </summary>
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 4 & 4095;
		}

		/// <summary>
		/// Establece el daño en el bloque, preservando el tipo de bala (primeros 4 bits)
		/// </summary>
		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= 15; // Preservar solo el tipo de bala (bits 0-3)
			num |= Math.Clamp(damage, 0, 4095) << 4; // Establecer daño en bits 4-15
			return Terrain.ReplaceData(value, num);
		}

		/// <summary>
		/// Valor que queda cuando la bala es destruida por daño (0 = desaparece)
		/// </summary>
		public override int GetDamageDestructionValue(int value)
		{
			return 0;
		}

		/// <summary>
		/// Calcula la salud del bloque (0 a 1, donde 1 es sin daño)
		/// </summary>
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

		/// <summary>
		/// Obtiene la durabilidad según el tipo de bala
		/// </summary>
		public override int GetDurability(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDurability(type);
		}

		// ====================================================================
		// SISTEMA DE PROYECTILES - Lo que realmente causa daño a entidades
		// ====================================================================

		/// <summary>
		/// Poder del proyectil - ES LO QUE REALMENTE CAUSA DAÑO A ENTIDADES
		/// El motor del juego usa este valor para calcular el daño
		/// </summary>
		public override float GetProjectilePower(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDamage(type);
		}

		/// <summary>
		/// Amortiguación del proyectil (1.0 = sin pérdida de velocidad)
		/// </summary>
		public override float GetProjectileDamping(int value)
		{
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			return GetBulletDamping(type);
		}

		/// <summary>
		/// Resistencia a impactos de otros proyectiles
		/// </summary>
		public override float GetProjectileResilience(int value)
		{
			return 0f; // Las balas son vulnerables a otros proyectiles
		}

		// ====================================================================
		// ENUM Y MÉTODOS ESTÁTICOS
		// ====================================================================

		public enum FirearmsBulletType
		{
			AK47Bullet,
			DesertEagleBullet,
			SPAS12Bullet,
			SniperBullet,
			RevolverBullet
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
				default:
					return Color.White;
			}
		}

		/// <summary>
		/// Daño que causa cada tipo de bala (usado por GetProjectilePower)
		/// </summary>
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
				default:
					return 10f;
			}
		}

		/// <summary>
		/// Durabilidad de cada tipo de bala (veces que puede recibir daño antes de destruirse)
		/// </summary>
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
				default:
					return 1;
			}
		}

		/// <summary>
		/// Amortiguación de cada tipo de bala (qué tan rápido pierde velocidad)
		/// </summary>
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
				default:
					return 0.8f;
			}
		}
	}
}