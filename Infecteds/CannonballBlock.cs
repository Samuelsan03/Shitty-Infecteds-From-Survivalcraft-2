using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CannonballBlock : FlatBlock
	{
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Dark iron color (override the passed-in color)
			Color cannonballColor = new Color(45, 45, 50, 255);
			// Bigger than a normal bullet
			float cannonballSize = size * 1.8f;
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, cannonballSize, ref matrix, this.GetDefaultTexture(value), cannonballColor, false, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			return 120f;
		}

		public override float GetExplosionPressure(int value)
		{
			return 8f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(CannonballBlock.Index, 0, 0);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			return LanguageControl.Get("CannonballBlock", 0);
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 229;
		}

		public static int Index = 531;
	}
}
