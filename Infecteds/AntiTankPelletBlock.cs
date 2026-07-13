using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class AntiTankPelletBlock : FlatBlock
	{
		// Cambia este índice al que le hayas asignado en tu proyecto/mod
		public new static int Index = 514;

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Escala modificada
			float size2 = size * 3.5f;

			Color purpleColor = new Color(153, 0, 199);
			Color finalColor = color * purpleColor;

			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size2, ref matrix, null, finalColor, false, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			// Daño significativamente mayor al original (MusketBall es 80f)
			return 1000000f;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			// Usamos la misma textura que la bala de mosquete original (229)
			return 229;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			// CORREGIDO: Se agregan los parámetros faltantes (light = 0, data = 0)
			yield return Terrain.MakeBlockValue(Index, 0, 0);
		}
	}
}
