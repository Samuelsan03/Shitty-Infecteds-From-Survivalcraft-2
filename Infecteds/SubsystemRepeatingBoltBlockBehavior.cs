using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatingBoltBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { RepeatingBoltBlock.Index };

		private SubsystemProjectiles m_subsystemProjectiles;
		private Random m_random = new Random();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			RepeatingBoltBlock.RepeatingBoltType boltType = RepeatingBoltBlock.GetBoltType(Terrain.ExtractData(projectile.Value));
			if (boltType == RepeatingBoltBlock.RepeatingBoltType.RepeatingExplosiveBolt)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
					new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatingBoltBlock.RepeatingBoltType boltType = RepeatingBoltBlock.GetBoltType(Terrain.ExtractData(worldItem.Value));
			if (worldItem.Velocity.Length() > 10f)
			{
				float breakChance = boltType switch
				{
					RepeatingBoltBlock.RepeatingBoltType.RepeatingCopperBolt => 0.1f,     // NUEVO: más frágil que hierro
					RepeatingBoltBlock.RepeatingBoltType.RepeatingIronBolt => 0.05f,
					RepeatingBoltBlock.RepeatingBoltType.RepeatingDiamondBolt => 0f,
					RepeatingBoltBlock.RepeatingBoltType.RepeatingExplosiveBolt => 0f,
					_ => 0f
				};
				if (m_random.Float(0f, 1f) < breakChance)
					return true; // se rompe
			}
			return false;
		}
	}
}