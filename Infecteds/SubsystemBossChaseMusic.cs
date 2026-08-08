using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBossChaseMusic : Subsystem, IUpdateable
	{
		private SubsystemBodies m_subsystemBodies;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			// Inicializamos el manager estático
			BossChaseMusicManager.Initialize();
		}

		public override void Dispose()
		{
			// ESTO ES LO CLAVE: Cuando sales del mundo al menú principal, 
			// o mueres y el mundo se descarga, esto se ejecuta y corta la música instantáneamente.
			BossChaseMusicManager.Stop();

			base.Dispose();
		}

		public void Update(float dt)
		{
			// Verificamos si el jefe te está persiguiendo
			bool isChasing = CheckIfAnyBruteIsChasing();

			// Le pasamos el estado al Manager estático
			BossChaseMusicManager.Update(isChasing, dt);
		}

		private bool CheckIfAnyBruteIsChasing()
		{
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body?.Entity == null) continue;

				string entityName = body.Entity.ValuesDictionary?.DatabaseObject?.Name;
				if (entityName != "InfectedBrute") continue;

				ComponentZombieChaseBehavior chaseBehavior = body.Entity.FindComponent<ComponentZombieChaseBehavior>();
				ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();

				if (chaseBehavior != null && health != null)
				{
					if (chaseBehavior.IsActive && chaseBehavior.Target != null && health.Health > 0f)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
