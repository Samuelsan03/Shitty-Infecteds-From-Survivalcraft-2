using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FlameThrowerBlock : Block
	{
		public static int Index = 513;

		private BlockMesh m_standaloneBlockMeshSwitchOff;
		private BlockMesh m_standaloneBlockMeshSwitchOn;
		private Texture2D m_texture;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/FlameThrower");

			// Cargar textura del modelo
			m_texture = ContentManager.Get<Texture2D>("Textures/FlameThrower");

			// Obtener transformaciones de los huesos
			Matrix bodyTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix switchTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Switch", true).ParentBone);

			// Crear BlockMesh para switch apagado (OFF)
			m_standaloneBlockMeshSwitchOff = new BlockMesh();
			m_standaloneBlockMeshSwitchOff.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], bodyTransform, false, false, false, false, Color.White);
			m_standaloneBlockMeshSwitchOff.AppendModelMeshPart(model.FindMesh("Switch", true).MeshParts[0], switchTransform, false, false, false, false, Color.White);

			// Crear BlockMesh para switch encendido (ON) - rotado en Z (como el martillo del mosquete se rota en X)
			m_standaloneBlockMeshSwitchOn = new BlockMesh();
			m_standaloneBlockMeshSwitchOn.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], bodyTransform, false, false, false, false, Color.White);
			m_standaloneBlockMeshSwitchOn.AppendModelMeshPart(model.FindMesh("Switch", true).MeshParts[0], Matrix.CreateRotationZ(1f) * switchTransform, false, false, false, false, Color.White);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar la misma lógica que el mosquete: dibujar según estado del switch
			if (GetSwitchState(Terrain.ExtractData(value)))
			{
				DrawMeshBlockWithTexture(primitivesRenderer, m_standaloneBlockMeshSwitchOn, m_texture, color, 2f * size, ref matrix, environmentData);
			}
			else
			{
				DrawMeshBlockWithTexture(primitivesRenderer, m_standaloneBlockMeshSwitchOff, m_texture, color, 2f * size, ref matrix, environmentData);
			}
		}

		private void DrawMeshBlockWithTexture(PrimitivesRenderer3D primitivesRenderer, BlockMesh blockMesh, Texture2D texture, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			environmentData = environmentData ?? new DrawBlockEnvironmentData();

			float lightIntensity = LightingManager.LightIntensityByLightValue[environmentData.Light];
			Color lightColor = Color.MultiplyColorOnly(color, lightIntensity);

			TexturedBatch3D batch = primitivesRenderer.TexturedBatch(texture, true, 0, null, RasterizerState.CullCounterClockwiseScissor, null, SamplerState.PointClamp);

			Matrix transformMatrix = (environmentData.ViewProjectionMatrix == null) ? matrix : (matrix * environmentData.ViewProjectionMatrix.Value);
			if (size != 1f)
			{
				transformMatrix = Matrix.CreateScale(size) * transformMatrix;
			}

			bool needsPerspective = (transformMatrix.M14 != 0f || transformMatrix.M24 != 0f || transformMatrix.M34 != 0f || transformMatrix.M44 != 1f);

			int vertexCount = blockMesh.Vertices.Count;
			int indexCount = blockMesh.Indices.Count;
			BlockMeshVertex[] vertices = blockMesh.Vertices.Array;
			int[] indices = blockMesh.Indices.Array;

			int startVertex = batch.TriangleVertices.Count;
			int startIndex = batch.TriangleIndices.Count;

			batch.TriangleVertices.Count += vertexCount;
			batch.TriangleIndices.Count += indexCount;

			for (int i = 0; i < vertexCount; i++)
			{
				BlockMeshVertex vertex = vertices[i];
				Vector3 position = vertex.Position;

				if (needsPerspective)
				{
					Vector4 pos4 = new Vector4(position, 1f);
					Vector4.Transform(ref pos4, ref transformMatrix, out pos4);
					float invW = 1f / pos4.W;
					position = new Vector3(pos4.X * invW, pos4.Y * invW, pos4.Z * invW);
				}
				else
				{
					Vector3.Transform(ref position, ref transformMatrix, out position);
				}

				Color vertexColor = vertex.IsEmissive ? vertex.Color * color : vertex.Color * lightColor;

				batch.TriangleVertices.Array[startVertex + i] = new VertexPositionColorTexture(
					position,
					vertexColor,
					vertex.TextureCoordinates
				);
			}

			for (int i = 0; i < indexCount; i++)
			{
				batch.TriangleIndices.Array[startIndex + i] = startVertex + indices[i];
			}
		}

		public override int GetDamage(int value) => (Terrain.ExtractData(value) >> 8) & 255;

		public override int SetDamage(int value, int damage)
		{
			int data = Terrain.ExtractData(value);
			data = (data & -65281) | (Math.Clamp(damage, 0, 255) << 8);
			return Terrain.ReplaceData(value, data);
		}

		// AÑADIDO: Método para evitar animación de cambio de slot cuando solo cambia el switch
		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			// Si el contenido es diferente (otro bloque), se necesita animación
			if (Terrain.ExtractContents(oldValue) != Index)
				return true;

			int oldData = Terrain.ExtractData(oldValue);
			int newData = Terrain.ExtractData(newValue);

			// Solo se necesita animación si el estado del switch cambia
			return GetSwitchState(oldData) != GetSwitchState(newData);
		}

		// Estado del switch (como el hammer del mosquete)
		public static bool GetSwitchState(int data) => (data & 4) != 0;

		public static int SetSwitchState(int data, bool state)
		{
			if (state)
				return data | 4;
			else
				return data & -5;
		}

		public static LoadState GetLoadState(int data) => (LoadState)(data & 3);
		public static int SetLoadState(int data, LoadState state) => (data & -4) | (int)(state);

		public static BulletBlock.BulletType? GetBulletType(int data)
		{
			int val = (data >> 4) & 15;
			return val == 0 ? null : (BulletBlock.BulletType?)(val - 1);
		}

		public static int SetBulletType(int data, BulletBlock.BulletType? type)
		{
			int val = type.HasValue ? (int)(type.Value + 1) : 0;
			return (data & -241) | ((val & 15) << 4);
		}

		public enum LoadState
		{
			Empty,
			Loaded
		}
	}
}