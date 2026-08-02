using System;
using GameEntitySystem;
using TemplatesDatabase;
using Engine;

namespace Game
{
	public class ComponentDeathSpawner : Component, IUpdateable
	{
		// Token: 0x170001F0 RID: 496
		public bool IsEnabled { get; set; }

		// Token: 0x170001F1 RID: 497
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000DE0 RID: 3552
		public virtual void Update(float dt)
		{
			if (!IsEnabled || m_hasSpawned || m_spawnEntityTemplateNames == null || m_spawnEntityTemplateNames.Length == 0)
			{
				return;
			}

			// Cuando la criatura acaba de morir
			if (m_componentHealth.Health <= 0f)
			{
				m_hasSpawned = true;

				// Verificamos la probabilidad
				if (s_random.Float(0f, 1f) < m_probability)
				{
					m_willSpawn = true; // Marcamos que sí debe spawear al terminar

					// 1. Iniciamos las partículas verdes
					m_particleSystem = new DeathSpawnParticleSystem();
					m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
					m_particleSystem.BoundingBox = m_componentBody.BoundingBox;

					// 2. Reproducimos el sonido
					m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentBody.Position, 3f, true);

					// 3. Iniciamos el proceso de desaparición del cadáver (igual que el Shapeshifter)
					if (!m_componentSpawn.IsDespawning)
					{
						m_componentSpawn.DespawnDuration = 3f; // 3 segundos desvaneciéndose con las partículas
						m_componentSpawn.Despawn();
					}
				}
			}

			// Actualizamos la posición de las partículas mientras el cuerpo existe
			if (m_particleSystem != null && !m_particleSystem.Stopped)
			{
				m_particleSystem.BoundingBox = m_componentBody.BoundingBox;
			}
		}

		// Token: 0x06000DE1 RID: 3553
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			m_componentSpawn = base.Entity.FindComponent<ComponentSpawn>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);

			m_probability = valuesDictionary.GetValue<float>("Probability");
			string spawnEntitiesString = valuesDictionary.GetValue<string>("SpawnEntities");

			if (!string.IsNullOrEmpty(spawnEntitiesString))
			{
				m_spawnEntityTemplateNames = spawnEntitiesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < m_spawnEntityTemplateNames.Length; i++)
				{
					m_spawnEntityTemplateNames[i] = m_spawnEntityTemplateNames[i].Trim();
					if (!string.IsNullOrEmpty(m_spawnEntityTemplateNames[i]))
					{
						DatabaseManager.FindEntityValuesDictionary(m_spawnEntityTemplateNames[i], true);
					}
				}
			}

			IsEnabled = true;

			// Nos suscribimos al evento cuando el cadáver termina de desaparecer
			ComponentSpawn componentSpawn = m_componentSpawn;
			componentSpawn.Despawned = (Action<ComponentSpawn>)Delegate.Combine(componentSpawn.Despawned, new Action<ComponentSpawn>(this.ComponentSpawn_Despawned));
		}

		// Token: 0x06000DE3 RID: 3555
		public virtual void ComponentSpawn_Despawned(ComponentSpawn componentSpawn)
		{
			// 4. Cuando el cadáver termina de desaparecer, aparecen las criaturas
			if (m_willSpawn)
			{
				Vector3 position = m_componentBody.Position;
				Vector3 velocity = m_componentBody.Velocity;

				foreach (string templateName in m_spawnEntityTemplateNames)
				{
					if (!string.IsNullOrEmpty(templateName))
					{
						Vector3 spawnOffset = new Vector3(s_random.Float(-1f, 1f), 0f, s_random.Float(-1f, 1f));

						Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
						ComponentBody componentBody = entity.FindComponent<ComponentBody>(true);
						componentBody.Position = position + spawnOffset;
						componentBody.Rotation = m_componentBody.Rotation;
						componentBody.Velocity = velocity;
						entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0.5f;

						base.Project.AddEntity(entity);
					}
				}
			}

			// Detenemos las partículas
			if (m_particleSystem != null)
			{
				m_particleSystem.Stopped = true;
			}
		}

		// Token: 0x0400081A RID: 2074
		public string[] m_spawnEntityTemplateNames;

		// Token: 0x0400081B RID: 2075
		public float m_probability;

		// Token: 0x0400081C RID: 2076
		public ComponentBody m_componentBody;

		// Token: 0x0400081D RID: 2077
		public ComponentHealth m_componentHealth;

		// Token: 0x0400081E RID: 2078
		public ComponentSpawn m_componentSpawn;

		// Token: 0x0400081F RID: 2079
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x04000820 RID: 2080
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000821 RID: 2081
		public DeathSpawnParticleSystem m_particleSystem;

		// Token: 0x04000822 RID: 2082
		public bool m_hasSpawned;

		// Token: 0x04000824 RID: 2084
		public bool m_willSpawn;

		// Token: 0x04000823 RID: 2083
		public static Random s_random = new Random();
	}
}
