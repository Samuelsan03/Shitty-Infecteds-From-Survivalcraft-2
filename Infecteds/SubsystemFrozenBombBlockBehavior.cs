using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFrozenBombBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks => Array.Empty<int>();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemFrozenExplosions = base.Project.FindSubsystem<SubsystemFrozenExplosions>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemBlockBehaviors = base.Project.FindSubsystem<SubsystemBlockBehaviors>(true);

			foreach (Projectile projectile in this.m_subsystemProjectiles.Projectiles)
			{
				this.ScanProjectile(projectile);
			}

			this.m_subsystemProjectiles.ProjectileAdded += delegate (Projectile projectile)
			{
				this.ScanProjectile(projectile);
			};
			this.m_subsystemProjectiles.ProjectileRemoved += delegate (Projectile projectile)
			{
				this.m_projectiles.Remove(projectile);
			};
		}

		public void ScanProjectile(Projectile projectile)
		{
			if (!this.m_projectiles.ContainsKey(projectile))
			{
				if (this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(projectile.Value)).Contains(this, null))
				{
					this.m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;
					this.m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.25f, 0.1f), new SmokeTrailParticleSystem(20, 0.33f, float.MaxValue, new Color(0.5f, 0.8f, 1f)));
				}
			}
		}

		public virtual void Update(float dt)
		{
			if (this.m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				foreach (Projectile projectile in this.m_projectiles.Keys)
				{
					if (this.m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						this.m_subsystemFrozenExplosions.TryExplodeBlock(Terrain.ToCell(projectile.Position.X), Terrain.ToCell(projectile.Position.Y), Terrain.ToCell(projectile.Position.Z), projectile.Value);
						projectile.ToRemove = true;
					}
				}
			}
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemFrozenExplosions m_subsystemFrozenExplosions;
		private SubsystemProjectiles m_subsystemProjectiles;
		private Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();
	}
}