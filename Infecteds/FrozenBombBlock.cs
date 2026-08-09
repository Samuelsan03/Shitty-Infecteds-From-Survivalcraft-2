using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FrozenBombBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bomb");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bomb", true).ParentBone);
			// Color azul/helado en lugar de gris
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Bomb", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false, new Color(0.3f, 0.5f, 0.85f));
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

		public static int Index = 519;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}