using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBossChaseMusic : Subsystem, IUpdateable
	{
		private SubsystemBodies m_subsystemBodies;
		private SubsystemPlayers m_subsystemPlayers;

		public const string MusicPath = "Music/ChaseTheme/Tank Theme";
		public const float MusicRadius = 50f;

		// NUEVO: mismo seguimiento que en SubsystemGhostChase.
		private Entity m_lastChasedEntity;
		private Entity m_lastChasedTarget;
		private bool m_wasChasing;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			InfectedsMusicManager.Initialize();
		}

		public override void Dispose()
		{
			InfectedsMusicManager.Stop(InfectedsMusicManager.MusicType.BossChase);
			base.Dispose();
		}

		public void Update(float dt)
		{
			if (!ShittyInfectedsSettings.EnableBossChaseMusic)
			{
				m_wasChasing = false;
				m_lastChasedEntity = null;
				m_lastChasedTarget = null;
				InfectedsMusicManager.Update(false, dt, MusicPath, InfectedsMusicManager.MusicType.BossChase);
				return;
			}

			Entity newEntity, newTarget;
			bool isChasing = CheckIfAnyBruteIsChasing(out newEntity, out newTarget);

			// NUEVO: si seguía habiendo persecución pero cambió el brute o la presa,
			// disparamos fade-out. Al terminar la persecución, el propio manager
			// (Update con isChasing=false) se encarga del fade.
			if (m_wasChasing && isChasing &&
				(newEntity != m_lastChasedEntity || newTarget != m_lastChasedTarget))
			{
				InfectedsMusicManager.FadeOut(
					InfectedsMusicManager.MusicType.BossChase,
					InfectedsMusicManager.ChaseFadeOutDuration);
			}

			m_wasChasing = isChasing;
			m_lastChasedEntity = newEntity;
			m_lastChasedTarget = newTarget;

			InfectedsMusicManager.Update(isChasing, dt, MusicPath, InfectedsMusicManager.MusicType.BossChase);
		}

		// Comprueba si el nombre de la entidad es uno de los brutes con música de jefe.
		// Sin diccionarios: solo comparaciones normales con ||.
		private static bool IsBossBruteName(string entityName)
		{
			if (string.IsNullOrEmpty(entityName)) return false;

			return entityName == "InfectedBrute"
				|| entityName == "InfectedBruteArsonist"
				|| entityName == "InfectedBruteFrozen"
				|| entityName == "InfectedBrutePoisonous";
		}

		private bool CheckIfAnyBruteIsChasing(out Entity chasedEntity, out Entity chasedTarget)
		{
			chasedEntity = null;
			chasedTarget = null;

			float radiusSquared = MusicRadius * MusicRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body?.Entity == null) continue;

				string entityName = body.Entity.ValuesDictionary?.DatabaseObject?.Name;

				// ANTES: if (entityName != "InfectedBrute" | ...) continue;  → mal, siempre true
				// AHORA: solo seguimos con los brutes con música de jefe.
				if (!IsBossBruteName(entityName)) continue;

				ComponentZombieChaseBehavior chaseBehavior = body.Entity.FindComponent<ComponentZombieChaseBehavior>();
				ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();

				if (chaseBehavior == null || health == null) continue;
				if (!chaseBehavior.IsActive || chaseBehavior.Target == null || health.Health <= 0f) continue;

				foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
				{
					if (player?.ComponentBody == null) continue;

					float distanceSquared = Vector3.DistanceSquared(body.Position, player.ComponentBody.Position);
					if (distanceSquared <= radiusSquared)
					{
						chasedEntity = body.Entity;
						chasedTarget = chaseBehavior.Target.Entity;
						return true;
					}
				}
			}
			return false;
		}
	}
}
