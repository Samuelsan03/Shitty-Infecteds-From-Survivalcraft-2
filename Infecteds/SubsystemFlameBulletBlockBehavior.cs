using System;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFlameBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { FlameBulletBlock.Index };

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			int value = worldItem.Value;
			int data = Terrain.ExtractData(value);
			FlameBulletBlock.FlameBulletType type = FlameBulletBlock.GetBulletType(data);

			if (type == FlameBulletBlock.FlameBulletType.Fire)
			{
				// Comportamiento original: fuego
				if (componentBody != null)
				{
					ComponentOnFire onFire = componentBody.Entity.FindComponent<ComponentOnFire>();
					if (onFire != null)
					{
						ComponentCreature attacker = null;
						if (worldItem is Projectile projectile && projectile.Owner != null)
						{
							attacker = projectile.Owner;
						}
						onFire.SetOnFire(attacker, m_random.Float(6f, 10f));
					}
					return true;
				}

				if (cellFace != null)
				{
					int x = cellFace.Value.X;
					int y = cellFace.Value.Y;
					int z = cellFace.Value.Z;

					m_subsystemFireBlockBehavior.SetCellOnFire(x, y, z, 1f);

					for (int i = 0; i < 6; i++)
					{
						int nx = x + m_offsets[i].X;
						int ny = y + m_offsets[i].Y;
						int nz = z + m_offsets[i].Z;

						int cellValue = m_subsystemTerrain.Terrain.GetCellValue(nx, ny, nz);
						int contents = Terrain.ExtractContents(cellValue);
						Block block = BlocksManager.Blocks[contents];

						if (block.GetFireDuration(cellValue) > 0f)
						{
							m_subsystemFireBlockBehavior.SetCellOnFire(nx, ny, nz, 1f);
						}
					}
					return true;
				}
				return false;
			}
			else if (type == FlameBulletBlock.FlameBulletType.Poison)
			{
				// Comportamiento para veneno
				if (componentBody != null)
				{
					ComponentInfectedWithPoison infection = componentBody.Entity.FindComponent<ComponentInfectedWithPoison>();
					if (infection != null)
					{
						ComponentCreature attacker = null;
						if (worldItem is Projectile projectile && projectile.Owner != null)
						{
							attacker = projectile.Owner;
						}
						// Intensidad 1.0 para veneno fuerte
						infection.TryInfect(1.0f);
					}
					return true;
				}
				// No afecta a bloques
				return true;
			}

			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
			base.Load(valuesDictionary);
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		private Random m_random = new Random();

		private readonly Point3[] m_offsets = new Point3[]
		{
			new Point3(1, 0, 0),
			new Point3(-1, 0, 0),
			new Point3(0, 1, 0),
			new Point3(0, -1, 0),
			new Point3(0, 0, 1),
			new Point3(0, 0, -1)
		};
	}
}
