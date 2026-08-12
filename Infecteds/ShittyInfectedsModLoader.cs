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
		ModsManager.RegisterHook("ManageCameras", this);
		ModsManager.RegisterHook("OnVitalStatsUpdateSleep", this);
		ModsManager.RegisterHook("OnProjectileHitBody", this);
		ModsManager.RegisterHook("ProcessAttackment", this);
	}

	/// Aplica protección de armadura de ComponentCreatureClothing a criaturas
	/// que no son jugadores (el ComponentClothing del jugador ya se maneja en vanilla)
	public override void ProcessAttackment(Attackment attackment)
	{
		// Verificaciones de seguridad
		if (attackment?.Target == null) return;
		if (attackment.AttackPower <= 0f) return;
		if (!attackment.EnableArmorProtection) return;

		// Si el objetivo ya tiene ComponentClothing (jugador), no intervenir
		// porque el sistema vanilla ya lo maneja
		if (attackment.Target.FindComponent<ComponentClothing>() != null) return;

		// Buscar ComponentCreatureClothing en la criatura objetivo
		ComponentCreatureClothing creatureClothing = attackment.Target.FindComponent<ComponentCreatureClothing>();

		if (creatureClothing != null)
		{
			// Guardar el daño original ANTES de la protección
			float originalPower = attackment.AttackPower;

			// Aplicar la protección de armadura de la ropa de la criatura
			// Esto reduce attackment.AttackPower y daña la ropa según su protección
			float remainingDamage = creatureClothing.ApplyArmorProtection(attackment);

			// Actualizar el poder de ataque con el daño restante después de la protección
			attackment.AttackPower = remainingDamage;

			// ============================================================
			// CORRECCIÓN PRINCIPAL: Si la armadura absorbió TODO el daño,
			// el flujo normal NO disparará el evento Injured (porque 
			// CalculateInjuryAmount retorna 0 cuando AttackPower <= 0).
			// Necesitamos dispararlo MANUALMENTE para que los ChaseBehaviors
			// (ComponentChaseBehavior, ComponentNewChaseBehavior, etc.)
			// reaccionen y persigan al atacante.
			// ============================================================
			if (remainingDamage <= 0f && originalPower > 0f)
			{
				ComponentHealth health = attackment.Target.FindComponent<ComponentHealth>();
				ComponentCreature attacker = attackment.Attacker?.FindComponent<ComponentCreature>();

				// Solo disparar si hay un atacante válido y el evento tiene suscriptores
				if (health?.Injured != null && attacker != null)
				{
					// AttackInjury con daño 0: no afecta la salud pero activa los ChaseBehaviors
					// porque ellos solo verifican injury.Attacker, no injury.Amount
					health.Injured.Invoke(new AttackInjury(0f, attackment));
				}
			}
		}
	}

	public override void OnProjectileHitBody(Projectile projectile, BodyRaycastResult bodyRaycastResult, ref Attackment attackment, ref Vector3 velocityAfterAttack, ref Vector3 angularVelocityAfterAttack, ref bool ignoreBody)
	{
		// Verificamos si el proyectil es de tipo FirearmsBulletBlock directamente (sin importar su índice numérico)
		if (projectile != null && BlocksManager.Blocks[Terrain.ExtractContents(projectile.Value)] is FirearmsBulletBlock)
		{
			// Elimina el empuje físico
			attackment.ImpulseFactor = 0f;

			// Elimina el aturdimiento por impacto (por si acaso mueve al mob)
			attackment.StunTimeAdd = 0f;
			attackment.StunTimeSet = 0f;

			// La bala se queda quieta
			velocityAfterAttack = Vector3.Zero;
			angularVelocityAfterAttack = Vector3.Zero;
		}
	}

	public void OnVitalStatsUpdateSleep(ComponentVitalStats vitalStats, ref float sleep, ref float gameTimeDelta, out bool skipVanilla)
	{
		skipVanilla = false;

		if (SubsystemGreenNightSky.Instance != null && SubsystemGreenNightSky.Instance.IsGreenNightActive)
		{
			// Al poner skipVanilla en true, el juego hace un "return" inmediato.
			// Esto evita que la barra baje, pero conserva el valor actual que tenga el jugador.
			skipVanilla = true;
		}
	}

	public override IEnumerable<KeyValuePair<string, int>> GetCameraList()
	{
		yield return new KeyValuePair<string, int>("Game.FreeCamera", 4);
	}

	public override void ManageCameras(GameWidget gameWidget)
	{
		gameWidget.AddCamera(new FreeCamera(gameWidget), (gw) =>
		{
			// 1. Verificamos si está activado en la configuración (ON/OFF)
			if (!ShittyInfectedsSettings.EnableFreeCamera) return false;

			// 2. Verificamos si NO es modo creativo
			ComponentPlayer player = gw.PlayerData?.ComponentPlayer;
			if (player != null)
			{
				SubsystemGameInfo gameInfo = player.Project.FindSubsystem<SubsystemGameInfo>();
				if (gameInfo != null)
				{
					return gameInfo.WorldSettings.GameMode != GameMode.Creative;
				}
			}
			return false;
		});
	}

	public override void GuiUpdate(ComponentGui componentGui)
	{
		if (componentGui?.m_componentPlayer?.ComponentBody == null)
			return;

		ContainerWidget guiWidget = componentGui.m_componentPlayer.GuiWidget;
		if (guiWidget == null)
			return;

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

		if (!ShittyInfectedsSettings.ShowCoordinates)
		{
			coordLabel.IsVisible = false;
			return;
		}

		bool isAlive = componentGui.m_componentPlayer.ComponentHealth.Health > 0f;
		bool isReady = componentGui.m_componentPlayer.PlayerData.IsReadyForPlaying;

		coordLabel.IsVisible = isAlive && isReady;

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

		// CORRECCIÓN: Verificar si el dueño es jugador O tiene manada "player"
		bool isOwnerPlayer = owner.Entity.FindComponent<ComponentPlayer>() != null;
		bool isOwnerPlayerHerd = ownerNewHerd != null && ownerNewHerd.HerdName == "player";
		bool isPlayerHerd = isOwnerPlayer || isOwnerPlayerHerd;

		// CORRECCIÓN: Verificar si el objetivo es jugador O tiene manada "player"
		bool isHitPlayer = hit.Entity.FindComponent<ComponentPlayer>() != null;
		bool isHitPlayerHerd = hitNewHerd != null && hitNewHerd.HerdName == "player";
		bool isHitInPlayerGroup = isHitPlayer || isHitPlayerHerd;

		if (sameNewHerd || sameZombieHerd || (isPlayerHerd && isHitInPlayerGroup))
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
						// NUEVA LÓGICA: Verificar si tiene vendaje y la criatura necesita curación
						int activeBlockIndex = Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue);
						bool hasBandage = activeBlockIndex == BlocksManager.GetBlockIndex<BandageSmallBlock>();

						if (hasBandage)
						{
							ComponentCreature hitCreature = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
							if (hitCreature != null && hitCreature.ComponentHealth != null && hitCreature.ComponentHealth.Health > 0f && hitCreature.ComponentHealth.Health < 1f)
							{
								// Si tiene vendaje y la criatura puede curarse, omitimos abrir el inventario
								// para que el flujo normal continúe y llegue al SubsystemBandageSmallBehavior
								return;
							}
						}

						player.ComponentMiner.Poke(false);
						player.ComponentGui.ModalPanelWidget = new CreatureInventoryWidget(player.ComponentMiner.Inventory, creatureInv);
						AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
						handled = true;
						return;
					}
				}
			}
		}

		int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
		int activeBlockIndex2 = Terrain.ExtractContents(activeBlockValue);

		if (activeBlockIndex2 == BlocksManager.GetBlockIndex<GreenNightRemoteControlBlock>())
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

		if (attacker is ComponentPlayer)
		{
			if (!ShittyInfectedsSettings.EnableCreatureAttacks) return;
			enemy = victim;
		}
		else if (victim is ComponentPlayer)
		{
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
				if (creature.Entity == enemy.Entity)
					continue;

				ComponentNewChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chaseBehavior != null)
				{
					chaseBehavior.CallRangeHelp(enemy);
				}
			}
		}
	}

	public override void OnMinerHit(ComponentMiner miner, ComponentBody targetBody, Vector3 hitPoint, Vector3 hitDirection, ref float damage, ref float hitProbability, ref float systemHitProbability, out bool skip)
	{
		skip = false;

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
		if (widget is BevelledButtonWidget button)
		{
			if (button.Name == "ZombiConfigButton" && button.IsClicked)
			{
				ScreensManager.SwitchScreen("ShittyInfectedsSettingsScreen");
			}

			if (button.Name == "ShittyExitButton" && button.IsClicked)
			{
				Window.Close();
			}

			// BOTÓN DEL BESTIARIO - AHORA CON FUNCIONALIDAD
			if (button.Name == "ShittyBestiaryButton" && button.IsClicked)
			{
				// Verificar si la pantalla ya está registrada, si no, registrarla
				if (ScreensManager.FindScreen<Screen>("BestiaryInfected") == null)
				{
					ScreensManager.AddScreen("BestiaryInfected", new BestiaryInfectedScreen());
				}

				// Cambiar a la pantalla del bestiario de infectados
				ScreensManager.SwitchScreen("BestiaryInfected", Array.Empty<object>());
			}
		}
	}

	public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
	{
		// Registrar la pantalla de configuración (ya existente)
		if (ScreensManager.FindScreen<Screen>("ShittyInfectedsSettingsScreen") == null)
		{
			ScreensManager.AddScreen("ShittyInfectedsSettingsScreen", new ShittyInfectedsSettingsScreen());
		}

		// REGISTRAR LAS PANTALLAS DEL BESTIARIO DE INFECTADOS
		if (ScreensManager.FindScreen<Screen>("BestiaryInfected") == null)
		{
			ScreensManager.AddScreen("BestiaryInfected", new BestiaryInfectedScreen());
		}

		if (ScreensManager.FindScreen<Screen>("BestiaryInfectedDescription") == null)
		{
			ScreensManager.AddScreen("BestiaryInfectedDescription", new BestiaryInfectedDescriptionScreen());
		}

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

		StackPanelWidget centerButtons = mainMenuScreen.Children.Find<StackPanelWidget>("CenterButtons", true);
		if (centerButtons != null)
		{
			if (centerButtons.Children.Count >= 3)
			{
				StackPanelWidget lastRow = centerButtons.Children[centerButtons.Children.Count - 1] as StackPanelWidget;
				if (lastRow != null)
				{
					BevelledButtonWidget exitButton = new BevelledButtonWidget
					{
						Name = "ShittyExitButton",
						Size = new Vector2(310f, 60f),
						HorizontalAlignment = WidgetAlignment.Center,
						VerticalAlignment = WidgetAlignment.Center,
						Text = LanguageControl.Get("ShittyInfectedsMod", "exitGame"),
						Color = Color.White
					};
					lastRow.Children.Add(exitButton);
				}
			}
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

		// NUEVO BOTÓN VERDE EN EL LADO IZQUIERDO
		if (leftBottomBar != null)
		{
			BevelledButtonWidget bestiaryButton = new BevelledButtonWidget
			{
				Name = "ShittyBestiaryButton",
				Size = new Vector2(60f, 60f),
				Text = "",
				CenterColor = new Color(100, 255, 100), // Fondo verde
				BevelColor = new Color(50, 200, 50)     // Bisel más oscuro (opcional)
			};

			// Creamos el icono como un hijo del botón
			RectangleWidget bestiaryIcon = new RectangleWidget
			{
				Size = new Vector2(40f, 40f), // Tamaño del icono (un poco más pequeño que el botón)
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Subtexture = ContentManager.Get<Subtexture>("Textures/zombi bestiario"),
				FillColor = Color.White, // Blanco para que no se mezcle con el verde del botón
				OutlineColor = new Color(0, 0, 0, 0) // Sin bordes negros
			};

			// Agregamos el icono DENTRO del botón
			bestiaryButton.Children.Add(bestiaryIcon);

			// Lo insertamos al final del panel izquierdo
			leftBottomBar.Children.Add(bestiaryButton);
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

	public static bool ShouldVomitIgnoreBody(ComponentBody ownerBody, ComponentBody hitBody)
	{
		if (ownerBody?.Entity == null || hitBody?.Entity == null) return false;

		ComponentCreature owner = ownerBody.Entity.FindComponent<ComponentCreature>();
		ComponentCreature hit = hitBody.Entity.FindComponent<ComponentCreature>();
		if (owner == null || hit == null || owner.Entity == hit.Entity) return false;

		ComponentNewHerdBehavior ownerNewHerd = owner.Entity.FindComponent<ComponentNewHerdBehavior>();
		ComponentNewHerdBehavior hitNewHerd = hit.Entity.FindComponent<ComponentNewHerdBehavior>();
		ComponentZombieHerdBehavior ownerZombieHerd = owner.Entity.FindComponent<ComponentZombieHerdBehavior>();
		ComponentZombieHerdBehavior hitZombieHerd = hit.Entity.FindComponent<ComponentZombieHerdBehavior>();

		bool sameNewHerd = ownerNewHerd != null && hitNewHerd != null &&
			ownerNewHerd.HerdName == hitNewHerd.HerdName &&
			!string.IsNullOrEmpty(ownerNewHerd.HerdName);

		bool sameZombieHerd = ownerZombieHerd != null && hitZombieHerd != null &&
			ownerZombieHerd.HerdName == hitZombieHerd.HerdName &&
			!string.IsNullOrEmpty(ownerZombieHerd.HerdName);

		// CORRECCIÓN: Incluir verificación directa de ComponentPlayer
		bool isOwnerPlayer = owner.Entity.FindComponent<ComponentPlayer>() != null;
		bool isOwnerPlayerHerd = ownerNewHerd != null && ownerNewHerd.HerdName == "player";
		bool isPlayerHerd = isOwnerPlayer || isOwnerPlayerHerd;

		bool isHitPlayer = hit.Entity.FindComponent<ComponentPlayer>() != null;
		bool isHitPlayerHerd = hitNewHerd != null && hitNewHerd.HerdName == "player";
		bool isHitInPlayerGroup = isHitPlayer || isHitPlayerHerd;

		if (sameNewHerd || sameZombieHerd || (isPlayerHerd && isHitInPlayerGroup))
		{
			bool isTarget = false;

			ComponentNewChaseBehavior newChase = owner.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase?.Target != null && newChase.Target.Entity == hit.Entity)
				isTarget = true;

			if (!isTarget)
			{
				ComponentZombieChaseBehavior zombieChase = owner.Entity.FindComponent<ComponentZombieChaseBehavior>();
				if (zombieChase?.Target != null && zombieChase.Target.Entity == hit.Entity)
					isTarget = true;
			}

			if (!isTarget) return true;
		}

		return false;
	}

	public override void SaveSettings(XElement xElement)
	{
		ShittyInfectedsSettingsManager.Save();
	}

	public override void LoadSettings(XElement xElement)
	{
		ShittyInfectedsSettingsManager.Load();
	}
}
