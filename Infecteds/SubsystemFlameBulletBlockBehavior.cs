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
			// Si impacta un cuerpo, prenderlo fuego
			if (componentBody != null)
			{
				ComponentOnFire onFire = componentBody.Entity.FindComponent<ComponentOnFire>();
				if (onFire != null)
				{
					// Intentar obtener el atacante (dueño del proyectil)
					ComponentCreature attacker = null;
					if (worldItem is Projectile projectile && projectile.Owner != null)
					{
						attacker = projectile.Owner;
					}

					// Prender fuego por 6-10 segundos
					onFire.SetOnFire(attacker, m_random.Float(6f, 10f));
				}
				return true;
			}

			// Si impacta un bloque, prender fuego al terreno
			if (cellFace != null)
			{
				int x = cellFace.Value.X;
				int y = cellFace.Value.Y;
				int z = cellFace.Value.Z;

				// Prender fuego al bloque impactado
				m_subsystemFireBlockBehavior.SetCellOnFire(x, y, z, 1f);

				// Prender fuego a bloques adyacentes (efecto de propagación)
				for (int i = 0; i < 6; i++)
				{
					int nx = x + m_offsets[i].X;
					int ny = y + m_offsets[i].Y;
					int nz = z + m_offsets[i].Z;

					// Verificar que el bloque sea inflamable
					int cellValue = m_subsystemTerrain.Terrain.GetCellValue(nx, ny, nz);
					int contents = Terrain.ExtractContents(cellValue);
					Block block = BlocksManager.Blocks[contents];

					// Si el bloque es inflamable (tiene duración de fuego > 0), prenderlo fuego
					if (block.GetFireDuration(cellValue) > 0f)
					{
						m_subsystemFireBlockBehavior.SetCellOnFire(nx, ny, nz, 1f);
					}
				}

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

		// Direcciones para propagar fuego
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