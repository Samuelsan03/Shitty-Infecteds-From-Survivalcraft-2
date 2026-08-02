using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class DeathSpawnParticleSystem : ParticleSystem<DeathSpawnParticleSystem.Particle>
	{
		// Token: 0x170002F9 RID: 761
		public bool Stopped { get; set; }

		// Token: 0x170002FA RID: 762
		public Vector3 Position { get; set; }

		// Token: 0x170002FB RID: 763
		public BoundingBox BoundingBox { get; set; }

		// Token: 0x0600137D RID: 4989
		public DeathSpawnParticleSystem() : base(40)
		{
			base.Texture = ContentManager.Get<Texture2D>("Textures/ShapeshiftParticle");
			base.TextureSlotsCount = 3;
		}

		// Token: 0x0600137E RID: 4990
		public override bool Simulate(float dt)
		{
			bool flag = false;
			this.m_generationSpeed = MathUtils.Min(this.m_generationSpeed + 15f * dt, 35f);
			this.m_toGenerate += this.m_generationSpeed * dt;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				DeathSpawnParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.Time += dt;
					if (particle.Time <= particle.Duration)
					{
						particle.Position += particle.Velocity * dt;
						particle.FlipX = this.m_random.Bool();
						particle.FlipY = this.m_random.Bool();
						particle.TextureSlot = (int)MathUtils.Min(9.900001f * particle.Time / particle.Duration, 8f);
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!this.Stopped)
				{
					while (this.m_toGenerate >= 1f)
					{
						particle.IsActive = true;
						particle.Position.X = this.m_random.Float(this.BoundingBox.Min.X, this.BoundingBox.Max.X);
						particle.Position.Y = this.m_random.Float(this.BoundingBox.Min.Y, this.BoundingBox.Max.Y);
						particle.Position.Z = this.m_random.Float(this.BoundingBox.Min.Z, this.BoundingBox.Max.Z);
						particle.Velocity = new Vector3(0f, this.m_random.Float(0.5f, 1.5f), 0f);
						particle.Color = Color.Green; // Partículas verdes
						particle.Size = new Vector2(0.4f);
						particle.Time = 0f;
						particle.Duration = this.m_random.Float(0.75f, 1.5f);
						this.m_toGenerate -= 1f;
					}
				}
				else
				{
					this.m_toGenerate = 0f;
				}
			}
			this.m_toGenerate = MathUtils.Remainder(this.m_toGenerate, 1f);
			return this.Stopped && !flag;
		}

		// Token: 0x04000D96 RID: 3478
		public Random m_random = new Random();

		// Token: 0x04000D97 RID: 3479
		public float m_generationSpeed;

		// Token: 0x04000D98 RID: 3480
		public float m_toGenerate;

		// Token: 0x020005A0 RID: 1440
		public class Particle : Game.Particle
		{
			// Token: 0x04002004 RID: 8196
			public float Time;

			// Token: 0x04002005 RID: 8197
			public float Duration;

			// Token: 0x04002006 RID: 8198
			public Vector3 Velocity;
		}
	}
}
