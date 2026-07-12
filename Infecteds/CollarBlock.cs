using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CollarBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Collar");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Collar", true).ParentBone);

			// Se ha cambiado Color.White por Color.Green en ambas líneas
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Collar", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, false, false, false, Color.Green);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Collar", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, true, false, false, Color.Green);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		public const int Index = 501;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
