using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonFlameThrowerParticleSystem : ParticleSystem<PoisonFlameThrowerParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }

		public Vector3 Direction { get; set; }

		public bool IsStopped { get; set; }

		private Random m_random = new Random();

		private float m_size;

		private float m_maxVisibilityDistance;

		private float m_toGenerate;

		private float m_age;

		private bool m_visible;

		private Vector3? m_lastPosition;

		public PoisonFlameThrowerParticleSystem(Vector3 position, Vector3 direction, float size, float maxVisibilityDistance) : base(200)
		{
			Position = position;
			Direction = direction;
			m_size = size;
			m_maxVisibilityDistance = maxVisibilityDistance;
			Texture = ContentManager.Get<Texture2D>("Textures/PukeParticle");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			m_age += dt;
			float generationRate = 180f;
			m_toGenerate += (IsStopped ? 0f : (generationRate * dt));
			if (m_lastPosition == null)
			{
				m_lastPosition = new Vector3?(Position);
			}
			bool flag = false;
			Vector3 dir = Vector3.Normalize(Direction);
			Vector3 right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
			if (right.LengthSquared() < 0.001f)
			{
				right = Vector3.UnitX;
			}
			Vector3 up = Vector3.Normalize(Vector3.Cross(dir, right));
			float drag = MathF.Pow(0.08f, dt);
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
						particle.Position += particle.Velocity * dt;
						particle.Velocity *= drag;
						particle.Position.Y += particle.Buoyancy * dt;
						float ageRatio = particle.Time / particle.Duration;
						particle.TextureSlot = (int)MathUtils.Min(9f * ageRatio, 8f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					particle.IsActive = true;
					float lerpT = m_random.Float(0f, 1f);
					particle.Position = Vector3.Lerp(m_lastPosition.Value, Position, lerpT);
					float spreadAngle = 0.1f + 0.05f * MathUtils.Saturate(m_age * 0.5f);
					Vector3 spread = m_random.Float(-spreadAngle, spreadAngle) * right + m_random.Float(-spreadAngle, spreadAngle) * up;
					Vector3 particleDir = Vector3.Normalize(dir + spread);
					float speed = m_random.Float(14f, 100f);
					particle.Velocity = speed * particleDir;
					float sizeVar = m_random.Float(2.3f, 1.3f);
					particle.Size = new Vector2(m_size * sizeVar);
					particle.Time = 0f;
					particle.Duration = m_random.Float(0.2f, 0.5f);
					particle.TimeToLive = particle.Duration;
					particle.Buoyancy = m_random.Float(0.2f, 1.0f);
					particle.Color = new Color(50, 200, 50);
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					m_toGenerate -= 1f;
				}
			}
			m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);
			m_lastPosition = new Vector3?(Position);
			return IsStopped && !flag;
		}

		public override void Draw(Camera camera)
		{
			float num = Vector3.Dot(Position - camera.ViewPosition, camera.ViewDirection);
			if (num > -0.5f && num <= m_maxVisibilityDistance && Vector3.DistanceSquared(Position, camera.ViewPosition) <= m_maxVisibilityDistance * m_maxVisibilityDistance)
			{
				m_visible = true;
				base.Draw(camera);
			}
			m_visible = false;
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;

			public float Time;

			public float Duration;

			public float TimeToLive;

			public float Buoyancy;
		}
	}
}
