using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentInfectedWithPoison : Component, IUpdateable
	{
		private const float NauseaCheckInterval = 3f;
		private const float NauseaCooldown = 15f;
		private const float HealthDamagePerVomit = 0.1f;
		private const float MoanCheckInterval = 6f;
		private const float MoanCooldown = 8f;

		private float m_poisonResistance;
		private float m_durationOfPoison;

		private float m_infectionDuration;
		private float m_poisonIntensity;

		private PukeParticleSystem m_pukeParticleSystem;
		private float m_greenoutDuration;
		private float m_greenoutFactor;
		private double? m_lastNauseaTime;
		private double? m_lastMoanTime;

		private bool m_firstVomitQueued;
		private float m_firstVomitTimer = -1f;

		private float m_originalWalkSpeed;
		private float m_originalFlySpeed;
		private float m_originalSwimSpeed;
		private float m_originalJumpSpeed;
		private float m_originalLadderSpeed;
		private bool m_speedsStored;

		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemNoise m_subsystemNoise;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentHealth m_componentHealth;

		private Random m_random = new Random();

		public bool IsInfected => m_infectionDuration > 0f;
		public bool IsVomiting => m_pukeParticleSystem != null;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		/// <summary>
		/// Calcula la efectividad del veneno basándose en la resistencia.
		/// Resistencia 0.1 = efectividad 0.9 (muy afectado)
		/// Resistencia 0.9 = efectividad 0.1 (poco afectado)
		/// </summary>
		private float PoisonEffectiveness => 1f - m_poisonResistance;

		public void TryInfect(float attackerIntensity)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
				return;

			float infectionChance = 1f - m_poisonResistance;
			if (m_random.Float(0f, 1f) < infectionChance)
			{
				bool wasAlreadyInfected = m_infectionDuration > 0f;
				m_poisonIntensity = MathUtils.Max(m_poisonIntensity, MathUtils.Clamp(attackerIntensity, 0f, 1f));
				m_infectionDuration = m_durationOfPoison;

				if (!wasAlreadyInfected)
				{
					m_firstVomitQueued = false;
					m_firstVomitTimer = 3.0f;
				}
			}
		}

		private void StoreOriginalSpeeds()
		{
			if (m_componentLocomotion != null && !m_speedsStored)
			{
				m_originalWalkSpeed = m_componentLocomotion.WalkSpeed;
				m_originalFlySpeed = m_componentLocomotion.FlySpeed;
				m_originalSwimSpeed = m_componentLocomotion.SwimSpeed;
				m_originalJumpSpeed = m_componentLocomotion.JumpSpeed;
				m_originalLadderSpeed = m_componentLocomotion.LadderSpeed;
				m_speedsStored = true;
			}
		}

		private void RestoreOriginalSpeeds()
		{
			if (m_componentLocomotion != null && m_speedsStored)
			{
				m_componentLocomotion.WalkSpeed = m_originalWalkSpeed;
				m_componentLocomotion.FlySpeed = m_originalFlySpeed;
				m_componentLocomotion.SwimSpeed = m_originalSwimSpeed;
				m_componentLocomotion.JumpSpeed = m_originalJumpSpeed;
				m_componentLocomotion.LadderSpeed = m_originalLadderSpeed;
				m_speedsStored = false;
			}
		}

		private void NauseaEffect()
		{
			m_lastNauseaTime = m_subsystemTime.GameTime;
			m_lastMoanTime = m_subsystemTime.GameTime;

			if (m_componentCreature != null && m_componentCreature.ComponentCreatureSounds != null)
			{
				m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}

			// CORRECCIÓN: El daño ahora se escala con la efectividad (resistencia inversa)
			float effectiveDamage = HealthDamagePerVomit * m_poisonIntensity * PoisonEffectiveness;
			float damageToApply = MathUtils.Min(effectiveDamage, m_componentHealth != null ? m_componentHealth.Health : 0f);

			if (damageToApply > 0f && m_componentHealth != null && m_componentHealth.Health > 0f)
			{
				m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + 0.75, delegate
				{
					if (m_componentHealth != null && m_componentHealth.Health > 0f)
					{
						m_componentHealth.Injure(damageToApply, null, false, "Poison");
					}
				});
			}

			if (m_pukeParticleSystem == null && m_subsystemParticles != null && m_subsystemTerrain != null)
			{
				m_pukeParticleSystem = new PukeParticleSystem(m_subsystemTerrain);
				m_subsystemParticles.AddParticleSystem(m_pukeParticleSystem, false);

				if (m_subsystemNoise != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
				{
					m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody.Position, 0.25f, 10f);
				}

				// CORRECCIÓN: La duración del efecto visual verde también se escala
				m_greenoutDuration = 0.8f * PoisonEffectiveness;
			}
		}

		private void UpdatePukeParticles(float dt)
		{
			if (m_pukeParticleSystem == null)
				return;

			if (m_componentLocomotion != null && m_componentCreatureModel != null)
			{
				float lookDownAngle = MathUtils.DegToRad(MathUtils.Lerp(-35f, -60f,
					SimplexNoise.Noise(2f * (float)MathUtils.Remainder(m_subsystemTime.GameTime, 10000.0))));

				m_componentLocomotion.LookOrder = new Vector2(
					m_componentLocomotion.LookOrder.X,
					Math.Clamp(lookDownAngle - m_componentLocomotion.LookAngles.Y, -2f, 2f));

				Vector3 upVector = m_componentCreatureModel.EyeRotation.GetUpVector();
				Vector3 forwardVector = m_componentCreatureModel.EyeRotation.GetForwardVector();
				m_pukeParticleSystem.Position = m_componentCreatureModel.EyePosition - 0.08f * upVector + 0.3f * forwardVector;
				m_pukeParticleSystem.Direction = Vector3.Normalize(forwardVector + 0.5f * upVector);
			}

			if (m_pukeParticleSystem.IsStopped)
			{
				m_pukeParticleSystem = null;
			}
		}

		private void UpdateGreenoutEffect(float dt)
		{
			if (m_greenoutDuration > 0f)
			{
				m_greenoutDuration = MathUtils.Max(m_greenoutDuration - dt, 0f);
				// CORRECCIÓN: La intensidad del efecto verde se escala con efectividad
				float maxGreenout = 0.95f * PoisonEffectiveness;
				m_greenoutFactor = MathUtils.Min(m_greenoutFactor + 0.5f * dt, maxGreenout);
			}
			else if (m_greenoutFactor > 0f)
			{
				m_greenoutFactor = MathUtils.Max(m_greenoutFactor - 0.5f * dt, 0f);
			}
		}

		/// <summary>
		/// Calcula el cooldown de náuseas basado en la resistencia.
		/// Menor resistencia = náuseas más frecuentes
		/// </summary>
		private float GetAdjustedNauseaCooldown()
		{
			// Base 15 segundos, reducido por efectividad del veneno
			// Resistencia 0.1 -> cooldown ~3s (muy frecuente)
			// Resistencia 0.9 -> cooldown ~13.5s (casi normal)
			return NauseaCooldown * (1f - 0.8f * PoisonEffectiveness);
		}

		/// <summary>
		/// Calcula el cooldown de gemidos basado en la resistencia.
		/// </summary>
		private float GetAdjustedMoanCooldown()
		{
			return MoanCooldown * (1f - 0.6f * PoisonEffectiveness);
		}

		public virtual void Update(float dt)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				m_infectionDuration = 0f;
			}

			if (m_infectionDuration > 0f)
			{
				m_infectionDuration = MathUtils.Max(m_infectionDuration - dt, 0f);

				if (m_firstVomitTimer > 0f)
				{
					m_firstVomitTimer -= dt;
					if (m_firstVomitTimer <= 0f)
					{
						m_firstVomitQueued = true;
						if (m_componentHealth != null && m_componentHealth.Health > 0f)
						{
							NauseaEffect();
						}
					}
				}

				if (m_componentHealth != null && m_componentHealth.Health > 0f)
				{
					float currentNauseaCooldown = GetAdjustedNauseaCooldown();

					if (m_subsystemTime.PeriodicGameTimeEvent(NauseaCheckInterval, -0.01f))
					{
						bool canNausea = false;
						if (m_lastNauseaTime != null)
						{
							double? timeSinceLastNausea = m_subsystemTime.GameTime - m_lastNauseaTime;
							if (timeSinceLastNausea.HasValue && timeSinceLastNausea.Value > currentNauseaCooldown)
							{
								canNausea = true;
							}
						}

						if (canNausea)
						{
							NauseaEffect();
						}
					}

					float currentMoanCooldown = GetAdjustedMoanCooldown();

					if (m_subsystemTime.PeriodicGameTimeEvent(MoanCheckInterval, 0f))
					{
						bool canMoan = false;
						if (m_lastMoanTime != null)
						{
							double? timeSinceLastMoan = m_subsystemTime.GameTime - m_lastMoanTime;
							if (timeSinceLastMoan.HasValue && timeSinceLastMoan.Value > currentMoanCooldown)
							{
								canMoan = true;
							}
						}

						if (canMoan && m_componentCreature != null && m_componentCreature.ComponentCreatureSounds != null)
						{
							m_lastMoanTime = m_subsystemTime.GameTime;
							m_componentCreature.ComponentCreatureSounds.PlayPainSound();
						}
					}
				}

				UpdatePukeParticles(dt);
				UpdateGreenoutEffect(dt);

				// CORRECCIÓN: El frenado por velocidad también se escala con efectividad
				if (m_componentLocomotion != null
					&& m_componentCreature != null
					&& m_componentCreature.ComponentBody != null
					&& m_subsystemTime.PeriodicGameTimeEvent(0.5f, 0.25f))
				{
					Vector3 velocity = m_componentCreature.ComponentBody.Velocity;
					float horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();

					if (horizontalSpeed > 0.1f)
					{
						// Resistencia 0.1 -> dampening ~0.955 (muy afectado)
						// Resistencia 0.9 -> dampening ~0.995 (casi normal)
						float dampening = 1f - 0.05f * m_poisonIntensity * PoisonEffectiveness;
						dampening = MathUtils.Max(dampening, 1f - 0.05f * PoisonEffectiveness);

						m_componentCreature.ComponentBody.Velocity = new Vector3(
							velocity.X * dampening,
							velocity.Y,
							velocity.Z * dampening);
					}
				}

				// --- LÓGICA DE VELOCIDAD (INFECTADO) ---
				if (!m_speedsStored)
				{
					StoreOriginalSpeeds();
				}

				// CORRECCIÓN: La penalización de velocidad se escala con efectividad
				// Resistencia 0.1 -> penalty mínimo ~0.36 (muy lento)
				// Resistencia 0.9 -> penalty mínimo ~0.96 (casi normal)
				float maxSpeedReduction = 0.6f * PoisonEffectiveness; // Hasta 60% de reducción para el más débil
				float penalty = 1f - maxSpeedReduction * m_poisonIntensity;
				penalty = MathUtils.Max(penalty, 1f - maxSpeedReduction);

				m_componentLocomotion.WalkSpeed = m_originalWalkSpeed * penalty;
				m_componentLocomotion.FlySpeed = m_originalFlySpeed * penalty;
				m_componentLocomotion.SwimSpeed = m_originalSwimSpeed * penalty;
				m_componentLocomotion.JumpSpeed = m_originalJumpSpeed * penalty;
				m_componentLocomotion.LadderSpeed = m_originalLadderSpeed * penalty;
			}
			else
			{
				UpdateGreenoutEffect(dt);
			}

			// --- RESTAURACIÓN DE VELOCIDAD (FUERA DEL if) ---
			if (m_infectionDuration <= 0f && m_speedsStored)
			{
				RestoreOriginalSpeeds();
				m_greenoutDuration = 0f;
				m_greenoutFactor = 0f;
				m_lastNauseaTime = null;
				m_lastMoanTime = null;
				m_poisonIntensity = 0f;
				m_firstVomitQueued = false;
				m_firstVomitTimer = -1f;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>();
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>();
			m_componentHealth = Entity.FindComponent<ComponentHealth>();

			m_poisonResistance = valuesDictionary.GetValue<float>("PoisonResistance");
			m_durationOfPoison = valuesDictionary.GetValue<float>("DurationOfPoison");
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("PoisonResistance", m_poisonResistance);
			valuesDictionary.SetValue<float>("DurationOfPoison", m_durationOfPoison);
		}
	}
}
