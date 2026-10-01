using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
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
			ModsManager.RegisterHook("OnPlayerDead", this);
			ModsManager.RegisterHook("ScoreMount", this);
			ModsManager.RegisterHook("UpdatePlayerInputAim", this);
		}

		public override void ScoreMount(ComponentRider rider, ComponentMount mount, out float? score)
		{
			score = null;

			if (mount?.Entity?.ValuesDictionary?.DatabaseObject?.Name != "FlyingInfected1")
				return;

			if (rider.ComponentCreature.Entity.FindComponent<ComponentPlayer>() != null)
			{
				score = -1f;
			}
		}

		public override void OnPlayerDead(PlayerData playerData)
		{
			if (!ShittyInfectedsSettings.EnableDeathMusic) return;

			InfectedsMusicManager.Initialize();

			InfectedsMusicManager.Update(
				isChasing: true,
				dt: 0f,
				path: "Music/Left 4 Dead 2 Left for Death",
				type: InfectedsMusicManager.MusicType.Death);
		}

		public override void ProcessAttackment(Attackment attackment)
		{
			if (attackment?.Target == null) return;
			if (attackment.AttackPower <= 0f) return;
			if (!attackment.EnableArmorProtection) return;

			if (attackment.Target.FindComponent<ComponentClothing>() != null) return;

			ComponentCreatureClothing creatureClothing = attackment.Target.FindComponent<ComponentCreatureClothing>();

			if (creatureClothing != null)
			{
				float originalPower = attackment.AttackPower;
				float remainingDamage = creatureClothing.ApplyArmorProtection(attackment);
				attackment.AttackPower = remainingDamage;

				if (remainingDamage <= 0f && originalPower > 0f)
				{
					ComponentHealth health = attackment.Target.FindComponent<ComponentHealth>();
					ComponentCreature attacker = attackment.Attacker?.FindComponent<ComponentCreature>();

					if (health?.Injured != null && attacker != null)
					{
						health.Injured.Invoke(new AttackInjury(0f, attackment));
					}
				}
			}
		}

		public override void OnProjectileHitBody(Projectile projectile, BodyRaycastResult bodyRaycastResult, ref Attackment attackment, ref Vector3 velocityAfterAttack, ref Vector3 angularVelocityAfterAttack, ref bool ignoreBody)
		{
			if (projectile != null && BlocksManager.Blocks[Terrain.ExtractContents(projectile.Value)] is FirearmsBulletBlock)
			{
				attackment.ImpulseFactor = 0f;
				attackment.StunTimeAdd = 0f;
				attackment.StunTimeSet = 0f;
				velocityAfterAttack = Vector3.Zero;
				angularVelocityAfterAttack = Vector3.Zero;
			}
		}

		public void OnVitalStatsUpdateSleep(ComponentVitalStats vitalStats, ref float sleep, ref float gameTimeDelta, out bool skipVanilla)
		{
			skipVanilla = false;

			if (SubsystemGreenNightSky.Instance != null && SubsystemGreenNightSky.Instance.IsGreenNightActive)
			{
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
				if (!ShittyInfectedsSettings.EnableFreeCamera) return false;

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
			ComponentBanditHerdBehavior ownerBanditHerd = owner.Entity.FindComponent<ComponentBanditHerdBehavior>();
			ComponentBanditHerdBehavior hitBanditHerd = hit.Entity.FindComponent<ComponentBanditHerdBehavior>();

			bool sameNewHerd = ownerNewHerd != null && hitNewHerd != null && ownerNewHerd.HerdName == hitNewHerd.HerdName && !string.IsNullOrEmpty(ownerNewHerd.HerdName);
			bool sameZombieHerd = ownerZombieHerd != null && hitZombieHerd != null && ownerZombieHerd.HerdName == hitZombieHerd.HerdName && !string.IsNullOrEmpty(ownerZombieHerd.HerdName);
			bool sameBanditHerd = ownerBanditHerd != null && hitBanditHerd != null && ownerBanditHerd.HerdName == hitBanditHerd.HerdName && !string.IsNullOrEmpty(ownerBanditHerd.HerdName);

			bool isOwnerPlayer = owner.Entity.FindComponent<ComponentPlayer>() != null;
			bool isOwnerPlayerHerd = ownerNewHerd != null && ownerNewHerd.HerdName == "player";
			bool isPlayerHerd = isOwnerPlayer || isOwnerPlayerHerd;

			bool isHitPlayer = hit.Entity.FindComponent<ComponentPlayer>() != null;
			bool isHitPlayerHerd = hitNewHerd != null && hitNewHerd.HerdName == "player";
			bool isHitInPlayerGroup = isHitPlayer || isHitPlayerHerd;

			if (sameNewHerd || sameZombieHerd || sameBanditHerd || (isPlayerHerd && isHitInPlayerGroup))
			{
				bool isTarget = false;

				ComponentNewChaseBehavior newChase = owner.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (newChase?.Target != null && newChase.Target.Entity == hit.Entity) isTarget = true;

				if (!isTarget)
				{
					ComponentZombieChaseBehavior zombieChase = owner.Entity.FindComponent<ComponentZombieChaseBehavior>();
					if (zombieChase?.Target != null && zombieChase.Target.Entity == hit.Entity) isTarget = true;
				}

				if (!isTarget)
				{
					ComponentBanditChaseBehavior banditChase = owner.Entity.FindComponent<ComponentBanditChaseBehavior>();
					if (banditChase?.Target != null && banditChase.Target.Entity == hit.Entity) isTarget = true;
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
						string entityName = bodyResult.ComponentBody.Entity.ValuesDictionary.DatabaseObject.Name;
						if (entityName == "FirearmsSeller")
						{
							ComponentFirearmsShop shopComponent = bodyResult.ComponentBody.Entity.FindComponent<ComponentFirearmsShop>();
							if (shopComponent != null && shopComponent.IsEntityAlive)
							{
								player.ComponentGui.ModalPanelWidget = new FirearmsShopWidget(player, shopComponent);
								AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
								handled = true;
								return;
							}
						}

						ComponentCreatureInventory creatureInv = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreatureInventory>();

						if (creatureInv != null)
						{
							int activeBlockIndex = Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue);
							bool hasBandage = activeBlockIndex == BlocksManager.GetBlockIndex<BandageBlock>();

							if (hasBandage)
							{
								ComponentCreature hitCreature = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
								if (hitCreature != null && hitCreature.ComponentHealth != null && hitCreature.ComponentHealth.Health > 0f && hitCreature.ComponentHealth.Health < 1f)
								{
									return;
								}
							}

							bool hasAntidote = activeBlockIndex == BlocksManager.GetBlockIndex<AntidotePillBlock>();

							if (hasAntidote)
							{
								ComponentCreature hitCreature = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreature>();
								if (hitCreature != null && hitCreature.ComponentHealth != null && hitCreature.ComponentHealth.Health > 0f)
								{
									ComponentCreatureFlu creatureFlu = bodyResult.ComponentBody.Entity.FindComponent<ComponentCreatureFlu>();
									ComponentInfectedWithPoison creaturePoison = bodyResult.ComponentBody.Entity.FindComponent<ComponentInfectedWithPoison>();

									if ((creatureFlu != null && creatureFlu.HasFlu) || (creaturePoison != null && creaturePoison.IsInfected))
									{
										SubsystemAntidotePillBehavior subsystem = player.Project.FindSubsystem<SubsystemAntidotePillBehavior>();
										subsystem?.CureCreatureWithMessage(player, hitCreature);
										player.ComponentMiner.RemoveActiveTool(1);
										handled = true;
										return;
									}
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
			InfectedsMusicManager.FadeOut(InfectedsMusicManager.MusicType.Death, 0.5f);

			if (spawnMode == PlayerData.SpawnMode.InitialIntro || spawnMode == PlayerData.SpawnMode.InitialNoIntro)
			{
				GiveStarterItems(player);

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
			InfectedsMusicManager.UpdateFades();

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

				if (button.Name == "ShittyBestiaryButton" && button.IsClicked)
				{
					if (ScreensManager.FindScreen<Screen>("BestiaryInfected") == null)
					{
						ScreensManager.AddScreen("BestiaryInfected", new BestiaryInfectedScreen());
					}

					ScreensManager.SwitchScreen("BestiaryInfected", Array.Empty<object>());
				}
			}
		}

		public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen, StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar)
		{
			if (ScreensManager.FindScreen<Screen>("ShittyInfectedsSettingsScreen") == null)
			{
				ScreensManager.AddScreen("ShittyInfectedsSettingsScreen", new ShittyInfectedsSettingsScreen());
			}

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

			if (leftBottomBar != null)
			{
				BevelledButtonWidget bestiaryButton = new BevelledButtonWidget
				{
					Name = "ShittyBestiaryButton",
					Size = new Vector2(60f, 60f),
					Text = "",
					CenterColor = new Color(100, 255, 100),
					BevelColor = new Color(50, 200, 50)
				};

				RectangleWidget bestiaryIcon = new RectangleWidget
				{
					Size = new Vector2(40f, 40f),
					HorizontalAlignment = WidgetAlignment.Center,
					VerticalAlignment = WidgetAlignment.Center,
					Subtexture = ContentManager.Get<Subtexture>("Textures/zombi bestiario"),
					FillColor = Color.White,
					OutlineColor = new Color(0, 0, 0, 0)
				};

				bestiaryButton.Children.Add(bestiaryIcon);
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
			ComponentBanditHerdBehavior ownerBanditHerd = owner.Entity.FindComponent<ComponentBanditHerdBehavior>();
			ComponentBanditHerdBehavior hitBanditHerd = hit.Entity.FindComponent<ComponentBanditHerdBehavior>();

			bool sameNewHerd = ownerNewHerd != null && hitNewHerd != null &&
				ownerNewHerd.HerdName == hitNewHerd.HerdName &&
				!string.IsNullOrEmpty(ownerNewHerd.HerdName);

			bool sameZombieHerd = ownerZombieHerd != null && hitZombieHerd != null &&
				ownerZombieHerd.HerdName == hitZombieHerd.HerdName &&
				!string.IsNullOrEmpty(ownerZombieHerd.HerdName);

			bool sameBanditHerd = ownerBanditHerd != null && hitBanditHerd != null &&
				ownerBanditHerd.HerdName == hitBanditHerd.HerdName &&
				!string.IsNullOrEmpty(ownerBanditHerd.HerdName);

			bool isOwnerPlayer = owner.Entity.FindComponent<ComponentPlayer>() != null;
			bool isOwnerPlayerHerd = ownerNewHerd != null && ownerNewHerd.HerdName == "player";
			bool isPlayerHerd = isOwnerPlayer || isOwnerPlayerHerd;

			bool isHitPlayer = hit.Entity.FindComponent<ComponentPlayer>() != null;
			bool isHitPlayerHerd = hitNewHerd != null && hitNewHerd.HerdName == "player";
			bool isHitInPlayerGroup = isHitPlayer || isHitPlayerHerd;

			if (sameNewHerd || sameZombieHerd || sameBanditHerd || (isPlayerHerd && isHitInPlayerGroup))
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

				if (!isTarget)
				{
					ComponentBanditChaseBehavior banditChase = owner.Entity.FindComponent<ComponentBanditChaseBehavior>();
					if (banditChase?.Target != null && banditChase.Target.Entity == hit.Entity)
						isTarget = true;
				}

				if (!isTarget) return true;
			}

			return false;
		}

		private void AddItemsToInventory(ComponentPlayer player, string blockName, int count)
		{
			if (player?.ComponentMiner?.Inventory == null) return;

			int blockIndex = BlocksManager.GetBlockIndex(blockName);
			if (blockIndex < 0 || blockIndex >= 1024) return;

			int blockValue = Terrain.MakeBlockValue(blockIndex, 0, 0);
			IInventory inventory = player.ComponentMiner.Inventory;

			int remaining = count;

			for (int i = 0; i < inventory.SlotsCount && remaining > 0; i++)
			{
				if (inventory.GetSlotValue(i) == blockValue)
				{
					int capacity = inventory.GetSlotCapacity(i, blockValue);
					int currentCount = inventory.GetSlotCount(i);
					int canAdd = capacity - currentCount;
					if (canAdd > 0)
					{
						int toAdd = Math.Min(canAdd, remaining);
						inventory.AddSlotItems(i, blockValue, toAdd);
						remaining -= toAdd;
					}
				}
			}

			for (int i = 0; i < inventory.SlotsCount && remaining > 0; i++)
			{
				if (inventory.GetSlotCount(i) == 0 || inventory.GetSlotValue(i) == 0)
				{
					int capacity = inventory.GetSlotCapacity(i, blockValue);
					if (capacity > 0)
					{
						int toAdd = Math.Min(capacity, remaining);
						inventory.AddSlotItems(i, blockValue, toAdd);
						remaining -= toAdd;
					}
				}
			}
		}

		private void GiveStarterItems(ComponentPlayer player)
		{
			AddItemsToInventory(player, "CookedFishBlock", 3);
			AddItemsToInventory(player, "IronMacheteBlock", 1);
			AddItemsToInventory(player, "DesertEagleBlock", 1);
			AddItemsToInventory(player, "DesertEagleAmmunitionBlock", 5);
			AddItemsToInventory(player, "BandageSmallBlock", 5);
			AddItemsToInventory(player, "AntidotePillBlock", 5);
			AddItemsToInventory(player, "CoinBlock", 100);
		}

		public override void UpdatePlayerInputAim(
			ComponentPlayer player,
			bool isAiming,
			ref bool flag,
			ref float timeIntervalAim,
			bool skipVanilla,
			out bool skip)
		{
			skip = false;
			timeIntervalAim = 0.1f;
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
}
