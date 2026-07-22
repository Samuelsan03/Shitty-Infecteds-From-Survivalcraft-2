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

		private float m_fluDuration;
		private float m_fluIntensity;

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

		public void TryInfect(float attackerIntensity)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

			bool wasAlreadySick = m_fluDuration > 0f;
			m_fluIntensity = MathUtils.Max(m_fluIntensity, MathUtils.Clamp(attackerIntensity, 0f, 1f));
			m_fluDuration = DefaultFluDuration;
		}

		public void ForceInfect(float intensity, float duration)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

			m_fluIntensity = MathUtils.Clamp(intensity, 0f, 1f);
			m_fluDuration = duration;
		}

		private void Sneeze()
		{
			m_sneezeDuration = SneezeDuration;
			PlayFluSound(m_sneezeSoundPath);

			if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);
			}
		}

		private void Cough()
		{
			m_lastCoughTime = m_subsystemTime.GameTime;
			m_coughDuration = CoughDuration;
			PlayFluSound(m_coughSoundPath);

			if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);
			}
		}

		private void PlayFluSound(string soundPath)
		{
			if (m_subsystemAudio == null || string.IsNullOrEmpty(soundPath) || m_componentCreature == null || m_componentCreature.ComponentBody == null)
				return;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			float volume = 0.75f;
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

			float damageToApply = MathUtils.Min(HealthDamagePerFlu * m_fluIntensity,
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

			if (m_coughDuration == 0f && (m_subsystemTime.GameTime - m_lastCoughTime > 40.0 || m_random.Bool(0.5f)))
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
				float lookDownAngle = MathUtils.DegToRad(MathUtils.Lerp(-35f, -65f,
					SimplexNoise.Noise(4f * (float)MathUtils.Remainder(m_subsystemTime.GameTime, 10000.0))));

				m_componentLocomotion.LookOrder = new Vector2(
					m_componentLocomotion.LookOrder.X,
					Math.Clamp(lookDownAngle - m_componentLocomotion.LookAngles.Y, -3f, 3f));
			}

			if (m_componentCreature != null
				&& m_componentCreature.ComponentBody != null
				&& m_componentCreatureModel != null
				&& m_random.Bool(2f * dt))
			{
				m_componentCreature.ComponentBody.ApplyImpulse(
					-1.2f * m_componentCreatureModel.EyeRotation.GetForwardVector());
			}
		}

		public virtual void Update(float dt)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				m_fluDuration = 0f;
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
					m_fluIntensity = 0f;
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

			m_fluDuration = valuesDictionary.GetValue<float>("FluDuration", 0f);
			m_fluIntensity = valuesDictionary.GetValue<float>("FluIntensity", 0f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("FluDuration", m_fluDuration);
			valuesDictionary.SetValue<float>("FluIntensity", m_fluIntensity);
		}
	}
}
