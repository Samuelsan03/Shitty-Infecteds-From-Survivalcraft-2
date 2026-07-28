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
			// Verificamos si la montura es un zombi y si tiene bloqueado el montaje
			ComponentMountZombie mountZombie = componentMount as ComponentMountZombie;

			if (mountZombie != null && !mountZombie.CanRiderBeMounted)
			{
				// Buscamos al jugador para mostrarle el mensaje por el GUI
				ComponentPlayer player = this.ComponentCreature.Entity.FindComponent<ComponentPlayer>();

				if (player != null && player.ComponentGui != null)
				{
					// Usamos LanguageControl con el índice 1
					player.ComponentGui.DisplaySmallMessage(LanguageControl.Get("ComponentRiderZombie", 1), new Color(0, 153, 76), true, true);
				}

				// Bloqueamos la acción de montarse haciendo un return sin llamar a la base
				return;
			}

			// Si no es un zombi bloqueado, se monta normalmente
			base.StartMounting(componentMount);
		}
	}
}
