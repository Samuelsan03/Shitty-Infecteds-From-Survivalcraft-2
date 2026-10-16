using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Copia del RodBlock original con color cambiado (sin campo, color normal hardcodeado)
	public class CommandRodBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Rod");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("IronRod", true).ParentBone);
			m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("IronRod", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
				false, false, false, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Color comandante (rojo) - sin campo, normal, hardcodeado
			Color commandColor = new Color(220, 60, 60, 255);

			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, commandColor, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, defaultTexture, commandColor, 2f * size, ref matrix, environmentData);
		}

		public static int Index = 532;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
