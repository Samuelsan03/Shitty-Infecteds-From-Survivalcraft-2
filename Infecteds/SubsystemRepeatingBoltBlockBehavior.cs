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
			// Si es virote explosivo, añadir estela de humo (opcional)
			if (ArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) == ArrowBlock.ArrowType.ExplosiveBolt)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
					new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			var type = ArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));
			if (worldItem.Velocity.Length() > 10f)
			{
				float breakChance = 0f;
				switch (type)
				{
					case ArrowBlock.ArrowType.IronBolt: breakChance = 0.05f; break;
					case ArrowBlock.ArrowType.DiamondBolt: breakChance = 0f; break;
					case ArrowBlock.ArrowType.ExplosiveBolt: breakChance = 0f; break;
				}
				if (m_random.Float(0f, 1f) < breakChance)
					return true; // se rompe
			}
			return false;
		}
	}
}