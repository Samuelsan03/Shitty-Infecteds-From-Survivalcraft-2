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
			// No genera vértices en el terreno
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
			// Obtener el tipo de bala del data
			FirearmsBulletType type = GetFirearmsBulletType(Terrain.ExtractData(value));
			// Obtener el color según el tipo
			Color bulletColor = GetBulletColor(type);
			// Dibujar como experiencia pero con el color de la bala
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.18f, ref matrix, m_texture, bulletColor, true, environmentData);
		}

		// ===== ENUM DE TIPOS DE BALA =====
		public enum FirearmsBulletType
		{
			AK47Bullet,
			// Agregar más tipos aquí en el futuro
		}

		// ===== MÉTODOS PARA MANEJAR EL ENUM EN EL DATA =====
		public static FirearmsBulletType GetFirearmsBulletType(int data)
		{
			return (FirearmsBulletType)(data & 0xF);
		}

		public static int SetFirearmsBulletType(int data, FirearmsBulletType type)
		{
			return (data & ~0xF) | (int)type;
		}

		// ===== COLOR Y DAÑO POR TIPO =====
		public static Color GetBulletColor(FirearmsBulletType type)
		{
			switch (type)
			{
				case FirearmsBulletType.AK47Bullet:
					return new Color(255, 180, 0); // Amarillo
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
				default:
					return 10f;
			}
		}
	}
}
