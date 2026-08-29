using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGiantFrozenRockBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemProjectiles m_subsystemProjectiles;

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BlocksManager.GetBlockIndex("GiantFrozenRockBlock", true) };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Color RGB de tu bloque (0, 162, 255) usando el SmokeTrail original con tamaño 4.5f
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new FrozenSmokeTrailParticleSystem(20, 4.5f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Si impacta a una criatura o jugador
			if (componentBody != null)
			{
				// 1. Causa la gripa a criaturas (ComponentCreatureFlu)
				ComponentCreatureFlu componentCreatureFlu = componentBody.Entity.FindComponent<ComponentCreatureFlu>();
				if (componentCreatureFlu != null)
				{
					componentCreatureFlu.TryInfect(1f);
				}

				// 2. Causa la gripa vanilla al jugador (ComponentFlu)
				ComponentFlu componentFlu = componentBody.Entity.FindComponent<ComponentFlu>();
				if (componentFlu != null)
				{
					componentFlu.StartFlu();
				}
			}

			return false;
		}
	}
}