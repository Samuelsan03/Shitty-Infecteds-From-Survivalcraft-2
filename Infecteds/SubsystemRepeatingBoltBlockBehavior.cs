using System;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatingBoltBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemProjectiles m_subsystemProjectiles;
		public Engine.Random m_random = new Engine.Random();

		public override int[] HandledBlocks
		{
			get { return new int[] { RepeatingBoltBlock.Index }; }
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			if (RepeatingBoltBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) == 3)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			base.Load(valuesDictionary);
		}
	}
}