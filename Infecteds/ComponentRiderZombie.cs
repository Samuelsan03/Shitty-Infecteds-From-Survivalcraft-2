using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRiderZombie : ComponentRider
	{
		public override void StartMounting(ComponentMount componentMount)
		{
			// Verificar si es una montura zombie
			ComponentMountZombie mountZombie = componentMount as ComponentMountZombie;

			// SOLO bloquear si:
			// 1. Es una montura zombie
			// 2. No permite ser montada
			// 3. El rider es un jugador
			if (mountZombie != null && !mountZombie.CanRiderBeMounted)
			{
				ComponentPlayer player = this.ComponentCreature.Entity.FindComponent<ComponentPlayer>();

				if (player != null)
				{
					// Mostrar mensaje y bloquear el montaje
					if (player.ComponentGui != null)
					{
						player.ComponentGui.DisplaySmallMessage(
							LanguageControl.Get("ComponentRiderZombie", 1),
							new Color(0, 153, 76),
							true,
							true
						);
					}
					return; // BLOQUEAR - No montar
				}
				// Si NO es jugador (IA), permitir montaje
			}

			// Para CUALQUIER otra montura (caballo normal, etc.) o zombie permitido,
			// llamar al comportamiento base NORMAL
			base.StartMounting(componentMount);
		}

		// NO SOBREESCRIBIR Update
		// NO SOBREESCRIBIR StartDismounting
		// NO SOBREESCRIBIR NADA MÁS
		// Todo lo demás hereda de ComponentRider sin cambios
	}
}
