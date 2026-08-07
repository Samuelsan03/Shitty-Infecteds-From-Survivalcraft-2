using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonBombBlock : Block
	{
		public static int Index = 518; // Cambia este índice por uno que no esté en uso en tu mod

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bomb");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bomb", true).ParentBone);

			// CAMBIO PRINCIPAL: Color verde venenoso en lugar de gris (0.3f, 0.3f, 0.3f)
			this.m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("Bomb", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f),
				false, false, false, false,
				new Color(0.1f, 0.7f, 0.1f) // Color verde tóxico
			);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, defaultTexture, color, 2f * size, ref matrix, environmentData);
		}
	}
}