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

		// Salud mínima que la gripe puede causar (no mata)
		private const float MinimumHealthFromFlu = 0.1f;

		// Factor mínimo de velocidad durante gripe (0.5 = mitad de velocidad)
		private const float MinimumSpeedFactor = 0.5f;

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

		// Velocidades originales para restaurar después de la gripe
		private float m_originalWalkSpeed;
		private float m_originalLadderSpeed;
		private float m_originalFlySpeed;
		private float m_originalSwimSpeed;
		private float m_originalJumpSpeed;

		private bool m_speedsStored;

		public bool HasFlu => m_fluDuration > 0f;
		public bool IsCoughing => m_coughDuration > 0f;
		public bool IsSneezing => m_sneezeDuration > 0f;
		public bool HasActiveSymptoms => m_coughDuration > 0f || m_sneezeDuration > 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		/// <summary>
		/// Calcula la efectividad de la gripe basándose en la resistencia.
		/// </summary>
		private float FluEffectiveness => 1f - m_fluResistance;

		public void TryInfect(float attackerIntensity)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

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

		/// <summary>
		/// Guarda las velocidades originales de locomoción la primera vez
		/// </summary>
		private void StoreOriginalSpeeds()
		{
			if (m_speedsStored || m_componentLocomotion == null)
				return;

			m_originalWalkSpeed = m_componentLocomotion.WalkSpeed;
			m_originalLadderSpeed = m_componentLocomotion.LadderSpeed;
			m_originalFlySpeed = m_componentLocomotion.FlySpeed;
			m_originalSwimSpeed = m_componentLocomotion.SwimSpeed;
			m_originalJumpSpeed = m_componentLocomotion.JumpSpeed;

			m_speedsStored = true;
		}

		/// <summary>
		/// Restaura las velocidades originales de locomoción
		/// </summary>
		private void RestoreOriginalSpeeds()
		{
			if (!m_speedsStored || m_componentLocomotion == null)
				return;

			m_componentLocomotion.WalkSpeed = m_originalWalkSpeed;
			m_componentLocomotion.LadderSpeed = m_originalLadderSpeed;
			m_componentLocomotion.FlySpeed = m_originalFlySpeed;
			m_componentLocomotion.SwimSpeed = m_originalSwimSpeed;
			m_componentLocomotion.JumpSpeed = m_originalJumpSpeed;

			m_speedsStored = false;
		}

		/// <summary>
		/// Actualiza las velocidades de locomoción basándose en la efectividad de la gripe
		/// </summary>
		private void UpdateLocomotionSpeeds()
		{
			if (m_componentLocomotion == null)
				return;

			if (m_fluDuration > 0f)
			{
				if (!m_speedsStored)
				{
					StoreOriginalSpeeds();
				}

				float effectiveness = FluEffectiveness;
				float speedFactor = MathUtils.Lerp(1f, MinimumSpeedFactor, effectiveness);

				m_componentLocomotion.WalkSpeed = m_originalWalkSpeed * speedFactor;
				m_componentLocomotion.LadderSpeed = m_originalLadderSpeed * speedFactor;
				m_componentLocomotion.FlySpeed = m_originalFlySpeed * speedFactor;
				m_componentLocomotion.SwimSpeed = m_originalSwimSpeed * speedFactor;
				m_componentLocomotion.JumpSpeed = m_originalJumpSpeed * speedFactor;
			}
			else if (m_speedsStored)
			{
				RestoreOriginalSpeeds();
			}
		}

		private void Sneeze()
		{
			m_sneezeDuration = SneezeDuration;
			PlayFluSound(m_sneezeSoundPath);

			if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
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

			// Calcular daño base escalado con efectividad
			float damageToApply = HealthDamagePerFlu * FluEffectiveness;

			// VERIFICACIÓN: No dañar si ya está en el mínimo de salud por gripe
			if (m_componentHealth != null && m_componentHealth.Health > MinimumHealthFromFlu)
			{
				float maxSafeDamage = m_componentHealth.Health - MinimumHealthFromFlu;
				damageToApply = MathUtils.Min(damageToApply, maxSafeDamage);

				if (damageToApply > 0f)
				{
					float damageToDefer = damageToApply;
					m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.75, delegate
					{
						if (m_componentHealth != null && m_componentHealth.Health > MinimumHealthFromFlu)
						{
							float safeDamage = MathUtils.Min(damageToDefer, m_componentHealth.Health - MinimumHealthFromFlu);
							if (safeDamage > 0f)
							{
								m_componentHealth.Injure(safeDamage, null, false, "Flu");
							}
						}
					});
				}
			}

			// ============================================
			// CORRECCIÓN: Secuencia Cough -> Sneeze
			// ============================================

			float coughChance = 0.3f + 0.5f * FluEffectiveness;

			// Primero verificar si puede toser
			if (m_coughDuration == 0f && (m_subsystemTime.GameTime - m_lastCoughTime > 40.0 || m_random.Bool(coughChance)))
			{
				// Tose
				Cough();

				// Programar estornudo DESPUÉS de que termine la tos
				float coughEndTime = (float)m_lastCoughTime + CoughDuration;
				m_subsystemTime.QueueGameTimeDelayedExecution(coughEndTime, delegate
				{
					// Verificar que sigue vivo, con gripe, y no está ya estornudando
					if (m_sneezeDuration == 0f
						&& m_fluDuration > 0f
						&& m_componentHealth != null
						&& m_componentHealth.Health > 0f)
					{
						Sneeze();
					}
				});
			}
			else if (m_sneezeDuration == 0f)
			{
				// Si no tosió, estornuda directamente
				Sneeze();
			}
		}

		private void UpdateCoughSneezeEffects(float dt)
		{
			if (!HasActiveSymptoms)
				return;

			// Limpiar tos/estornudo si muere
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
			bool isDead = m_componentHealth != null && m_componentHealth.Health <= 0f;

			if (isDead)
			{
				if (m_speedsStored)
				{
					RestoreOriginalSpeeds();
				}
				m_fluDuration = 0f;
				m_coughDuration = 0f;
				m_sneezeDuration = 0f;
				return;
			}

			if (m_fluDuration > 0f)
			{
				m_fluDuration = MathUtils.Max(m_fluDuration - dt, 0f);

				// ACTUALIZAR VELOCIDADES DE LOCOMOCIÓN
				UpdateLocomotionSpeeds();

				// Solo aplicar daño si está por encima del mínimo
				if (m_componentHealth != null && m_componentHealth.Health > MinimumHealthFromFlu)
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
				if (m_speedsStored)
				{
					RestoreOriginalSpeeds();
				}

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

			m_fluResistance = valuesDictionary.GetValue<float>("FluResistance");
			m_fluDuration = valuesDictionary.GetValue<float>("FluDuration");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("FluResistance", m_fluResistance);
			valuesDictionary.SetValue<float>("FluDuration", m_fluDuration);
		}
	}
}
