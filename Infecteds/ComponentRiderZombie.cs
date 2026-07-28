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
			ComponentMountZombie mountZombie = componentMount as ComponentMountZombie;

			if (mountZombie != null && !mountZombie.CanRiderBeMounted)
			{
				// ============================================
				// SOLO bloquear si es un jugador
				// Las criaturas/zombis IA SI pueden montarse
				// ============================================
				ComponentPlayer player = this.ComponentCreature.Entity.FindComponent<ComponentPlayer>();

				if (player != null)
				{
					if (player.ComponentGui != null)
					{
						player.ComponentGui.DisplaySmallMessage(LanguageControl.Get("ComponentRiderZombie", 1), new Color(0, 153, 76), true, true);
					}
					return;
				}
				// No es jugador → continuar con el montaje normalmente
			}

			base.StartMounting(componentMount);
		}
	}
}
