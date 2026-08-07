using System;
using GameEntitySystem;
using TemplatesDatabase;
using Engine;

namespace Game
{
	public class ComponentDeathSpawner : Component, IUpdateable
	{
		public bool IsEnabled { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public virtual void Update(float dt)
		{
			if (!IsEnabled || m_hasSpawned || m_spawnEntityTemplateNames == null || m_spawnEntityTemplateNames.Length == 0)
			{
				return;
			}

			if (m_componentHealth.Health <= 0f)
			{
				m_hasSpawned = true;

				if (s_random.Float(0f, 1f) < m_probability)
				{
					m_willSpawn = true;

					m_particleSystem = new DeathSpawnParticleSystem();
					m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
					m_particleSystem.BoundingBox = m_componentBody.BoundingBox;

					m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentBody.Position, 3f, true);

					if (!m_componentSpawn.IsDespawning)
					{
						m_componentSpawn.DespawnDuration = 3f;
						m_componentSpawn.Despawn();
					}
				}
			}

			if (m_particleSystem != null && !m_particleSystem.Stopped)
			{
				m_particleSystem.BoundingBox = m_componentBody.BoundingBox;
			}
		}

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

			ComponentSpawn componentSpawn = m_componentSpawn;
			componentSpawn.Despawned = (Action<ComponentSpawn>)Delegate.Combine(componentSpawn.Despawned, new Action<ComponentSpawn>(this.ComponentSpawn_Despawned));
		}

		public virtual void ComponentSpawn_Despawned(ComponentSpawn componentSpawn)
		{
			// Cuando el cadáver termina de desaparecer, aparece SOLO UNA criatura
			if (m_willSpawn && m_spawnEntityTemplateNames != null && m_spawnEntityTemplateNames.Length > 0)
			{
				Vector3 position = m_componentBody.Position;
				Vector3 velocity = m_componentBody.Velocity;

				// Elegimos SOLO UN template aleatorio de la lista
				int randomIndex = s_random.Int(0, m_spawnEntityTemplateNames.Length - 1);
				string templateName = m_spawnEntityTemplateNames[randomIndex];

				if (!string.IsNullOrEmpty(templateName))
				{
					Vector3 spawnOffset = new Vector3(s_random.Float(-1f, 1f), 0f, s_random.Float(-1f, 1f));

					Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
					ComponentBody entityBody = entity.FindComponent<ComponentBody>(true);
					entityBody.Position = position + spawnOffset;
					entityBody.Rotation = m_componentBody.Rotation;
					entityBody.Velocity = velocity;
					entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0.5f;

					base.Project.AddEntity(entity);
				}
			}

			if (m_particleSystem != null)
			{
				m_particleSystem.Stopped = true;
			}
		}

		public string[] m_spawnEntityTemplateNames;
		public float m_probability;
		public ComponentBody m_componentBody;
		public ComponentHealth m_componentHealth;
		public ComponentSpawn m_componentSpawn;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemAudio m_subsystemAudio;
		public DeathSpawnParticleSystem m_particleSystem;
		public bool m_hasSpawned;
		public bool m_willSpawn;
		public static Random s_random = new Random();
	}
}
