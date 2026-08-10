using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFrozenSnowballBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { FrozenSnowballBlock.Index };
			}
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new FrozenSmokeTrailParticleSystem(20, 1f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			this.m_subsystemAudio.PlaySound("Audio/congelado", 1f, 0f, worldItem.Position, 3f, false);
			if (componentBody != null && componentBody.Entity != null)
			{
				ComponentPlayer componentPlayer = componentBody.Entity.FindComponent<ComponentPlayer>();
				if (componentPlayer != null && componentPlayer.ComponentVitalStats != null)
				{
					componentPlayer.ComponentVitalStats.Temperature = Math.Max(0f, componentPlayer.ComponentVitalStats.Temperature - 4f);
					componentPlayer.ComponentVitalStats.Wetness = Math.Min(1f, componentPlayer.ComponentVitalStats.Wetness + 0.3f);
					if (componentPlayer.ComponentFlu != null)
					{
						componentPlayer.ComponentFlu.StartFlu();
					}
				}
				else
				{
					ComponentCreatureFlu componentCreatureFlu = componentBody.Entity.FindComponent<ComponentCreatureFlu>();
					if (componentCreatureFlu != null)
					{
						componentCreatureFlu.ForceInfect(600f);
					}
					else
					{
						ComponentFlu componentFlu = componentBody.Entity.FindComponent<ComponentFlu>();
						if (componentFlu != null)
						{
							componentFlu.StartFlu();
						}
					}
				}
			}
			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
		}

		public SubsystemProjectiles m_subsystemProjectiles;

		public SubsystemAudio m_subsystemAudio;
	}
}