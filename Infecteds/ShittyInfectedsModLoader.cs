using System;
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
	}

	public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float damage, ref float hitProbability, ref float systemHitProbability, out bool skip)
	{
		skip = false;

		// Verificar que el que golpea es un jugador
		ComponentPlayer player = miner.ComponentPlayer;
		if (player == null)
			return;

		// Verificar que el objetivo es una criatura válida
		ComponentCreature targetCreature = targetBody.Entity.FindComponent<ComponentCreature>();
		if (targetCreature == null)
			return;

		// Buscar criaturas aliadas del jugador (manada "player")
		SubsystemCreatureSpawn creatureSpawn = miner.Project.FindSubsystem<SubsystemCreatureSpawn>();

		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f)
				continue;

			// Verificar que pertenece a la manada "player"
			ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior == null || herdBehavior.HerdName != "player")
				continue;

			// Ordenar atacar a la criatura golpeada por el jugador
			ComponentNewChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior != null && chaseBehavior.Target == null)
			{
				chaseBehavior.Attack(targetCreature, 20f, 30f, false);
			}
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
			logo.Size = new Vector2(320f, 136f); // o el tamaño deseado, el usuario puso 320x136 en su último código
		}

		// 2. Agregar título debajo de la etiqueta de la API en TopArea
		StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
		if (topArea != null)
		{
			// Espacio pequeño (como el que hay entre logo y versión)
			CanvasWidget spacer = new CanvasWidget
			{
				Size = new Vector2(0f, 5f)
			};
			topArea.Children.Add(spacer);

			LabelWidget titleLabel = new LabelWidget
			{
				Text = "Shitty Infecteds v1.0",
				Color = new Color(0, 255, 94),
				HorizontalAlignment = WidgetAlignment.Center,
				FontScale = 0.5f, // Mismo tamaño que la etiqueta "Version"
				DropShadow = true,
				Margin = new Vector2(0f, 0f)
			};
			topArea.Children.Add(titleLabel);
		}

		// 3. Agregar enlace de TikTok encima del copyright
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
