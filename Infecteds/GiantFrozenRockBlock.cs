using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000XX RID: XXX
	public class GiantFrozenRockBlock : ChunkBlock
	{
		// Token: 0x06000XXX RID: XXX RVA: 0x00000000 File Offset: 0x00000000
		public GiantFrozenRockBlock() : base(
			Matrix.CreateScale(4.5f) * Matrix.CreateRotationX(1f) * Matrix.CreateRotationZ(2f),
			Matrix.CreateTranslation(0.875f, 0.1875f, 0f),
			new Color(0, 162, 255, 255),
			false)
		{
		}

		// Token: 0x06000XXX RID: XXX RVA: 0x00000000 File Offset: 0x00000000
		public override Texture2D GetDefaultTexture(int value)
		{
			return ContentManager.Get<Texture2D>("Textures/roca textura");
		}

		// Token: 0x04000XXX RID: XXX
		public static int Index = 526;
	}
}