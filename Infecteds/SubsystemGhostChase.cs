using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemGhostChase : Subsystem, IUpdateable
	{
		private SubsystemBodies m_subsystemBodies;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemTime m_subsystemTime;

		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private double m_nextUpdateTime;

		// Último resultado de la detección (se recalcula cada 0.5s, pero la música se gestiona cada frame)
		private bool m_isChasing;

		// NUEVO: cazador y presa actuales, para detectar cambio de objetivo.
		private Entity m_lastChasedEntity;
		private Entity m_lastChasedTarget;

		public const string MusicPath = "Music/ChaseTheme/Hotel Insanity Chase Theme";

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
		}

		public void Update(float dt)
		{
			// Si la opción está desactivada en el menú, impedir que suene
			if (!ShittyInfectedsSettings.EnableGhostChaseMusic)
			{
				m_isChasing = false;
				m_lastChasedEntity = null;
				m_lastChasedTarget = null;
				InfectedsMusicManager.Update(false, dt, MusicPath, InfectedsMusicManager.MusicType.Chase);
				return;
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_nextUpdateTime = m_subsystemTime.GameTime + 0.5;

				Entity newEntity, newTarget;
				bool newIsChasing = DetectChase(out newEntity, out newTarget);

				// NUEVO: si seguimos persiguiendo pero cambió el cazador o la presa
				// (nueva víctima, la presa lo provocó, etc.), hacemos un fade suave
				// para que la música arranque limpia para la nueva presa.
				// Esto NO se aplica al inicio de la persecución (allí Play es directo).
				if (m_isChasing && newIsChasing &&
					(newEntity != m_lastChasedEntity || newTarget != m_lastChasedTarget))
				{
					InfectedsMusicManager.FadeOut(
						InfectedsMusicManager.MusicType.Chase,
						InfectedsMusicManager.ChaseFadeOutDuration);
				}

				m_isChasing = newIsChasing;
				m_lastChasedEntity = newEntity;
				m_lastChasedTarget = newTarget;
			}

			InfectedsMusicManager.Update(m_isChasing, dt, MusicPath, InfectedsMusicManager.MusicType.Chase);
		}

		// Ahora además reporta quién persigue y a quién, para detectar cambio de presa.
		private bool DetectChase(out Entity chasedEntity, out Entity chasedTarget)
		{
			chasedEntity = null;
			chasedTarget = null;

			if (m_subsystemPlayers.ComponentPlayers.Count == 0)
			{
				return false;
			}

			ComponentBody playerBody = m_subsystemPlayers.ComponentPlayers[0].ComponentBody;
			m_componentBodies.Clear();

			m_subsystemBodies.FindBodiesAroundPoint(
				new Vector2(playerBody.Position.X, playerBody.Position.Z), 60f, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody body = m_componentBodies.Array[i];
				if (body.Entity.ValuesDictionary.DatabaseObject.Name == "GhostNormal")
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					ComponentZombieChaseBehavior chaseBehavior = body.Entity.FindComponent<ComponentZombieChaseBehavior>();

					if (creature != null && health != null && health.Health > 0f && chaseBehavior != null)
					{
						if (chaseBehavior.IsActive && chaseBehavior.Target != null && m_subsystemPlayers.IsPlayer(chaseBehavior.Target.Entity))
						{
							float distance = Vector3.Distance(playerBody.Position, creature.ComponentBody.Position);
							if (distance <= 50f)
							{
								chasedEntity = body.Entity;
								chasedTarget = chaseBehavior.Target.Entity;
								return true;
							}
						}
					}
				}
			}
			return false;
		}

		public override void Dispose()
		{
			InfectedsMusicManager.Stop(InfectedsMusicManager.MusicType.Chase);
			base.Dispose();
		}
	}
}
