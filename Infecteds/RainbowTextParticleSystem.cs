using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RainbowTextParticleSystem : ParticleSystem<RainbowTextParticleSystem.Particle>
	{
		// Tiempo propio acumulado en Simulate (SIEMPRE en segundos, sin depender
		// de la unidad de Time.FrameStartTime). Esta es la clave para que el
		// arcoíris se anime correctamente.
		private float m_time;

		public RainbowTextParticleSystem(Vector3 position, Vector3 velocity, string text) : base(1)
		{
			Random random = new Random();
			RainbowTextParticleSystem.Particle particle = base.Particles[0];
			particle.IsActive = true;
			particle.Position = position;
			particle.TimeToLive = 1.5f;
			particle.Velocity = velocity + random.Vector3(0.75f) * new Vector3(1f, 0f, 1f) + 0.5f * Vector3.UnitY;
			particle.Text = text;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);

			// Acumular tiempo propio en segundos. Independiente de la unidad
			// de Time.FrameStartTime, garantiza animación fluida.
			m_time += dt;

			float s = MathF.Pow(0.1f, dt);
			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				RainbowTextParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						particle.Velocity += new Vector3(0f, 0.5f, 0f) * dt;
						particle.Velocity *= s;
						particle.Position += particle.Velocity * dt;
					}
					else
					{
						particle.IsActive = false;
					}
				}
			}
			return !flag;
		}

		public override void Draw(Camera camera)
		{
			if (this.m_batch == null)
			{
				this.m_batch = this.SubsystemParticles.PrimitivesRenderer.FontBatch(
					LabelWidget.BitmapFont, 0, DepthStencilState.None, null, null, null);
			}

			Vector3 viewDirection = camera.ViewDirection;
			Vector3 vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
			Vector3 v = -Vector3.Normalize(Vector3.Cross(vector, viewDirection));

			for (int i = 0; i < base.Particles.Length; i++)
			{
				RainbowTextParticleSystem.Particle particle = base.Particles[i];
				if (!particle.IsActive) continue;

				float num = Vector3.Distance(camera.ViewPosition, particle.Position);

				// Fade-in cerca de la cámara (sin cambios) y fade-out aumentado
				// de 20 → 60 bloques para que se vea a media distancia del arma.
				float num2 = MathUtils.Saturate(3f * (num - 0.2f));
				float num3 = MathUtils.Saturate(0.05f * (60f - num));
				float num4 = num2 * num3;
				if (num4 <= 0f) continue;

				float size = 0.006f * MathF.Sqrt(num);
				float alpha = MathUtils.Saturate(2f * particle.TimeToLive) * num4;

				// ===== Arcoíris con HSV =====
				// 180 grados/segundo => ciclo completo cada 2 s.
				// En los 1.5 s de vida recorre ~3/4 del arcoíris: muy visible.
				float hue = (m_time * 180f) % 360f;
				if (hue < 0f) hue += 360f;

				// Saturación=1, Valor=1 => colores puros y brillantes.
				Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));
				Color rainbowColor = new Color(rgb.X, rgb.Y, rgb.Z, 1f);

				// Multiplicar por alpha para fundido al final de la vida.
				Color color = rainbowColor * alpha;

				this.m_batch.QueueText(
					particle.Text,
					particle.Position,
					vector * size,
					v * size,
					color,
					TextAnchor.Center,
					Vector2.Zero
				);
			}
		}

		public FontBatch3D m_batch;

		public class Particle : Game.Particle
		{
			public float TimeToLive;
			public Vector3 Velocity;
			public string Text;
		}
	}
}
