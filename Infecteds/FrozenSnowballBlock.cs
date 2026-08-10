using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FrozenSnowballBlock : Block
	{
		public static int Index = 519;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Snowball");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Snowball", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Snowball", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, new Color(140, 200, 255));
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
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2.5f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, defaultTexture, color, 2.5f * size, ref matrix, environmentData);
		}
	}
}