using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class BloodParticleSystem : ParticleSystem<BloodParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }

		public Vector3 Direction { get; set; }

		public bool IsStopped { get; set; }

		public Random m_random = new Random();

		public SubsystemTerrain m_subsystemTerrain;

		public float m_duration;

		public float m_toGenerate;

		public BloodParticleSystem(SubsystemTerrain terrain) : base(80)
		{
			this.m_subsystemTerrain = terrain;
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/blood particle");
			base.TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			int num = Terrain.ToCell(this.Position.X);
			int num2 = Terrain.ToCell(this.Position.Y);
			int num3 = Terrain.ToCell(this.Position.Z);
			int num4 = 0;
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num + 1, num2, num3));
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num - 1, num2, num3));
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num, num2 + 1, num3));
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num, num2 - 1, num3));
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num, num2, num3 + 1));
			num4 = MathUtils.Max(num4, this.m_subsystemTerrain.Terrain.GetCellLight(num, num2, num3 - 1));
			Color c = Color.White;
			float s = LightingManager.LightIntensityByLightValue[num4];
			c *= s;
			c.A = byte.MaxValue;
			dt = Math.Clamp(dt, 0f, 0.1f);
			float s2 = MathF.Pow(0.03f, dt);
			this.m_duration += dt;

			float num5 = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * this.m_duration + (float)(this.GetHashCode() % 100)) - 0.3f);
			float num6 = 20f * num5 + 15f;
			this.m_toGenerate += num6 * dt;

			bool flag = false;
			for (int i = 0; i < base.Particles.Length; i++)
			{
				BloodParticleSystem.Particle particle = base.Particles[i];
				if (particle.IsActive)
				{
					flag = true;
					particle.TimeToLive -= dt;
					if (particle.TimeToLive > 0f)
					{
						Vector3 position = particle.Position;
						Vector3 vector = position + particle.Velocity * dt;
						TerrainRaycastResult? terrainRaycastResult = this.m_subsystemTerrain.Raycast(position, vector, false, true, (int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));
						if (terrainRaycastResult != null)
						{
							Plane plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
							vector = position;
							if (plane.Normal.X != 0f)
							{
								particle.Velocity *= new Vector3(-0.05f, 0.05f, 0.05f);
							}
							if (plane.Normal.Y != 0f)
							{
								particle.Velocity *= new Vector3(0.05f, -0.05f, 0.05f);
							}
							if (plane.Normal.Z != 0f)
							{
								particle.Velocity *= new Vector3(0.05f, 0.05f, -0.05f);
							}
						}
						particle.Position = vector;
						BloodParticleSystem.Particle particle2 = particle;
						particle2.Velocity.Y = particle2.Velocity.Y + -9.81f * dt;
						particle.Velocity *= s2;
						particle.Color *= MathUtils.Saturate(particle.TimeToLive);
						particle.TextureSlot = (int)(2.99f * MathUtils.Saturate(1f - particle.TimeToLive / 1.5f));
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!this.IsStopped && this.m_toGenerate >= 1f)
				{
					Vector3 v = this.m_random.Vector3(0f, 1f);
					particle.IsActive = true;
					particle.Position = this.Position + 0.05f * v;
					particle.Color = Color.MultiplyColorOnly(c, this.m_random.Float(0.7f, 1f));
					particle.Velocity = MathUtils.Lerp(1f, 2.5f, num5) * Vector3.Normalize(this.Direction + 0.25f * v);
					particle.TimeToLive = 1.5f;
					particle.Size = new Vector2(0.4f);
					particle.FlipX = this.m_random.Bool();
					particle.FlipY = this.m_random.Bool();
					this.m_toGenerate -= 1f;
				}
			}
			return this.IsStopped && !flag;
		}

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;

			public float TimeToLive;
		}
	}
}
