using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntiTankPelletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { AntiTankPelletBlock.Index };

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Al retornar true, le decimos al motor que destruya el proyectil al impactar.
			// Esto evita completamente la lógica de rebote (ricochet) del SubsystemBulletBlockBehavior original.
			return true;
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Establecer el amortiguamiento en fluidos a 1.0f hace que MathF.Pow(1.0f, dt) sea siempre 1.
			// Es decir, la velocidad se multiplica por 1 y el agua no le afecta en absoluto.
			projectile.DampingInFluid = 1f;
		}
	}
}