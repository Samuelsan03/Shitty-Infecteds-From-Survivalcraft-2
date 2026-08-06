using System;
using Engine;
using TemplatesDatabase;
using static Game.FirearmsBulletBlock;

namespace Game
{
	public class SubsystemFirearmsBulletBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { FirearmsBulletBlock.Index };
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			bool result = true;

			if (cellFace != null)
			{
				m_subsystemAudio.PlayRandomSound(
					"Audio/Ricochets",
					1f,
					m_random.Float(-0.2f, 0.2f),
					new Vector3(cellFace.Value.X, cellFace.Value.Y, cellFace.Value.Z),
					8f,
					true
				);
			}

			return result;
		}

		// ===== ESTO HACE QUE LA BALA VAYA RECTA =====
		public override void OnFiredAsProjectile(Projectile projectile)
		{
			projectile.Gravity = 0f;

			// USAR el damping específico de cada tipo de bala, NO 1.0f
			FirearmsBulletType type = FirearmsBulletBlock.GetFirearmsBulletType(Terrain.ExtractData(projectile.Value));
			projectile.Damping = FirearmsBulletBlock.GetBulletDamping(type);

			projectile.AngularVelocity = Vector3.Zero;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemAudio m_subsystemAudio;
		public Random m_random = new Random();
	}
}
