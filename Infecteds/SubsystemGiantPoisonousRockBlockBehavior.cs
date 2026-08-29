using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGiantPoisonousRockBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemProjectiles m_subsystemProjectiles;

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BlocksManager.GetBlockIndex("GiantPoisonousRockBlock", true) };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Color RGB de tu bloque (0, 204, 0) usando nuestro Particle System de vómito
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new PoisonTrailParticleSystem(20, 4.5f, float.MaxValue, new Color(0, 204, 0, 255)));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Si impacta a una criatura o jugador
			if (componentBody != null)
			{
				// 1. Causa tu veneno personalizado (ComponentInfectedWithPoison)
				ComponentInfectedWithPoison componentPoison = componentBody.Entity.FindComponent<ComponentInfectedWithPoison>();
				if (componentPoison != null)
				{
					componentPoison.TryInfect(1f, "GiantPoisonousRock");
				}

				// 2. Causa la náusea/estado enfermo vanilla (ComponentSickness)
				ComponentSickness componentSickness = componentBody.Entity.FindComponent<ComponentSickness>();
				if (componentSickness != null)
				{
					componentSickness.StartSickness();
				}
			}

			return false;
		}
	}
}