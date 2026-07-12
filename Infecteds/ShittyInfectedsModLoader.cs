using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game;

public class ShittyInfectedsModLoader : ModLoader
{
	private static readonly List<string> ListaMusica = new List<string>
	{
		"Music/Menu Music",
		"Music/Menu Music 2",
		"Music/Friday the 13th - Killer Puzzle - Theme Song"
	};

	private Game.Random random = new Game.Random();

	public override void __ModInitialize()
	{
		ModsManager.RegisterHook("MenuPlayMusic", this);
		ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
		ModsManager.RegisterHook("OnMinerHit", this);
		ModsManager.RegisterHook("CalculateCreatureInjuryAmount", this);
		ModsManager.RegisterHook("OnWidgetConstruct", this);
		ModsManager.RegisterHook("OnPlayerSpawned", this);
		ModsManager.RegisterHook("ChangeSkyColor", this);
		ModsManager.RegisterHook("OnPlayerInputInteract", this);
		ModsManager.RegisterHook("OnProjectileRaycastBody", this);
		ModsManager.RegisterHook("AfterWidgetUpdate", this);
		ModsManager.RegisterHook("GuiUpdate", this);
	}

	public override void GuiUpdate(ComponentGui componentGui)
	{
		if (componentGui?.m_componentPlayer?.ComponentBody == null)
			return;

		ContainerWidget guiWidget = componentGui.m_componentPlayer.GuiWidget;
		if (guiWidget == null)
			return;

		// Buscar o crear el label de coordenadas
		LabelWidget coordLabel = guiWidget.Children.Find<LabelWidget>("ShittyCoordsLabel", false);
		if (coordLabel == null)
		{
			coordLabel = new LabelWidget
			{
				Name = "ShittyCoordsLabel",
				Text = "",
				Color = new Color(255, 255, 255, 200),
				HorizontalAlignment = WidgetAlignment.Near,
				VerticalAlignment = WidgetAlignment.Near,
				FontScale = 0.6f,
				DropShadow = true,
				Margin = new Vector2(80f, 20f)
			};
			guiWidget.Children.Add(coordLabel);
		}

		// 1. Verificar si está desactivado en la configuración
		if (!ShittyInfectedsSettings.ShowCoordinates)
		{
			coordLabel.IsVisible = false;
			return;
		}

		// 2. Verificar si el jugador está vivo y jugando normalmente
		bool isAlive = componentGui.m_componentPlayer.ComponentHealth.Health > 0f;
		bool isReady = componentGui.m_componentPlayer.PlayerData.IsReadyForPlaying;

		// Mostrar solo si está vivo y listo para jugar
		coordLabel.IsVisible = isAlive && isReady;

		// 3. Actualizar coordenadas con LanguageControl
		if (coordLabel.IsVisible)
		{
			Vector3 pos = componentGui.m_componentPlayer.ComponentBody.Position;
			coordLabel.Text = string.Format(LanguageControl.Get("ShittyInfectedsMod", "1"), pos.X, pos.Y, pos.Z);
		}
	}

	public override void OnProjectileRaycastBody(ComponentBody body, Projectile projectile, float distance, out bool ignore)
	{
		ignore = false;
		if (projectile?.OwnerEntity == null || body?.Entity == null) return;

		ComponentCreature owner = projectile.OwnerEntity.FindComponent<ComponentCreature>();
		ComponentCreature hit = body.Entity.FindComponent<ComponentCreature>();
		if (owner == null || hit == null || owner.Entity == hit.Entity) return;

		ComponentNewHerdBehavior ownerNewHerd = owner.Entity.FindComponent<ComponentNewHerdBehavior>();
		ComponentNewHerdBehavior hitNewHerd = hit.Entity.FindComponent<ComponentNewHerdBehavior>();
		ComponentZombieHerdBehavior ownerZombieHerd = owner.Entity.FindComponent<ComponentZombieHerdBehavior>();
		ComponentZombieHerdBehavior hitZombieHerd = hit.Entity.FindComponent<ComponentZombieHerdBehavior>();

		bool sameNewHerd = ownerNewHerd != null && hitNewHerd != null && ownerNewHerd.HerdName == hitNewHerd.HerdName && !string.IsNullOrEmpty(ownerNewHerd.HerdName);
		bool sameZombieHerd = ownerZombieHerd != null && hitZombieHerd != null && ownerZombieHerd.HerdName == hitZombieHerd.HerdName && !string.IsNullOrEmpty(ownerZombieHerd.HerdName);
		bool isPlayerHerd = (ownerNewHerd != null && ownerNewHerd.HerdName == "player") || owner.Entity.FindComponent<ComponentPlayer>() != null;
		bool isHitPlayerHerd = hitNewHerd != null && hitNewHerd.HerdName == "player";

		if (sameNewHerd || sameZombieHerd || (isPlayerHerd && isHitPlayerHerd))
		{
			bool isTarget = false;
			ComponentNewChaseBehavior newChase = owner.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase?.Target != null && newChase.Target.Entity == hit.Entity) isTarget = true;

			if (!isTarget)
			{
				ComponentZombieChaseBehavior zombieChase = owner.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (zombieChase?.Target != null && zombieChase.Target.Entity == hit.Entity) isTarget = true;
			}

			if (!isTarget) ignore = true;
		}
	}

	public override void OnPlayerInputInteract(ComponentPlayer player, ref bool handled, ref double timeInterval, ref int priorityUse, ref int priorityInteract, ref int priorityPlace)
	{
		if (handled) return;

		// --- NUEVA LÓGICA: INTERACCIÓN CON INVENTARIO DE CRIATURA ---
		if (player.ComponentMiner != null && player.ComponentCreatureModel != null)
		{
			Vector3 eyePosition = player.ComponentCreatureModel.EyePosition;
			Vector3 forwardVector = player.ComponentCreatureModel.EyeRotation.GetForwardVector();
			Ray3 ray = new Ray3(eyePosition, forwardVector);

			object raycastResult = player.ComponentMiner.Raycast(ray, RaycastMode.Interaction, false, true, false);

			if (raycastResult is BodyRaycastResult bodyResult)
			{
				if (bodyResult.ComponentBody != null)
				{
					ComponentCreatureInventory creatureInv = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreatureInventory>();

					if (creatureInv != null)
					{
						// 1. Ejecutamos la animación de "Poke"
						player.ComponentMiner.Poke(false);

						// 2. Asignamos el widget a la propiedad ModalPanelWidget (EXACTAMENTE igual que hace el cofre)
						player.ComponentGui.ModalPanelWidget = new CreatureInventoryWidget(player.ComponentMiner.Inventory, creatureInv);

						// 3. Sonido de interfaz
						AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);

						// 4. Marcamos como manejado
						handled = true;
						return;
					}
				}
			}
		}

		// --- LÓGICA EXISTENTE: CONTROL REMOTO ---
		int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
		int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);

		if (activeBlockIndex == BlocksManager.GetBlockIndex<GreenNightRemoteControlBlock>())
		{
			SubsystemGreenNightSky subsystemGreenNight = player.Project.FindSubsystem<SubsystemGreenNightSky>(true);

			if (subsystemGreenNight != null)
			{
				GreenNightActivationDialog dialog = new GreenNightActivationDialog(subsystemGreenNight);
				DialogsManager.ShowDialog(player.GuiWidget, dialog);
			}

			handled = true;
		}
	}

	public override bool OnPlayerSpawned(PlayerData.SpawnMode spawnMode, ComponentPlayer player, Vector3 position)
	{
		if ((spawnMode == PlayerData.SpawnMode.InitialIntro || spawnMode == PlayerData.SpawnMode.InitialNoIntro)
			&& player.PlayerData.SpawnsCount <= 1)
		{
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

		// CASO 1: El jugador ataca a una criatura
		if (attacker is ComponentPlayer)
		{
			// Verificamos la configuración: "When hitting a creature, your herd allies will attack it"
			if (!ShittyInfectedsSettings.EnableCreatureAttacks) return;

			enemy = victim;
		}
		// CASO 2: Una criatura ataca al jugador
		else if (victim is ComponentPlayer)
		{
			// Verificamos la configuración: "When a creature hits you in creative, your allies will attack it"
			if (!ShittyInfectedsSettings.AttackOnHitCreative) return;

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

		// Verificamos la configuración: Si está desactivado, no hacemos nada y dejamos la probabilidad normal
		if (!ShittyInfectedsSettings.EnableCreatureAttacks) return;

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

	public override void AfterWidgetUpdate(Widget widget)
	{
		if (widget is BevelledButtonWidget button && button.Name == "ZombiConfigButton")
		{
			if (button.IsClicked)
			{
				ScreensManager.SwitchScreen("ShittyInfectedsSettingsScreen");
			}
		}
	}

	public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
	{
		if (ScreensManager.FindScreen<Screen>("ShittyInfectedsSettingsScreen") == null)
		{
			ScreensManager.AddScreen("ShittyInfectedsSettingsScreen", new ShittyInfectedsSettingsScreen());
		}

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

	public override void SaveSettings(XElement xElement)
	{
		// El juego llama a esto cuando guarda la configuración. 
		// Llamamos a nuestro manager para actualizar el XML propio.
		ShittyInfectedsSettingsManager.Save();
	}

	public override void LoadSettings(XElement xElement)
	{
		// El juego llama a esto al cargar los mods. 
		// Llamamos a nuestro manager para leer nuestro XML propio.
		ShittyInfectedsSettingsManager.Load();
	}
}
