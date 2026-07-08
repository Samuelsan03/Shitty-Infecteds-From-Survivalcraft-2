using System;
using System.Linq;
using System.Collections.Generic;
using Game;
using Engine;

public class ShittyInfectedsModLoader : ModLoader
{
	// Lista de rutas de música (sin extensión) dentro del mod
	private static readonly List<string> ListaMusica = new List<string>
	{
		"Music/Menu Music",
		"Music/Menu Music 2",
	};

	private Game.Random random = new Game.Random();

	public override void __ModInitialize()
	{
		ModsManager.RegisterHook("MenuPlayMusic", this);
		ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
		ModsManager.RegisterHook("OnMinerHit", this);
		ModsManager.RegisterHook("CalculateCreatureInjuryAmount", this);
	}

	public override void CalculateCreatureInjuryAmount(Injury injury)
	{
		if (injury == null || injury.ComponentHealth == null)
			return;

		// Obtener el atacante (quien causó la herida)
		ComponentCreature attacker = injury.Attacker;
		if (attacker == null)
			return;

		// Obtener la víctima
		ComponentCreature victim = injury.ComponentHealth.m_componentCreature;
		if (victim == null || victim == attacker)
			return;

		// Determinar quién es el enemigo que debe ser atacado
		ComponentCreature enemy = null;

		if (attacker is ComponentPlayer)
		{
			// El jugador atacó a una criatura, las aliadas deben atacar a la víctima
			enemy = victim;
		}
		else if (victim is ComponentPlayer)
		{
			// Una criatura atacó al jugador, las aliadas deben atacar al agresor
			enemy = attacker;
		}
		else
		{
			return; // No es una situación donde debamos intervenir
		}

		if (enemy == null)
			return;

		// CORREGIDO: Buscar UNA criatura aliada y usar CallNearbyCreaturesHelp
		// Esto es más eficiente y garantiza que todas reaccionen
		SubsystemCreatureSpawn creatureSpawn = injury.ComponentHealth.Project.FindSubsystem<SubsystemCreatureSpawn>();
		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f)
				continue;

			ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herd != null && herd.HerdName == "player")
			{
				// Usar CallNearbyCreaturesHelp que alerta a todos los cercanos
				herd.CallNearbyCreaturesHelp(enemy, 20f, 30f, false);
				break; // Solo necesitamos llamar una vez
			}
		}
	}

	public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float damage, ref float hitProbability, ref float systemHitProbability, out bool skip)
	{
		skip = false;

		// Verificar que el que golpea es un jugador
		ComponentPlayer player = miner.ComponentPlayer;
		if (player == null)
			return;

		// Si la probabilidad de golpe es 0 o menor, no hacemos nada
		if (hitProbability <= 0f)
			return;

		// Verificar que el objetivo es una criatura válida
		ComponentCreature targetCreature = targetBody.Entity.FindComponent<ComponentCreature>();
		if (targetCreature == null)
			return;

		// CORREGIDO: Forzar que el golpe SIEMPRE acierte cuando hay aliados disponibles
		// para garantizar que CalculateCreatureInjuryAmount se dispare
		SubsystemCreatureSpawn creatureSpawn = miner.Project.FindSubsystem<SubsystemCreatureSpawn>();
		bool hasAllies = false;

		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f)
				continue;

			ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior != null && herdBehavior.HerdName == "player")
			{
				hasAllies = true;
				break;
			}
		}

		// Si hay aliados disponibles, forzamos el acierto
		if (hasAllies)
		{
			hitProbability = 1f;
			systemHitProbability = 1f;
		}
	}

	// Música aleatoria (se mantiene)
	public override void MenuPlayMusic(out string contentMusicPath)
	{
		int index = random.Int(ListaMusica.Count);
		contentMusicPath = ListaMusica[index];
	}

	public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
	{
		// 1. Ajustar logo
		RectangleWidget logo = mainMenuScreen.Children.Find<RectangleWidget>("Logo", true);
		if (logo != null)
		{
			logo.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Logo");
			logo.Size = new Vector2(320f, 136f);
		}

		// 2. Agregar título debajo de la etiqueta de la API en TopArea
		StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
		if (topArea != null)
		{
			LabelWidget titleLabel = new LabelWidget
			{
				Text = "Shitty Infecteds v1.0",
				Color = new Color(0, 255, 94),
				HorizontalAlignment = WidgetAlignment.Center,
				FontScale = 0.5f,
				DropShadow = true,
				Margin = new Vector2(0f, 0f)
			};
			topArea.Children.Add(titleLabel);
		}

		// 3. Agregar botón cuadrado en la barra lateral derecha
		if (rightBottomBar != null)
		{
			BevelledButtonWidget configButton = new BevelledButtonWidget
			{
				Size = new Vector2(60f, 60f),
				Name = "ZombiConfigButton"
			};

			RectangleWidget icon = new RectangleWidget
			{
				Size = new Vector2(28f, 28f),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/zombi configurador"),
				FillColor = Color.White,
				OutlineColor = new Color(0, 0, 0, 0)
			};

			configButton.Children.Add(icon);
			rightBottomBar.Children.Insert(0, configButton);
		}

		// 4. Agregar enlace de TikTok encima del copyright
		StackPanelWidget bottomInfos = mainMenuScreen.Children.Find<StackPanelWidget>("BottomInfos", true);
		if (bottomInfos != null)
		{
			StackPanelWidget tiktokRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 4f)
			};

			LinkWidget tiktokLink = new LinkWidget
			{
				Text = "Tiktok: @athormi",
				Url = "https://www.tiktok.com/@athormi",
				Color = Color.White,
				FontScale = 0.7f,
				DropShadow = true
			};

			tiktokRow.Children.Add(tiktokLink);
			bottomInfos.Children.Insert(0, tiktokRow);
		}
	}
}
