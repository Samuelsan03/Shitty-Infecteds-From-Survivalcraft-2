using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento que añade un efecto de empuje al golpear a una víctima,
	/// usando dos parámetros simples: probabilidad de empuje y fuerza de empuje.
	/// Soporta los tres comportamientos de persecución: ComponentChaseBehavior,
	/// ComponentNewChaseBehavior y ComponentZombieChaseBehavior.
	/// </summary>
	public class Gravity : ComponentBehavior, IUpdateable
	{
		// Propiedades requeridas por ComponentBehavior e IUpdateable
		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Máquina de estados
		private StateMachine m_stateMachine = new StateMachine();

		// Subsystems
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;

		// Componentes propios
		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentBody m_componentBody;

		// Referencia al comportamiento de persecución
		private ComponentBehavior m_chaseBehavior;

		// LOS DOS PARÁMETROS SIMPLES QUE PEDISTE
		private float m_pushProbability = 0f;
		private float m_pushForce = 0f;

		// Variable para evitar aplicar empuje múltiples veces en el mismo golpe
		private bool m_pushAppliedThisHit = false;

		// ---------------------------------------------------------------------
		// Carga de la plantilla
		// ---------------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Obtener subsystems
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			// Obtener componentes propios
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);

			// Buscar el comportamiento de persecución que esté presente (original, new o zombie)
			m_chaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			if (m_chaseBehavior == null)
				m_chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			if (m_chaseBehavior == null)
				m_chaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			// Cargar los dos parámetros simples
			m_pushProbability = valuesDictionary.GetValue<float>("PushProbability", 0.1f);
			m_pushForce = valuesDictionary.GetValue<float>("PushForce", 10f);

			// Configurar la máquina de estados con dos estados: Idle y Chasing
			m_stateMachine.AddState("Idle",
				enter: null,
				update: () =>
				{
					if (IsChaseActive())
						m_stateMachine.TransitionTo("Chasing");
				},
				leave: null
			);

			m_stateMachine.AddState("Chasing",
				enter: () => { m_pushAppliedThisHit = false; },
				update: () =>
				{
					if (!IsChaseActive())
					{
						m_stateMachine.TransitionTo("Idle");
						return;
					}

					if (m_componentCreatureModel.IsAttackHitMoment && !m_pushAppliedThisHit)
					{
						ComponentCreature target = GetTargetFromChase();
						if (target != null && target.ComponentHealth.Health > 0f)
						{
							ApplyPush(target);
							m_pushAppliedThisHit = true;
						}
					}

					if (!m_componentCreatureModel.IsAttackHitMoment)
						m_pushAppliedThisHit = false;
				},
				leave: null
			);

			m_stateMachine.TransitionTo("Idle");
		}

		// ---------------------------------------------------------------------
		// Método de actualización (IUpdateable)
		// ---------------------------------------------------------------------
		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		// ---------------------------------------------------------------------
		// Métodos auxiliares
		// ---------------------------------------------------------------------

		private bool IsChaseActive()
		{
			if (m_chaseBehavior == null) return false;

			if (m_chaseBehavior is ComponentChaseBehavior chase)
				return chase.IsActive && chase.Target != null;
			if (m_chaseBehavior is ComponentNewChaseBehavior newChase)
				return newChase.IsActive && newChase.Target != null;
			if (m_chaseBehavior is ComponentZombieChaseBehavior zombieChase)
				return zombieChase.IsActive && zombieChase.Target != null;

			return false;
		}

		private ComponentCreature GetTargetFromChase()
		{
			if (m_chaseBehavior == null) return null;

			if (m_chaseBehavior is ComponentChaseBehavior chase)
				return chase.Target;
			if (m_chaseBehavior is ComponentNewChaseBehavior newChase)
				return newChase.Target;
			if (m_chaseBehavior is ComponentZombieChaseBehavior zombieChase)
				return zombieChase.Target;

			return null;
		}

		private void ApplyPush(ComponentCreature target)
		{
			if (m_pushProbability <= 0f || m_pushForce <= 0f) return;

			Random random = new Random();
			if (random.Float(0f, 1f) > m_pushProbability) return;

			Vector3 attackerPos = m_componentBody.Position;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 direction = targetPos - attackerPos;
			float horizontalDist = new Vector2(direction.X, direction.Z).Length();

			if (horizontalDist < 0.01f)
			{
				direction = m_componentBody.Matrix.Forward;
				direction.Y = 0f;
				if (direction.LengthSquared() < 0.01f)
					direction = Vector3.UnitZ;
			}
			else
			{
				direction = new Vector3(direction.X, 0f, direction.Z);
			}

			direction = Vector3.Normalize(direction);
			Vector3 pushDir = direction + Vector3.UnitY * 0.5f;
			pushDir = Vector3.Normalize(pushDir);

			float impulseMagnitude = m_pushForce * 1e7f;
			Vector3 impulse = pushDir * impulseMagnitude;

			target.ComponentBody.ApplyImpulse(impulse);
		}
	}
}
