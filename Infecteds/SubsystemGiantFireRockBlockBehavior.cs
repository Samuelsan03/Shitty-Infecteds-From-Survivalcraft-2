using System;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGiantFireRockBlockBehavior : SubsystemBlockBehavior
	{
		private Random m_random = new Random();
		private SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		private SubsystemExplosions m_subsystemExplosions;
		private SubsystemProjectiles m_subsystemProjectiles;

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BlocksManager.GetBlockIndex("GiantFireRockBlock", true) };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemFireBlockBehavior = base.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
			m_subsystemExplosions = base.Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			// Lógica idéntica a la flecha de fuego, adaptada a tu bloque y color
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 4.5f, float.MaxValue, new Color(255, 128, 0, 255)));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			projectile.IsIncendiary = true;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// 1. Causar incendio al impacto
			if (componentBody != null)
			{
				ComponentOnFire componentOnFire = componentBody.Entity.FindComponent<ComponentOnFire>();
				if (componentOnFire != null)
				{
					componentOnFire.SetOnFire(null, m_random.Float(6f, 8f));
				}
			}
			else if (cellFace.HasValue)
			{
				m_subsystemFireBlockBehavior.SetCellOnFire(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z, 1f);
			}

			// 2. 1% de probabilidad de que explote incendiariamente al impactar
			if (m_random.Bool(0.01f))
			{
				int x = cellFace.HasValue ? cellFace.Value.X : Terrain.ToCell(worldItem.Position.X);
				int y = cellFace.HasValue ? cellFace.Value.Y : Terrain.ToCell(worldItem.Position.Y);
				int z = cellFace.HasValue ? cellFace.Value.Z : Terrain.ToCell(worldItem.Position.Z);

				Block giantFireRock = BlocksManager.GetBlock("GiantFireRockBlock");
				int blockValue = Terrain.MakeBlockValue(giantFireRock.BlockIndex);

				float pressure = giantFireRock.GetExplosionPressure(blockValue);
				if (pressure <= 0f)
				{
					pressure = 200f;
				}

				m_subsystemExplosions.AddExplosion(x, y, z, pressure, true, false);
			}

			return false;
		}
	}
}
