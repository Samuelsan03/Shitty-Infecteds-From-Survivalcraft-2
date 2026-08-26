using System;
using System.Collections.Generic;
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

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
		}

		public void Update(float dt)
		{
			ChaseMusicManager.Update();

			// Si la opción está desactivada en el menú, impedir que suene y salir del método
			if (!ShittyInfectedsSettings.EnableGhostChaseMusic)
			{
				ChaseMusicManager.StopMusic();
				return;
			}

			if (m_subsystemTime.GameTime < m_nextUpdateTime) return;
			m_nextUpdateTime = m_subsystemTime.GameTime + 0.5;

			bool isChasing = false;

			if (m_subsystemPlayers.ComponentPlayers.Count == 0)
			{
				ChaseMusicManager.StopMusic();
				return;
			}

			ComponentBody playerBody = m_subsystemPlayers.ComponentPlayers[0].ComponentBody;

			m_componentBodies.Clear();

			// Buscamos en 60 bloques para tener un pequeño margen de detección antes del corte
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerBody.Position.X, playerBody.Position.Z), 60f, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				if (m_componentBodies.Array[i].Entity.ValuesDictionary.DatabaseObject.Name == "GhostNormal")
				{
					ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					ComponentHealth health = m_componentBodies.Array[i].Entity.FindComponent<ComponentHealth>();
					ComponentZombieChaseBehavior chaseBehavior = m_componentBodies.Array[i].Entity.FindComponent<ComponentZombieChaseBehavior>();

					if (creature != null && health != null && health.Health > 0f && chaseBehavior != null)
					{
						if (chaseBehavior.IsActive && chaseBehavior.Target != null && m_subsystemPlayers.IsPlayer(chaseBehavior.Target.Entity))
						{
							// Cálculo de distancia exacto
							float distance = Vector3.Distance(playerBody.Position, creature.ComponentBody.Position);

							// CONDICIÓN ESTRICTA: A 50 BLOQUES O MENOS
							if (distance <= 50f)
							{
								isChasing = true;
								break;
							}
						}
					}
				}
			}

			// Activa o corta la música de golpe
			if (isChasing)
			{
				ChaseMusicManager.PlayChaseMusic();
			}
			else
			{
				ChaseMusicManager.StopMusic();
			}
		}

		public override void Dispose()
		{
			ChaseMusicManager.StopMusic();
			base.Dispose();
		}
	}
}