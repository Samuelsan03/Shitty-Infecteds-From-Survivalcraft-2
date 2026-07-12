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

			// Efecto de fuego (igual que FireArrow)
			if (type == RepeatBoltType.RepeatFireBolt)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				projectile.IsIncendiary = true;
			}

			// (Opcional) Efecto de brillo para diamante, etc.
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatBoltType boltType = RepeatBoltBlock.GetRepeatBoltType(Terrain.ExtractData(worldItem.Value));

			if (worldItem.Velocity.Length() > 10f)
			{
				float chance = 0f;
				switch (boltType)
				{
					case RepeatBoltType.RepeatCopperBolt:
						chance = 0.1f;    // 10% (igual que CopperArrow)
						break;
					case RepeatBoltType.RepeatIronBolt:
						chance = 0.05f;   // 5% (igual que IronBolt)
						break;
					case RepeatBoltType.RepeatDiamondBolt:
						chance = 0f;      // 0% (como DiamondBolt)
						break;
					case RepeatBoltType.RepeatExplosiveBolt:
						chance = 0.05f;   // 5% (como ExplosiveBolt)
						break;
					case RepeatBoltType.RepeatFireBolt:
						chance = 0.5f;    // 50% (igual que FireArrow)
						break;
				}

				if (m_random.Float(0f, 1f) < chance)
				{
					return true; // El virote se destruye
				}
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
		}
	}
}
