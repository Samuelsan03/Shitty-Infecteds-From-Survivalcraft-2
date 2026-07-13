using System;
using System.IO;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public static class ShittyInfectedsSettingsManager
	{
		private static readonly string SettingsFilePath = ModsManager.ExternalPath + "/ShittyInfectedsSettings.xml";
		private const string RootElementName = "ShittyInfectedsSettings";

		public static void Save()
		{
			try
			{
				XElement root = new XElement(RootElementName);

				root.Add(new XElement("EnableCreatureAttacks",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.EnableCreatureAttacks.ToString().ToLower())
				));

				root.Add(new XElement("AttackOnHitCreative",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.AttackOnHitCreative.ToString().ToLower())
				));

				root.Add(new XElement("ShowCoordinates",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.ShowCoordinates.ToString().ToLower())
				));

				root.Add(new XElement("ShowCreatureHealthBars",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.ShowCreatureHealthBars.ToString().ToLower())
				));

				root.Add(new XElement("EnableCreatureBleeding",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.EnableCreatureBleeding.ToString().ToLower())
				));

				using (Stream stream = Storage.OpenFile(SettingsFilePath, OpenFileMode.Create))
				{
					XmlUtils.SaveXmlToStream(root, stream, System.Text.Encoding.UTF8, true);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyInfecteds] Error al guardar la configuración: {ex}");
			}
		}

		public static void Load()
		{
			try
			{
				if (!Storage.FileExists(SettingsFilePath)) return;

				using (Stream stream = Storage.OpenFile(SettingsFilePath, OpenFileMode.Read))
				{
					XElement root = XmlUtils.LoadXmlFromStream(stream, null, true);
					if (root.Name != RootElementName) return;

					XElement elem1 = root.Element("EnableCreatureAttacks");
					if (elem1 != null)
					{
						if (bool.TryParse(elem1.Attribute("value")?.Value, out bool val1))
							ShittyInfectedsSettings.EnableCreatureAttacks = val1;
					}

					XElement elem2 = root.Element("AttackOnHitCreative");
					if (elem2 != null)
					{
						if (bool.TryParse(elem2.Attribute("value")?.Value, out bool val2))
							ShittyInfectedsSettings.AttackOnHitCreative = val2;
					}

					XElement elem3 = root.Element("ShowCoordinates");
					if (elem3 != null)
					{
						if (bool.TryParse(elem3.Attribute("value")?.Value, out bool val3))
							ShittyInfectedsSettings.ShowCoordinates = val3;
					}

					XElement elem4 = root.Element("ShowCreatureHealthBars");
					if (elem4 != null)
					{
						if (bool.TryParse(elem4.Attribute("value")?.Value, out bool val4))
							ShittyInfectedsSettings.ShowCreatureHealthBars = val4;
					}

					XElement elem5 = root.Element("EnableCreatureBleeding");
					if (elem5 != null)
					{
						if (bool.TryParse(elem5.Attribute("value")?.Value, out bool val5))
							ShittyInfectedsSettings.EnableCreatureBleeding = val5;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyInfecteds] Error al cargar la configuración: {ex}");
			}
		}
	}
}
