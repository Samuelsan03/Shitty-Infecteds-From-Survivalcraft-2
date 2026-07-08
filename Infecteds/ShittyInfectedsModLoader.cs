using System;
using System.Linq;
using System.Collections.Generic;
using Game;
using Engine;

public class ShittyInfectedsModLoader : ModLoader
{
	private static readonly List<string> ListaMusica = new List<string>
	{
		"Music/Menu Music",
		"Music/Menu Music 2",
	};

	private Game.Random random = new Game.Random();

	private static bool m_greenNightDialogShown = false;
	private static ComponentPlayer m_pendingPlayer = null;

	public override void __ModInitialize()
	{
		ModsManager.RegisterHook("MenuPlayMusic", this);
		ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
		ModsManager.RegisterHook("OnMinerHit", this);
		ModsManager.RegisterHook("CalculateCreatureInjuryAmount", this);
		ModsManager.RegisterHook("OnWidgetConstruct", this);
		ModsManager.RegisterHook("OnPlayerSpawned", this);
		ModsManager.RegisterHook("ChangeSkyColor", this);
	}

	public override bool OnPlayerSpawned(PlayerData.SpawnMode spawnMode, ComponentPlayer player, Vector3 position)
	{
		if ((spawnMode == PlayerData.SpawnMode.InitialIntro || spawnMode == PlayerData.SpawnMode.InitialNoIntro) && !m_greenNightDialogShown)
		{
			m_greenNightDialogShown = true;
			m_pendingPlayer = player;

			if (player?.GuiWidget != null)
			{
				DialogsManager.ShowDialog(player.GuiWidget, new GreenNightConfigDialog(player));
			}
		}
		return false;
	}

	public override void OnWidgetConstruct(ref Widget widget)
	{
		if (widget is PanoramaWidget)
		{
			widget = new ShittyInfectedsPanoramaWidget();
		}
	}

	public override void CalculateCreatureInjuryAmount(Injury injury)
	{
		if (injury == null || injury.ComponentHealth == null)
			return;

		ComponentCreature attacker = injury.Attacker;
		if (attacker == null)
			return;

		ComponentCreature victim = injury.ComponentHealth.m_componentCreature;
		if (victim == null || victim == attacker)
			return;

		ComponentCreature enemy = null;

		if (attacker is ComponentPlayer)
		{
			enemy = victim;
		}
		else if (victim is ComponentPlayer)
		{
			enemy = attacker;
		}
		else
		{
			return;
		}

		if (enemy == null)
			return;

		SubsystemCreatureSpawn creatureSpawn = injury.ComponentHealth.Project.FindSubsystem<SubsystemCreatureSpawn>();
		foreach (ComponentCreature creature in creatureSpawn.Creatures)
		{
			if (creature.ComponentHealth.Health <= 0f)
				continue;

			ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (herd != null && herd.HerdName == "player")
			{
				herd.CallNearbyCreaturesHelp(enemy, 20f, 30f, false);
				break;
			}
		}
	}

	public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float damage, ref float hitProbability, ref float systemHitProbability, out bool skip)
	{
		skip = false;

		ComponentPlayer player = miner.ComponentPlayer;
		if (player == null)
			return;

		if (hitProbability <= 0f)
			return;

		ComponentCreature targetCreature = targetBody.Entity.FindComponent<ComponentCreature>();
		if (targetCreature == null)
			return;

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

		if (hasAllies)
		{
			hitProbability = 1f;
			systemHitProbability = 1f;
		}
	}

	public override void MenuPlayMusic(out string contentMusicPath)
	{
		int index = random.Int(ListaMusica.Count);
		contentMusicPath = ListaMusica[index];
	}

	public override Color ChangeSkyColor(Color color, Vector3 direction, float timeOfDay, int temperature)
	{
		if (SubsystemGreenNightSky.Instance != null && SubsystemGreenNightSky.Instance.IsGreenNightActive)
		{
			return new Color(16, 81, 0);
		}
		return color;
	}

	public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
	{
		m_greenNightDialogShown = false;

		RectangleWidget logo = mainMenuScreen.Children.Find<RectangleWidget>("Logo", true);
		if (logo != null)
		{
			logo.Subtexture = ContentManager.Get<Subtexture>("Textures/Gui/Logo");
			logo.Size = new Vector2(320f, 136f);
		}

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
