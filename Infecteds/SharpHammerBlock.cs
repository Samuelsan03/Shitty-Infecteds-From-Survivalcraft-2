using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000XX RID: XXXX
	public abstract class SharpHammerBlock : Block
	{
		// Token: 0x060004BE RID: XXXX RVA: 0x00000000 File Offset: 0x00000000
		public SharpHammerBlock(int handleTextureSlot, int headTextureSlot)
		{
			this.m_handleTextureSlot = handleTextureSlot;
			this.m_headTextureSlot = headTextureSlot;
		}

		// Token: 0x060004BF RID: XXXX RVA: 0x00000000 File Offset: 0x00000000
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/SharpHammer");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Handle", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Head", true).ParentBone);
			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(model.FindMesh("Handle", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((float)(this.m_handleTextureSlot % 16) / 16f, (float)(this.m_handleTextureSlot / 16) / 16f, 0f), -1);
			BlockMesh blockMesh2 = new BlockMesh();
			blockMesh2.AppendModelMeshPart(model.FindMesh("Head", true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation((float)(this.m_headTextureSlot % 16) / 16f, (float)(this.m_headTextureSlot / 16) / 16f, 0f), -1);
			this.m_standaloneBlockMesh.AppendBlockMesh(blockMesh);
			this.m_standaloneBlockMesh.AppendBlockMesh(blockMesh2);
			base.Initialize();
		}

		// Token: 0x060004C0 RID: XXXX RVA: 0x00000000 File Offset: 0x00000000
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x060004C1 RID: XXXX RVA: 0x00000000 File Offset: 0x00000000
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x04000207 RID: XXXX
		public int m_handleTextureSlot;

		// Token: 0x04000208 RID: XXXX
		public int m_headTextureSlot;

		// Token: 0x04000209 RID: XXXX
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
