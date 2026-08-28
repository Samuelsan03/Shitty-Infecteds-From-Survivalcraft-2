using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class PoisonVomitParticleSystem : ParticleSystem<PoisonVomitParticleSystem.Particle>
	{
		public Vector3 Position { get; set; }

		public Vector3 Direction { get; set; }

		public bool IsStopped { get; set; }

		public ComponentBody OwnerBody { get; set; }

		public ComponentCreature Attacker { get; set; }

		public float PoisonIntensity { get; set; } = 0.5f;

		public PoisonVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemTime time) : base(120)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemTime = time;
			base.Texture = ContentManager.Get<Texture2D>("Textures/Gui/vomito venenoso");
			base.TextureSlotsCount = 3;
		}

		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);

			if (m_subsystemTime != null && m_subsystemTime.GameTime - m_lastClearTime > 2.0)
			{
				m_recentlyAffectedEntities.Clear();
				m_lastClearTime = m_subsystemTime.GameTime;
			}

			m_toGenerate += (IsStopped ? 0f : 100f * dt);

			bool anyActive = false;

			for (int i = 0; i < base.Particles.Length; i++)
			{
				PoisonVomitParticleSystem.Particle particle = base.Particles[i];

				if (particle.IsActive)
				{
					anyActive = true;
					particle.Time += dt;

					if (particle.Time <= particle.Duration)
					{
						particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);

						if (!particle.IsStuck)
						{
							Vector3 oldPos = particle.Position;
							Vector3 newPos = oldPos + particle.Velocity * dt;

							particle.Velocity.Y -= 12f * dt;
							particle.Velocity *= MathF.Pow(0.4f, dt);

							TerrainRaycastResult? terrainHit = null;
							if (m_subsystemTerrain != null)
							{
								terrainHit = m_subsystemTerrain.Raycast(oldPos, newPos, false, true,
									(int value, float _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable_(value));
							}

							if (terrainHit != null)
							{
								particle.IsStuck = true;
								particle.Velocity = Vector3.Zero;
								particle.Position = oldPos;
							}
							else
							{
								if (CheckBodyCollisionAndInfect(newPos, particle))
								{
									particle.IsStuck = true;
									particle.Velocity = Vector3.Zero;
								}
								else
								{
									particle.Position = newPos;
								}
							}
						}
					}
					else
					{
						particle.IsActive = false;
					}
				}
				else if (!IsStopped && m_toGenerate >= 1f)
				{
					Vector3 spread = m_random.Vector3(0f, 0.25f);
					particle.IsActive = true;
					particle.Position = Position + 0.1f * spread;
					particle.Color = Color.White;

					Vector3 dir = Vector3.Normalize(Direction + 0.35f * spread);
					float speed = m_random.Float(6f, 100f);
					particle.Velocity = dir * speed;

					particle.Time = 0f;
					particle.Duration = m_random.Float(0.8f, 1.3f);
					particle.Size = new Vector2(m_random.Float(0.1f, 0.5f));
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					particle.IsStuck = false;

					m_toGenerate -= 1f;
				}
			}

			m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);

			return IsStopped && !anyActive;
		}

		private bool CheckBodyCollisionAndInfect(Vector3 position, PoisonVomitParticleSystem.Particle particle)
		{
			if (m_subsystemBodies == null) return false;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), 2f, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody body = m_componentBodies.Array[i];

				// CORRECCIÓN: Usar ShouldVomitIgnoreBody para fuego amigo completo
				if (body == OwnerBody || ShittyInfectedsModLoader.ShouldVomitIgnoreBody(OwnerBody, body)) continue;

				int entityId = body.Entity.GetHashCode();
				if (m_recentlyAffectedEntities.Contains(entityId)) continue;

				BoundingBox box = body.BoundingBox;
				box.Min -= new Vector3(0.15f);
				box.Max += new Vector3(0.15f);

				if (box.Contains(position))
				{
					ComponentPlayer player = body.Entity.FindComponent<ComponentPlayer>();
					if (player != null && player.ComponentSickness != null)
					{
						player.ComponentSickness.StartSickness();
					}
					else
					{
						ComponentInfectedWithPoison infection = body.Entity.FindComponent<ComponentInfectedWithPoison>();
						if (infection != null)
						{
							infection.TryInfect(PoisonIntensity);
						}
					}

					m_recentlyAffectedEntities.Add(entityId);

					particle.Position = position;
					return true;
				}
			}

			return false;
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public float m_toGenerate;
		public double m_lastClearTime;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public HashSet<int> m_recentlyAffectedEntities = new HashSet<int>();
		public Random m_random = new Random();

		public class Particle : Game.Particle
		{
			public Vector3 Velocity;
			public float Time;
			public float Duration;
			public bool IsStuck;
		}
	}
}