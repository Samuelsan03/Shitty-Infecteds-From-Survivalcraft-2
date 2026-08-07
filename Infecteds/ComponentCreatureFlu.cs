using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureFlu : Component, IUpdateable
	{
		private const float FluEffectInterval = 13f;
		private const float CoughDuration = 4f;
		private const float SneezeDuration = 1f;
		private const float HealthDamagePerFlu = 0.1f;
		private const float FluEffectCheckInterval = 5f;
		private const float DefaultFluDuration = 900f;

		private float m_fluResistance;
		private float m_fluDuration;

		private float m_coughDuration;
		private float m_sneezeDuration;

		private double m_lastEffectTime = -1000.0;
		private double m_lastCoughTime = -1000.0;

		private string m_sneezeSoundPath;
		private string m_coughSoundPath;

		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemAudio m_subsystemAudio;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentHealth m_componentHealth;
		private ComponentLocomotion m_componentLocomotion;

		private Random m_random = new Random();

		public bool HasFlu => m_fluDuration > 0f;
		public bool IsCoughing => m_coughDuration > 0f;
		public bool IsSneezing => m_sneezeDuration > 0f;
		public bool HasActiveSymptoms => m_coughDuration > 0f || m_sneezeDuration > 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		/// <summary>
		/// Calcula la efectividad de la gripe basándose en la resistencia.
		/// Resistencia 0.1 = efectividad 0.9 (muy afectado, tose fuerte, mucho daño)
		/// Resistencia 0.9 = efectividad 0.1 (poco afectado, tose débil, casi sin daño)
		/// </summary>
		private float FluEffectiveness => 1f - m_fluResistance;

		public void TryInfect(float attackerIntensity)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

			// La resistencia ahora afecta la probabilidad de contagiarse (igual que el veneno)
			float infectionChance = 1f - m_fluResistance;
			if (m_random.Float(0f, 1f) < infectionChance)
			{
				m_fluDuration = DefaultFluDuration;
			}
		}

		public void ForceInfect(float duration)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

			m_fluDuration = duration;
		}

		private void Sneeze()
		{
			m_sneezeDuration = SneezeDuration;
			PlayFluSound(m_sneezeSoundPath);

			if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
				// El ruido también se escala con la efectividad
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f * FluEffectiveness, 10f);
			}
		}

		private void Cough()
		{
			m_lastCoughTime = m_subsystemTime.GameTime;
			m_coughDuration = CoughDuration;
			PlayFluSound(m_coughSoundPath);

			if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f * FluEffectiveness, 10f);
			}
		}

		private void PlayFluSound(string soundPath)
		{
			if (m_subsystemAudio == null || string.IsNullOrEmpty(soundPath) || m_componentCreature == null || m_componentCreature.ComponentBody == null)
				return;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			// El volumen del sonido se escala con la efectividad (los débiles tosen más fuerte)
			float volume = 0.75f * FluEffectiveness;
			float pitch = m_random.Float(-0.2f, 0.2f);
			float minDistance = 10f;

			ReadOnlyList<ContentInfo> contentList = ContentManager.List(soundPath);
			if (contentList.Count > 0)
			{
				int index = m_random.Int(0, contentList.Count - 1);
				m_subsystemAudio.PlaySound(contentList[index].ContentPath, volume, pitch, position, minDistance, 0f);
			}
			else
			{
				m_subsystemAudio.PlaySound(soundPath, volume, pitch, position, minDistance, 0f);
			}
		}

		private void FluEffect()
		{
			m_lastEffectTime = m_subsystemTime.GameTime;

			// CORRECCIÓN: El daño ahora se escala con la efectividad (resistencia inversa)
			float damageToApply = MathUtils.Min(HealthDamagePerFlu * FluEffectiveness,
				m_componentHealth != null ? m_componentHealth.Health : 0f);

			if (damageToApply > 0f && m_componentHealth != null && m_componentHealth.Health > 0f)
			{
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.75, delegate
				{
					if (m_componentHealth != null && m_componentHealth.Health > 0f)
					{
						m_componentHealth.Injure(damageToApply, null, false, "Flu");
					}
				});
			}

			// La probabilidad de toser en vez de estornudar aumenta con la efectividad
			float coughChance = 0.3f + 0.5f * FluEffectiveness;
			if (m_coughDuration == 0f && (m_subsystemTime.GameTime - m_lastCoughTime > 40.0 || m_random.Bool(coughChance)))
			{
				Cough();
			}
			else if (m_sneezeDuration == 0f)
			{
				Sneeze();
			}
		}

		private void UpdateCoughSneezeEffects(float dt)
		{
			if (!HasActiveSymptoms)
				return;

			// CORRECCIÓN BUG MUERTE: Limpiar tos/estornudo inmediatamente si muere
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				m_coughDuration = 0f;
				m_sneezeDuration = 0f;
				return;
			}

			m_coughDuration = MathUtils.Max(m_coughDuration - dt, 0f);
			m_sneezeDuration = MathUtils.Max(m_sneezeDuration - dt, 0f);

			if (m_componentCreatureModel != null && m_componentLocomotion != null)
			{
				// CORRECCIÓN: La inclinación de la cabeza al toser se escala con la efectividad
				// Resistencia 0.1 -> se agacha muchísimo (-65f)
				// Resistencia 0.9 -> casi no se agacha (-38f)
				float maxAngle = -35f - 30f * FluEffectiveness;

				float lookDownAngle = MathUtils.DegToRad(MathUtils.Lerp(-35f, maxAngle,
					SimplexNoise.Noise(4f * (float)MathUtils.Remainder(m_subsystemTime.GameTime, 10000.0))));

				m_componentLocomotion.LookOrder = new Vector2(
					m_componentLocomotion.LookOrder.X,
					Math.Clamp(lookDownAngle - m_componentLocomotion.LookAngles.Y, -3f, 3f));
			}

			if (m_componentCreature != null
				&& m_componentCreature.ComponentBody != null
				&& m_componentCreatureModel != null)
			{
				// CORRECCIÓN: La fuerza y frecuencia del impulso al toser se escala con la efectividad
				float impulseChance = 2f * dt * FluEffectiveness;
				if (m_random.Bool(impulseChance))
				{
					float impulseStrength = -1.2f * FluEffectiveness;
					m_componentCreature.ComponentBody.ApplyImpulse(
						impulseStrength * m_componentCreatureModel.EyeRotation.GetForwardVector());
				}
			}
		}

		public virtual void Update(float dt)
		{
			// ============================================
			// CORRECCIÓN BUG MUERTE: Parar todo al inicio
			// ============================================
			bool isDead = m_componentHealth != null && m_componentHealth.Health <= 0f;

			if (isDead)
			{
				m_fluDuration = 0f;
				m_coughDuration = 0f;
				m_sneezeDuration = 0f;
				return;
			}

			if (m_fluDuration > 0f)
			{
				m_fluDuration = MathUtils.Max(m_fluDuration - dt, 0f);

				if (m_componentHealth != null && m_componentHealth.Health > 0f)
				{
					if (m_subsystemTime.PeriodicGameTimeEvent(FluEffectCheckInterval, -0.01f)
						&& m_subsystemTime.GameTime - m_lastEffectTime > FluEffectInterval)
					{
						FluEffect();
					}
				}

				UpdateCoughSneezeEffects(dt);
			}
			else
			{
				UpdateCoughSneezeEffects(dt);

				if (!HasActiveSymptoms)
				{
					m_lastEffectTime = -1000.0;
					m_lastCoughTime = -1000.0;
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>();
			m_componentHealth = Entity.FindComponent<ComponentHealth>();
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>();

			m_sneezeSoundPath = valuesDictionary.GetValue<string>("SneezeSoundPath");
			m_coughSoundPath = valuesDictionary.GetValue<string>("CoughSoundPath");

			m_fluResistance = valuesDictionary.GetValue<float>("FluResistance", 0.5f);
			m_fluDuration = valuesDictionary.GetValue<float>("FluDuration", 0f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("FluResistance", m_fluResistance);
			valuesDictionary.SetValue<float>("FluDuration", m_fluDuration);
		}
	}
}
