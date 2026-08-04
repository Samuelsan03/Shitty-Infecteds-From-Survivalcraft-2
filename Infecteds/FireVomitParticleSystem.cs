using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;

namespace Game
{
	public class FireVomitParticleSystem : ParticleSystem<FireVomitParticleSystem.Particle>
	{
		// Token: 0x170002F3 RID: 755
		public Vector3 Position { get; set; }

		// Token: 0x170002F4 RID: 756
		public Vector3 Direction { get; set; }

		// Token: 0x170002F5 RID: 757
		public bool IsStopped { get; set; }

		// Token: 0x170002F6 RID: 758
		public ComponentBody OwnerBody { get; set; }

		// Token: 0x170002F7 RID: 759
		public ComponentCreature Attacker { get; set; }

		// Token: 0x06001348 RID: 4936
		public FireVomitParticleSystem(SubsystemTerrain terrain, SubsystemBodies bodies, SubsystemTime time) : base(120)
		{
			m_subsystemTerrain = terrain;
			m_subsystemBodies = bodies;
			m_subsystemTime = time;
			base.Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
			base.TextureSlotsCount = 3;
		}

		// Token: 0x06001349 RID: 4937
		public override bool Simulate(float dt)
		{
			dt = Math.Clamp(dt, 0f, 0.1f);

			// Limpiar lista de entidades afectadas cada 2 segundos para permitir re-ignición y daño periódico
			if (m_subsystemTime != null && m_subsystemTime.GameTime - m_lastClearTime > 2.0)
			{
				m_recentlyAffectedEntities.Clear();
				m_lastClearTime = m_subsystemTime.GameTime;
			}

			// Generar partículas mientras no esté detenido
			m_toGenerate += (IsStopped ? 0f : 100f * dt);

			bool anyActive = false;

			for (int i = 0; i < base.Particles.Length; i++)
			{
				FireVomitParticleSystem.Particle particle = base.Particles[i];

				if (particle.IsActive)
				{
					anyActive = true;
					particle.Time += dt;

					if (particle.Time <= particle.Duration)
					{
						// Animar textura de fuego
						particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);

						if (!particle.IsStuck)
						{
							Vector3 oldPos = particle.Position;
							Vector3 newPos = oldPos + particle.Velocity * dt;

							// Gravedad reducida para fuego flotante
							particle.Velocity.Y -= 4f * dt;

							// Desaceleración
							particle.Velocity *= MathF.Pow(0.5f, dt);

							// Colisión con terreno
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
								// Colisión con cuerpos - Genera incendio y causa un poco de daño
								if (CheckBodyCollisionAndSetFire(newPos, particle))
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
					// Generar nueva partícula
					Vector3 spread = m_random.Vector3(0f, 0.2f);
					particle.IsActive = true;
					particle.Position = Position + 0.1f * spread;
					particle.Color = Color.White;

					// Dirección con dispersión
					Vector3 dir = Vector3.Normalize(Direction + 0.3f * spread);
					float speed = m_random.Float(8f, 100f);
					particle.Velocity = dir * speed;

					particle.Time = 0f;
					particle.Duration = m_random.Float(0.7f, 1.1f);
					particle.Size = new Vector2(m_random.Float(0.1f, 0.56f));
					particle.FlipX = m_random.Bool();
					particle.FlipY = m_random.Bool();
					particle.IsStuck = false;

					m_toGenerate -= 1f;
				}
			}

			m_toGenerate = MathUtils.Remainder(m_toGenerate, 1f);

			return IsStopped && !anyActive;
		}

		/// <summary>
		/// Verifica colisión con cuerpos, genera incendio, causa un poco de daño 
		/// y registra la causa de muerte usando el sistema de idiomas.
		/// </summary>
		private bool CheckBodyCollisionAndSetFire(Vector3 position, FireVomitParticleSystem.Particle particle)
		{
			if (m_subsystemBodies == null) return false;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), 2f, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody body = m_componentBodies.Array[i];

				// Ignorar al dueño del vómito
				if (body == OwnerBody) continue;

				// Verificar si ya fue afectado recientemente (evitar spam)
				int entityId = body.Entity.GetHashCode();
				if (m_recentlyAffectedEntities.Contains(entityId)) continue;

				// Verificar si la partícula está dentro del bounding box (expandido ligeramente)
				BoundingBox box = body.BoundingBox;
				box.Min -= new Vector3(0.15f);
				box.Max += new Vector3(0.15f);

				if (box.Contains(position))
				{
					// Generar incendio
					ComponentOnFire onFire = body.Entity.FindComponent<ComponentOnFire>();
					if (onFire != null)
					{
						onFire.SetOnFire(Attacker, m_random.Float(8f, 14f));
					}

					// Causa un poco de daño directo (5% de la vida) y registra la causa de muerte usando lang
					ComponentHealth targetHealth = body.Entity.FindComponent<ComponentHealth>();
					if (targetHealth != null && targetHealth.Health > 0f)
					{
						targetHealth.Injure(0.05f, Attacker, false, LanguageControl.Get("ComponentMonsterSkills", 1));
					}

					m_recentlyAffectedEntities.Add(entityId);

					particle.Position = position;
					return true;
				}
			}

			return false;
		}

		// Token: 0x04000D6D RID: 3437
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000D6E RID: 3438
		public SubsystemBodies m_subsystemBodies;

		// Token: 0x04000D6F RID: 3439
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000D70 RID: 3440
		public float m_toGenerate;

		// Token: 0x04000D71 RID: 3441
		public double m_lastClearTime;

		// Token: 0x04000D72 RID: 3442
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		// Token: 0x04000D73 RID: 3443
		public HashSet<int> m_recentlyAffectedEntities = new HashSet<int>();

		// Token: 0x04000D74 RID: 3444
		public Random m_random = new Random();

		// Token: 0x020005A0 RID: 1440
		public class Particle : Game.Particle
		{
			// Token: 0x0400200A RID: 8202
			public Vector3 Velocity;

			// Token: 0x0400200B RID: 8203
			public float Time;

			// Token: 0x0400200C RID: 8204
			public float Duration;

			// Token: 0x0400200D RID: 8205
			public bool IsStuck;
		}
	}
}
