using System;
using Engine;
using TemplatesDatabase;

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
			// Sin gravedad = no cae
			projectile.Gravity = 0f;

			// Damping 1.0 = sin desaceleración (MathF.Pow(1.0, dt) = 1.0)
			projectile.Damping = 1f;

			// Sin rotación
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
