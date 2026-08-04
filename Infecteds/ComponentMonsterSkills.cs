using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200017B RID: 379
	public class ComponentMonsterSkills : Component, IUpdateable
	{
		// Token: 0x17000145 RID: 325
		// (get) Token: 0x06000BB9 RID: 3001
		// (set) Token: 0x06000BBA RID: 3002
		/// <summary>
		/// ¿Puede vomitar fuego?
		/// </summary>
		public bool CanVomitFire { get; set; }

		// Token: 0x17000146 RID: 326
		// (get) Token: 0x06000BBB RID: 3003
		// (set) Token: 0x06000BBC RID: 3004
		/// <summary>
		/// Tiempo para volver a vomitar (cooldown en segundos)
		/// </summary>
		public float TimeToVomitAgain { get; set; }

		// Token: 0x17000147 RID: 327
		// (get) Token: 0x06000BBD RID: 3005
		// (set) Token: 0x06000BBE RID: 3006
		/// <summary>
		/// Distancia para vomitar: X = distancia mínima (no vomita si está más cerca), Y = distancia máxima (cancela si se aleja)
		/// </summary>
		public Vector2 DistanceToVomit { get; set; }

		// Token: 0x17000148 RID: 328
		// (get) Token: 0x06000BBF RID: 3007
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x17000149 RID: 329
		// (get) Token: 0x06000BC0 RID: 3008
		public bool IsVomiting
		{
			get
			{
				return m_isVomiting;
			}
		}

		// Token: 0x1700014A RID: 330
		// (get) Token: 0x06000BC1 RID: 3009
		public float VomitCooldownRemaining
		{
			get
			{
				return MathUtils.Max(m_vomitCooldownTimer, 0f);
			}
		}

		// Token: 0x1700014B RID: 331
		// (get) Token: 0x06000BC2 RID: 3010
		public float VomitDurationRemaining
		{
			get
			{
				return MathUtils.Max(m_vomitDurationTimer, 0f);
			}
		}

		/// <summary>
		/// Duración del vómito en segundos (no se carga del diccionario)
		/// </summary>
		public float DurationOfVomiting { get; set; } = 10f;

		/// <summary>
		/// Obtiene el objetivo actual del comportamiento de persecución
		/// </summary>
		public ComponentCreature GetChaseTarget()
		{
			if (m_chaseBehavior == null) return null;

			if (m_chaseBehaviorType == typeof(ComponentChaseBehavior))
				return ((ComponentChaseBehavior)m_chaseBehavior).Target;
			if (m_chaseBehaviorType == typeof(ComponentNewChaseBehavior))
				return ((ComponentNewChaseBehavior)m_chaseBehavior).Target;
			if (m_chaseBehaviorType == typeof(ComponentZombieChaseBehavior))
				return ((ComponentZombieChaseBehavior)m_chaseBehavior).Target;

			return null;
		}

		/// <summary>
		/// Verifica si algún comportamiento de persecución está activo
		/// </summary>
		public bool IsChasingActive()
		{
			if (m_chaseBehavior == null) return false;

			if (m_chaseBehaviorType == typeof(ComponentChaseBehavior))
				return ((ComponentChaseBehavior)m_chaseBehavior).IsActive;
			if (m_chaseBehaviorType == typeof(ComponentNewChaseBehavior))
				return ((ComponentNewChaseBehavior)m_chaseBehavior).IsActive;
			if (m_chaseBehaviorType == typeof(ComponentZombieChaseBehavior))
				return ((ComponentZombieChaseBehavior)m_chaseBehavior).IsActive;

			return false;
		}

		/// <summary>
		/// Verifica si el objetivo está vivo (salud > 0)
		/// </summary>
		public bool IsTargetAlive(ComponentCreature target)
		{
			if (target == null) return false;

			ComponentHealth targetHealth = target.Entity.FindComponent<ComponentHealth>(false);
			if (targetHealth == null) return false;

			return targetHealth.Health > 0f;
		}

		/// <summary>
		/// Fuerza el inicio del vómito (para uso externo si es necesario)
		/// </summary>
		public void ForceStartVomiting()
		{
			if (!CanVomitFire) return;
			StartVomiting();
		}

		/// <summary>
		/// Fuerza la detención del vómito
		/// </summary>
		public void ForceStopVomiting()
		{
			StopVomiting();
		}

		// Token: 0x06000BC3 RID: 3011
		public void Update(float dt)
		{
			// No vomitar si está muerto o deshabilitado
			if (!CanVomitFire || m_componentHealth.Health <= 0f)
			{
				if (m_isVomiting) StopVomiting();
				return;
			}

			// Actualizar cooldown solo si NO está vomitando
			if (!m_isVomiting && m_vomitCooldownTimer > 0f)
			{
				m_vomitCooldownTimer -= dt;
			}

			ComponentCreature target = GetChaseTarget();
			float distanceToTarget = float.MaxValue;

			// CORRECCIÓN: Verificar si el objetivo está muerto
			bool targetIsDead = target != null && !IsTargetAlive(target);

			// Si el objetivo está muerto, tratarlo como si fuera null
			if (targetIsDead)
			{
				target = null;
			}

			if (target != null && target.ComponentBody != null)
			{
				distanceToTarget = Vector3.Distance(
					m_componentCreature.ComponentBody.Position,
					target.ComponentBody.Position);
			}

			if (m_isVomiting)
			{
				// Reducir duración del vómito
				m_vomitDurationTimer -= dt;

				// Verificar si debemos detener el vómito
				bool shouldStop = false;

				if (m_vomitDurationTimer <= 0f)
				{
					// Se acabó la duración del vómito - iniciar cooldown
					shouldStop = true;
				}
				else if (target == null || !IsChasingActive())
				{
					// No hay objetivo o no está persiguiendo
					// CORRECCIÓN: Esto ahora incluye cuando el objetivo murió
					shouldStop = true;
				}
				else if (distanceToTarget < DistanceToVomit.X)
				{
					// Demasiado cerca - no vomitar
					shouldStop = true;
				}
				else if (distanceToTarget > DistanceToVomit.Y + 2f)
				{
					// Demasiado lejos - cancelar (con tolerancia de 2 bloques)
					shouldStop = true;
				}

				if (shouldStop)
				{
					StopVomiting();
				}
				else
				{
					// Actualizar posición y dirección recta hacia el objetivo
					UpdateVomitTransform(target);
				}
			}
			else
			{
				// Verificar si debemos iniciar el vómito
				// CORRECCIÓN: Ahora también verifica que el objetivo no esté muerto
				if (m_vomitCooldownTimer <= 0f && target != null && !targetIsDead && IsChasingActive())
				{
					// Solo vomitar si está en el rango de distancia correcto
					if (distanceToTarget >= DistanceToVomit.X && distanceToTarget <= DistanceToVomit.Y)
					{
						// Pequeña probabilidad aleatoria por frame para que no sea instantáneo
						if (m_random.Float(0f, 1f) < 0.15f * dt)
						{
							StartVomiting();
						}
					}
				}
			}
		}

		// Token: 0x06000BC4 RID: 3012
		private void StartVomiting()
		{
			if (!CanVomitFire || m_isVomiting) return;

			ComponentCreature target = GetChaseTarget();

			// CORRECCIÓN: Verificar que el objetivo existe y está vivo antes de iniciar
			if (target == null || !IsTargetAlive(target)) return;

			m_isVomiting = true;
			m_vomitDurationTimer = DurationOfVomiting;

			// Crear o reutilizar el sistema de partículas
			if (m_vomitParticleSystem == null || m_vomitParticleSystem.IsStopped)
			{
				m_vomitParticleSystem = new FireVomitParticleSystem(m_subsystemTerrain, m_subsystemBodies, m_subsystemTime);
				m_vomitParticleSystem.OwnerBody = m_componentCreature.ComponentBody;
				m_vomitParticleSystem.Attacker = m_componentCreature;
				m_subsystemParticles.AddParticleSystem(m_vomitParticleSystem, false);
			}
			else
			{
				m_vomitParticleSystem.IsStopped = false;
			}

			// Actualizar transform inmediatamente desde la cabeza
			UpdateVomitTransform(target);
		}

		// Token: 0x06000BC5 RID: 3013
		private void StopVomiting()
		{
			if (!m_isVomiting) return;

			m_isVomiting = false;

			// Iniciar cooldown DESPUÉS de terminar de vomitar
			m_vomitCooldownTimer = TimeToVomitAgain;

			if (m_vomitParticleSystem != null)
			{
				m_vomitParticleSystem.IsStopped = true;
				m_vomitParticleSystem = null;
			}
		}

		// Token: 0x06000BC6 RID: 3014
		private void UpdateVomitTransform(ComponentCreature target)
		{
			if (m_vomitParticleSystem == null || m_componentCreatureModel == null) return;

			// CORRECCIÓN: Verificar que el objetivo sigue vivo antes de actualizar dirección
			if (!IsTargetAlive(target)) return;

			// Respetando el cálculo de la posición original de ComponentSickness
			Vector3 upVector = m_componentCreatureModel.EyeRotation.GetUpVector();
			Vector3 forwardVector = m_componentCreatureModel.EyeRotation.GetForwardVector();

			// Posición de salida desde la cabeza de la criatura
			Vector3 mouthPos = m_componentCreatureModel.EyePosition - 0.08f * upVector + 0.3f * forwardVector;

			// CORRECCIÓN: Calcular dirección RECTA hacia el centro del objetivo
			// para que el vómito no se vaya a direcciones erróneas al girar el modelo
			Vector3 targetCenter = target.ComponentBody.BoundingBox.Center();
			Vector3 toTarget = targetCenter - mouthPos;
			float distance = toTarget.Length();

			Vector3 direction;
			if (distance > 0.01f)
			{
				direction = Vector3.Normalize(toTarget);
			}
			else
			{
				// Fallback al comportamiento original si está pegado al objetivo
				direction = Vector3.Normalize(forwardVector + 0.5f * upVector);
			}

			m_vomitParticleSystem.Position = mouthPos;
			m_vomitParticleSystem.Direction = direction;
		}

		// Token: 0x06000BC7 RID: 3015
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);

			// Cargar configuración del diccionario
			CanVomitFire = valuesDictionary.GetValue<bool>("CanVomitFire", false);
			TimeToVomitAgain = valuesDictionary.GetValue<float>("TimeToVomitAgain", 5f);
			DistanceToVomit = valuesDictionary.GetValue<Vector2>("DistanceToVomit", new Vector2(3f, 10f));

			// DurationOfVomiting NO se carga del diccionario, usa el valor por defecto (10f)

			// Buscar CUALQUIER comportamiento de persecución que tenga la criatura
			// Prioridad: ComponentChaseBehavior > ComponentNewChaseBehavior > ComponentZombieChaseBehavior
			ComponentChaseBehavior chase1 = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			ComponentNewChaseBehavior chase2 = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			ComponentZombieChaseBehavior chase3 = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			if (chase1 != null)
			{
				m_chaseBehavior = chase1;
				m_chaseBehaviorType = typeof(ComponentChaseBehavior);
			}
			else if (chase2 != null)
			{
				m_chaseBehavior = chase2;
				m_chaseBehaviorType = typeof(ComponentNewChaseBehavior);
			}
			else if (chase3 != null)
			{
				m_chaseBehavior = chase3;
				m_chaseBehaviorType = typeof(ComponentZombieChaseBehavior);
			}

			// Iniciar con cooldown parcial para que el primer vómito pueda ocurrir relativamente pronto
			m_vomitCooldownTimer = TimeToVomitAgain * 0.3f;
		}

		// Token: 0x06000BC8 RID: 3016
		public override void OnEntityRemoved()
		{
			if (m_isVomiting)
			{
				StopVomiting();
			}
		}

		// Token: 0x04000688 RID: 1672
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000689 RID: 1673
		public SubsystemBodies m_subsystemBodies;

		// Token: 0x0400068A RID: 1674
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x0400068B RID: 1675
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400068C RID: 1676
		public ComponentCreature m_componentCreature;

		// Token: 0x0400068D RID: 1677
		public ComponentCreatureModel m_componentCreatureModel;

		// Token: 0x0400068E RID: 1678
		public ComponentHealth m_componentHealth;

		// Token: 0x0400068F RID: 1679
		public float m_vomitCooldownTimer;

		// Token: 0x04000690 RID: 1680
		public float m_vomitDurationTimer;

		// Token: 0x04000691 RID: 1681
		public FireVomitParticleSystem m_vomitParticleSystem;

		// Token: 0x04000692 RID: 1682
		public bool m_isVomiting;

		// Token: 0x04000693 RID: 1683
		public object m_chaseBehavior;

		// Token: 0x04000694 RID: 1684
		public Type m_chaseBehaviorType;

		// Token: 0x04000695 RID: 1685
		public Random m_random = new Random();
	}
}
