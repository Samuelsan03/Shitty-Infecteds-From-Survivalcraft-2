using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class ShittyInfectedsFlatBlock : Block
	{
		private string m_texturePath;
		protected Texture2D m_texture;

		protected ShittyInfectedsFlatBlock(string texturePath)
		{
			this.m_texturePath = texturePath;
		}

		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>(this.m_texturePath);
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			int data = Terrain.ExtractData(value);
			int rotation = data & 3;

			generator.GenerateFlatVertices(this, value, x, y, z, rotation, Color.White, geometry.OpaqueSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar los datos de entorno por defecto si no hay ninguno
			environmentData = (environmentData ?? BlocksManager.m_defaultEnvironmentData);

			// Aplicar iluminación al color
			float lightIntensity = LightingManager.LightIntensityByLightValue[environmentData.Light];
			Color drawColor = Color.MultiplyColorOnly(color, lightIntensity);

			Vector3 translation = matrix.Translation;
			Vector3 right = matrix.Right;
			Vector3 up = matrix.Up;

			// Escala del bloque plano (0.85 es el tamaño estándar que usa el juego)
			float scale = 0.85f * size;

			// Generar los 4 puntos de la cara plana (plano XY)
			Vector3 p1 = translation + scale * (-right - up);
			Vector3 p2 = translation + scale * (right - up);
			Vector3 p3 = translation + scale * (-right + up);
			Vector3 p4 = translation + scale * (right + up);

			// Aplicar la matriz de Vista/Proyección si existe
			if (environmentData.ViewProjectionMatrix != null)
			{
				Matrix vp = environmentData.ViewProjectionMatrix.Value;
				Vector3.Transform(ref p1, ref vp, out p1);
				Vector3.Transform(ref p2, ref vp, out p2);
				Vector3.Transform(ref p3, ref vp, out p3);
				Vector3.Transform(ref p4, ref vp, out p4);
			}

			// Iniciar el batch de texturas 3D
			TexturedBatch3D texturedBatch = primitivesRenderer.TexturedBatch(this.m_texture, true, 0, null, RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.PointClamp);

			// Coordenadas UV completas (0 a 1) porque usamos una textura independiente, no un atlas
			Vector2 uvBottomLeft = new Vector2(0f, 1f);
			Vector2 uvBottomRight = new Vector2(1f, 1f);
			Vector2 uvTopLeft = new Vector2(0f, 0f);
			Vector2 uvTopRight = new Vector2(1f, 0f);

			// Dibujar la cara frontal
			texturedBatch.QueueQuad(p1, p3, p4, p2, uvBottomLeft, uvTopLeft, uvTopRight, uvBottomRight, drawColor);

			// Dibujar la cara trasera (para que no desaparezca al rotar la cámara)
			texturedBatch.QueueQuad(p1, p2, p4, p3, uvBottomLeft, uvBottomRight, uvTopRight, uvTopLeft, drawColor);
		}
	}
}
