using System;
using Engine;
using TemplatesDatabase;
using static Game.RepeatBoltBlock;

namespace Game
{
	public class SubsystemRepeatBoltBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemProjectiles m_subsystemProjectiles;
		public Random m_random = new Random();

		public override int[] HandledBlocks
		{
			get { return new int[] { RepeatBoltBlock.Index }; }
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			RepeatBoltType type = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(projectile.Value));

			// Virote de diamante: no se frena en el agua
			if (type == RepeatBoltType.RepeatDiamondBolt)
			{
				projectile.DampingInFluid = 1f;
			}

			if (type == RepeatBoltType.RepeatFireBolt)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				projectile.IsIncendiary = true;
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatBoltType boltType = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(worldItem.Value));

			bool destroy = false;
			float velocity = worldItem.Velocity.Length();

			if (velocity > 10f)
			{
				float chance = 0f;
				switch (boltType)
				{
					case RepeatBoltType.RepeatCopperBolt: chance = 0.1f; break;
					case RepeatBoltType.RepeatIronBolt: chance = 0.05f; break;
					case RepeatBoltType.RepeatDiamondBolt: chance = 0f; break;
					case RepeatBoltType.RepeatExplosiveBolt: chance = 0.05f; break;
					case RepeatBoltType.RepeatFireBolt: chance = 0.5f; break;
					case RepeatBoltType.RepeatPoisonBolt: chance = 0.8f; break;
					case RepeatBoltType.RepeatSeverelyPoisonousBolt: chance = 1f; break;
				}

				if (m_random.Float(0f, 1f) < chance)
					destroy = true;
			}

			if (componentBody != null && componentBody.Entity != null)
			{
				ApplyPoisonToTarget(componentBody, boltType);
			}

			return destroy;
		}

		private void ApplyPoisonToTarget(ComponentBody targetBody, RepeatBoltType boltType)
		{
			if (targetBody == null || targetBody.Entity == null)
				return;

			float intensity = 0f;
			switch (boltType)
			{
				case RepeatBoltType.RepeatPoisonBolt:
					intensity = 0.3f;
					break;
				case RepeatBoltType.RepeatSeverelyPoisonousBolt:
					intensity = 0.7f;
					break;
				default:
					return;
			}

			ComponentPlayer player = targetBody.Entity.FindComponent<ComponentPlayer>();
			if (player != null && player.ComponentSickness != null)
			{
				player.ComponentSickness.StartSickness();
				return;
			}

			ComponentInfectedWithPoison infection = targetBody.Entity.FindComponent<ComponentInfectedWithPoison>();
			if (infection != null)
			{
				infection.TryInfect(intensity);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
		}
	}
}
