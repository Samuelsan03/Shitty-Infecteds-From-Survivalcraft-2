using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonFlameThrowerParticleSystem : ParticleSystem<PoisonFlameThrowerParticleSystem.Particle>
	{
		public bool IsStopped { get; set; }
		public Vector3 Position { get => m_position; set => m_position = value; }
		public Vector3 Direction { get => m_direction; set => m_direction = Vector3.Normalize(value); }

		private Vector3 m_position;
		private Vector3 m_direction = Vector3.UnitY;
		private float m_size;
		private float m_toGenerate;
		private bool m_visible;
		private float m_maxVisibilityDistance;
		private float m_age;
		private Random m_random = new Random();

		public PoisonFlameThrowerParticleSystem(Vector3 position, Vector3 direction, float size, float maxVisibilityDistance)
			: base(20)
		{
			m_position = position;
			m_direction = Vector3.Normalize(direction);
			m_size = size;
			m_maxVisibilityDistance = maxVisibilityDistance;
			Texture = ContentManager.Get<Texture2D>("Textures/PukeParticle");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			m_age += dt;
			bool flag = false;

			if (m_visible || m_age < 2f)
			{
				m_toGenerate += (IsStopped ? 0f : (20f * dt));

				for (int i = 0; i < Particles.Length; i++)
				{
					Particle particle = Particles[i];

					if (particle.IsActive)
					{
						flag = true;
						particle.Time += dt;
						particle.TimeToLive -= dt;

						if (particle.TimeToLive > 0f)
						{
							particle.Position += m_direction * particle.Speed * dt;
							particle.Position += 0.15f * m_size * new Vector3(
								m_random.Float(-0.5f, 0.5f),
								m_random.Float(-0.5f, 0.5f),
								m_random.Float(-0.5f, 0.5f)
							) * dt;
							particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / 1.25f, 8f);
						}
						else
						{
							particle.IsActive = false;
						}
					}
					else if (m_toGenerate >= 1f)
					{
						particle.IsActive = true;
						particle.Position = m_position + 0.1f * m_size * new Vector3(
							m_random.Float(-1f, 1f),
							m_random.Float(-1f, 1f),
							m_random.Float(-1f, 1f)
						);
						particle.Color = new Color (0,255,0);
						particle.Size = new Vector2(m_size * m_random.Float(1.8f, 1.8f));
						particle.Speed = m_random.Float(15f, 35f);
						particle.Time = 0f;
						particle.TimeToLive = m_random.Float(0.8f, 1.8f);
						particle.FlipX = (m_random.Int(0, 1) == 0);
						particle.FlipY = (m_random.Int(0, 1) == 0);
						m_toGenerate -= 1f;
					}
				}

				m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);
			}

			m_visible = false;
			return IsStopped && !flag;
		}

		public override void Draw(Camera camera)
		{
			float num = Vector3.Dot(m_position - camera.ViewPosition, camera.ViewDirection);
			if (num > -0.5f && num <= m_maxVisibilityDistance &&
				Vector3.DistanceSquared(m_position, camera.ViewPosition) <= m_maxVisibilityDistance * m_maxVisibilityDistance)
			{
				m_visible = true;
				base.Draw(camera);
			}
		}

		public class Particle : Game.Particle
		{
			public float Time;
			public float TimeToLive;
			public float Speed;
		}
	}
}
