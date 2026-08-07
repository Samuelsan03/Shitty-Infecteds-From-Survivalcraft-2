using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonBombBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks => new int[] { PoisonBombBlock.Index };
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemPoisonExplosions m_subsystemPoisonExplosions;

		private Dictionary<Projectile, bool> m_projectiles = new Dictionary<Projectile, bool>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			// Inicializar todos los subsistemas necesarios en un solo lugar
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemPoisonExplosions = Project.FindSubsystem<SubsystemPoisonExplosions>(true);

			foreach (Projectile projectile in m_subsystemProjectiles.Projectiles)
			{
				ScanProjectile(projectile);
			}

			m_subsystemProjectiles.ProjectileAdded += (Action<Projectile>)delegate (Projectile p) { ScanProjectile(p); };
			m_subsystemProjectiles.ProjectileRemoved += (Action<Projectile>)delegate (Projectile p) { m_projectiles.Remove(p); };
		}

		public void ScanProjectile(Projectile projectile)
		{
			if (!m_projectiles.ContainsKey(projectile))
			{
				int num = Terrain.ExtractContents(projectile.Value);
				if (num == PoisonBombBlock.Index)
				{
					m_projectiles.Add(projectile, true);
					projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;

					// Humo verde de estela
					m_subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.25f, 0.1f),
						new SmokeTrailParticleSystem(20, 0.33f, float.MaxValue, new Color(100, 200, 50)));
				}
			}
		}

		public void Update(float dt)
		{
			// Usamos el mismo sistema de eventos periódicos que la bomba original
			if (m_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
			{
				foreach (Projectile projectile in m_projectiles.Keys)
				{
					// MANEJO CORRECTO SIN "Stopped": Usar tiempo de vida exactamente como la bomba original
					if (m_subsystemGameInfo.TotalElapsedGameTime - projectile.CreationTime > 5.0)
					{
						int x = Terrain.ToCell(projectile.Position.X);
						int y = Terrain.ToCell(projectile.Position.Y);
						int z = Terrain.ToCell(projectile.Position.Z);

						// Llamar a nuestro SubsystemExplosions de Veneno
						// Presión de 15f (equivalente a una bomba estándar)
						m_subsystemPoisonExplosions.AddPoisonExplosion(x, y, z, 15f);

						projectile.ToRemove = true;
					}
				}
			}
		}
	}
}