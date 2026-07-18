using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class BestiaryInfectedScreen : Screen
	{
		private ListPanelWidget m_creaturesList;
		private List<BestiaryCreatureInfo> m_infectedList = new List<BestiaryCreatureInfo>();
		private BevelledRectangleWidget m_rainbowBar;
		private BevelledRectangleWidget m_buttonRect;  // Fondo del botón de retroceso
		private RectangleWidget m_arrowImage;
		private static float s_hue = 0f;

		public BestiaryInfectedScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/BestiaryInfectedScreen");
			LoadContents(this, node);
			m_creaturesList = Children.Find<ListPanelWidget>("CreaturesList", true);
			m_creaturesList.ItemWidgetFactory = ItemWidgetFactory;
			m_creaturesList.ItemClicked += OnCreaturesListItemClicked;
			m_rainbowBar = Children.Find<BevelledRectangleWidget>("RainbowBar", true);

			// Obtener componentes del botón de retroceso
			ButtonWidget backButton = Children.Find<ButtonWidget>("TopBar.Back", true);
			m_buttonRect = backButton?.Children.Find<BevelledRectangleWidget>("BevelledButton.Rectangle", true);
			m_arrowImage = backButton?.Children.Find<RectangleWidget>("BevelledButton.Image", true);

			BuildInfectedList();
		}

		private void BuildInfectedList()
		{
			// Obtener todos los templates de criaturas infectadas desde el bloque
			HashSet<string> infectedTemplates = new HashSet<string>();
			var eggBlock = (InfectedEggBlock)BlocksManager.Blocks[InfectedEggBlock.Index];
			foreach (InfectedEggBlock.InfectedType type in Enum.GetValues(typeof(InfectedEggBlock.InfectedType)))
			{
				string[] templates = InfectedEggBlock.GetCreaturesForType(type);
				foreach (string t in templates)
					infectedTemplates.Add(t);
			}

			// Recorrer todas las definiciones de entidades
			foreach (ValuesDictionary entityDict in DatabaseManager.EntitiesValuesDictionaries)
			{
				string templateName = entityDict.DatabaseObject.Name;
				if (!infectedTemplates.Contains(templateName))
					continue;

				ValuesDictionary creatureDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentCreature));
				if (creatureDict == null)
					continue;

				string displayName = creatureDict.GetValue<string>("DisplayName");
				if (string.IsNullOrEmpty(displayName))
					continue;

				// Obtener descripción
				string description = creatureDict.GetValue<string>("Description") ?? "";
				// Obtener otros componentes
				ValuesDictionary modelDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentCreatureModel));
				ValuesDictionary bodyDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentBody));
				ValuesDictionary healthDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentHealth));
				ValuesDictionary minerDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentMiner));
				ValuesDictionary locomDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentLocomotion));
				ValuesDictionary herdDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentHerdBehavior));
				ValuesDictionary mountDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentMount));
				ValuesDictionary lootDict = DatabaseManager.FindValuesDictionaryForComponent(entityDict, typeof(ComponentLoot));

				BestiaryCreatureInfo info = new BestiaryCreatureInfo
				{
					EntityValuesDictionary = entityDict,
					Order = 0,
					DisplayName = displayName,
					Description = description,
					ModelName = modelDict?.GetValue<string>("ModelName") ?? "",
					TextureOverride = modelDict?.GetValue<string>("TextureOverride"),
					Mass = bodyDict?.GetValue<float>("Mass") ?? 1f,
					AttackResilience = healthDict?.GetValue<float>("AttackResilience") ?? 0f,
					AttackPower = minerDict?.GetValue<float>("AttackPower") ?? 0f,
					MovementSpeed = locomDict != null ? MathUtils.Max(locomDict.GetValue<float>("WalkSpeed"), locomDict.GetValue<float>("FlySpeed"), locomDict.GetValue<float>("SwimSpeed")) : 0f,
					JumpHeight = locomDict != null ? MathUtils.Sqr(locomDict.GetValue<float>("JumpSpeed")) / 20f : 0f,
					IsHerding = herdDict != null,
					CanBeRidden = mountDict != null,
					HasSpawnerEgg = true,
					Loot = lootDict != null ? ComponentLoot.ParseLootList(lootDict.GetValue<ValuesDictionary>("Loot")) : new List<ComponentLoot.Loot>()
				};

				m_infectedList.Add(info);
			}

			// Ordenar por nombre
			m_infectedList = m_infectedList.OrderBy(i => i.DisplayName).ToList();

			foreach (var item in m_infectedList)
				m_creaturesList.AddItem(item);
		}

		private ContainerWidget ItemWidgetFactory(object item)
		{
			BestiaryCreatureInfo info = (BestiaryCreatureInfo)item;
			XElement node = ContentManager.Get<XElement>("Widgets/BestiaryItem");
			ContainerWidget widget = (ContainerWidget)Widget.LoadWidget(this, node, null);
			ModelWidget model = widget.Children.Find<ModelWidget>("BestiaryItem.Model", true);
			BestiaryScreen.SetupBestiaryModelWidget(info, model, new Vector3(-1f, 0f, -1f), false, false);
			widget.Children.Find<LabelWidget>("BestiaryItem.Text", true).Text = info.DisplayName;
			widget.Children.Find<LabelWidget>("BestiaryItem.Details", true).Text = info.Description;
			return widget;
		}

		private void OnCreaturesListItemClicked(object item)
		{
			ScreensManager.SwitchScreen("BestiaryInfectedDescription", new object[] { item, m_infectedList });
		}

		public override void Enter(object[] parameters)
		{
			m_creaturesList.SelectedItem = null;
		}

		public override void Update()
		{
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
				ScreensManager.GoBack(Array.Empty<object>());

			// Efecto arcoíris
			s_hue += 0.005f;
			if (s_hue >= 1f) s_hue -= 1f;
			Vector3 hsv = new Vector3(s_hue * 360f, 1f, 1f);
			Vector3 rgb = Color.HsvToRgb(hsv);
			Color rainbow = new Color(rgb);

			// Aplicar a la barra lateral (parte inferior)
			if (m_rainbowBar != null)
			{
				m_rainbowBar.CenterColor = rainbow;
				m_rainbowBar.BevelColor = rainbow;
			}

			// Aplicar al fondo del botón de retroceso
			if (m_buttonRect != null)
			{
				m_buttonRect.CenterColor = rainbow;
				m_buttonRect.BevelColor = rainbow;
			}

			// Aplicar a la flecha de retroceso
			if (m_arrowImage != null)
			{
				m_arrowImage.FillColor = rainbow;
			}
		}
	}
}
