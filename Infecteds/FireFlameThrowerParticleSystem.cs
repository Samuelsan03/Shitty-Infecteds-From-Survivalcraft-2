using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FireFlameThrowerParticleSystem : ParticleSystem<FireFlameThrowerParticleSystem.Particle>
	{
		public bool IsStopped { get; set; }

		public Vector3 Position
		{
			get => m_position;
			set => m_position = value;
		}

		public Vector3 Direction
		{
			get => m_direction;
			set => m_direction = Vector3.Normalize(value);
		}

		private Vector3 m_position;
		private Vector3 m_direction = Vector3.UnitY;
		private float m_size;
		private float m_toGenerate;
		private bool m_visible;
		private float m_maxVisibilityDistance;
		private float m_age;
		private Random m_random = new Random();

		public FireFlameThrowerParticleSystem(Vector3 position, Vector3 direction, float size, float maxVisibilityDistance)
			: base(150) // AUMENTADO: Más partículas para un chorro denso y continuo
		{
			m_position = position;
			m_direction = Vector3.Normalize(direction);
			m_size = size;
			m_maxVisibilityDistance = maxVisibilityDistance;
			Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			m_age += dt;
			bool flag = false;

			if (m_visible || m_age < 2f)
			{
				// AUMENTADO: Generamos muchas más partículas por segundo
				m_toGenerate += (IsStopped ? 0f : (80f * dt));

				// REDUCIDO: Fricción mucho menor para que el fuego viaje lejos y no se pare en seco
				float s = MathF.Pow(0.15f, dt);

				// CORREGIDO: Sin fuerza loca hacia arriba. Solo un leve flotamiento al final
				Vector3 v = new Vector3(0f, 2f, 0f);

				for (int i = 0; i < Particles.Length; i++)
				{
					Particle particle = Particles[i];

					if (particle.IsActive)
					{
						flag = true;
						particle.Time += dt;

						if (particle.Time <= particle.Duration)
						{
							particle.Position += particle.Velocity * dt;
							particle.Velocity *= s;
							particle.Velocity += v * dt;
							particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);
							// El tamaño ya no se actualiza aquí para evitar parpadeos
						}
						else
						{
							particle.IsActive = false;
						}
					}
					else if (m_toGenerate >= 1f)
					{
						particle.IsActive = true;

						Vector3 v2 = new Vector3(m_random.Float(-1f, 1f), m_random.Float(-1f, 1f), m_random.Float(-1f, 1f));
						particle.Position = m_position + 0.3f * v2 * m_size;

						particle.Color = new Color(255, 128, 0);

						// AUMENTADO: Velocidad de 100 a 150 para que acompañe a la bala (que va a 120f)
						particle.Velocity = m_random.Float(100f, 150f) * (m_direction + 0.15f * v2);

						particle.Time = 0f;
						particle.Duration = m_random.Float(0.2f, 0.6f); // Vida corta para que se renueve rápido en el chorro

						// AUMENTADO: Tamaño grande definido al nacer para no tapar como una mancha densa
						particle.Size = new Vector2(m_size * m_random.Float(2.5f, 4f));

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
			public Vector3 Velocity;
			public float Time;
			public float Duration;
		}
	}
}
