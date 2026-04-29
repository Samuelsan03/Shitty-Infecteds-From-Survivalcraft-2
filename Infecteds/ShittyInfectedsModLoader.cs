using System;
using System.Collections.Generic;
using Game;
using Engine;

public class ShittyInfectedsModLoader : ModLoader
{
	public static bool HerdAttackOnPlayerHitEnabled = true;
	public static bool HerdAttackOnPlayerInjuryCreativeEnabled = true;

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

	// Resto de métodos: CalculateCreatureInjuryAmount, OnMinerHit, MenuPlayMusic (sin cambios)
	// ...
	// (los incluyo completos para que todo quede claro)

	public override void CalculateCreatureInjuryAmount(Injury injury)
	{
		if (!HerdAttackOnPlayerInjuryCreativeEnabled) return;
		if (injury == null || injury.ComponentHealth == null) return;
		if (!(injury.ComponentHealth.m_componentCreature is ComponentPlayer)) return;

		SubsystemGameInfo gameInfo = injury.ComponentHealth.m_subsystemGameInfo;
		if (gameInfo.WorldSettings.GameMode != GameMode.Creative) return;

		ComponentCreature attacker = injury.Attacker;
		if (attacker == null) return;

		SubsystemCreatureSpawn creatureSpawn = injury.ComponentHealth.Project.FindSubsystem<SubsystemCreatureSpawn>();
		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f) continue;
			ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herd == null || herd.HerdName != "player") continue;
			ComponentNewChaseBehavior chase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chase != null && chase.Target == null)
				chase.Attack(attacker, 20f, 30f, false);
		}
	}

	public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection,
		ref float damage, ref float hitProbability, ref float systemHitProbability, out bool skip)
	{
		skip = false;
		if (!HerdAttackOnPlayerHitEnabled) return;

		ComponentPlayer player = miner.ComponentPlayer;
		if (player == null) return;
		if (systemHitProbability < 1f) return;

		ComponentCreature targetCreature = targetBody.Entity.FindComponent<ComponentCreature>();
		if (targetCreature == null) return;

		SubsystemCreatureSpawn creatureSpawn = miner.Project.FindSubsystem<SubsystemCreatureSpawn>();
		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f) continue;
			ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herdBehavior == null || herdBehavior.HerdName != "player") continue;
			ComponentNewChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior != null && chaseBehavior.Target == null)
				chaseBehavior.Attack(targetCreature, 20f, 30f, false);
		}
	}

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

		// 2. Título
		StackPanelWidget topArea = mainMenuScreen.Children.Find<StackPanelWidget>("TopArea", true);
		if (topArea != null)
		{
			topArea.Children.Add(new LabelWidget
			{
				Text = "Shitty Infecteds v1.0",
				Color = new Color(0, 255, 94),
				HorizontalAlignment = WidgetAlignment.Center,
				FontScale = 0.5f,
				DropShadow = true,
				Margin = new Vector2(0f, 0f)
			});
		}

		// 3. Botón de configuración (ahora usa ZombiConfigButton)
		if (rightBottomBar != null)
		{
			var configButton = new ZombiConfigButton
			{
				Size = new Vector2(60f, 60f),
				Name = "ZombiConfigButton"
			};

			configButton.Children.Add(new RectangleWidget
			{
				Size = new Vector2(28f, 28f),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/zombi configurador"),
				FillColor = Color.White,
				OutlineColor = new Color(0, 0, 0, 0)
			});

			rightBottomBar.Children.Insert(0, configButton);
		}

		// 4. Enlace TikTok
		StackPanelWidget bottomInfos = mainMenuScreen.Children.Find<StackPanelWidget>("BottomInfos", true);
		if (bottomInfos != null)
		{
			var tiktokRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 4f)
			};
			tiktokRow.Children.Add(new LinkWidget
			{
				Text = "Tiktok: @athormi",
				Url = "https://www.tiktok.com/@athormi",
				Color = Color.White,
				FontScale = 0.7f,
				DropShadow = true
			});
			bottomInfos.Children.Insert(0, tiktokRow);
		}
	}
}
