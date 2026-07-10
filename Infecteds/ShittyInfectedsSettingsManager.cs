using System;
using System.IO;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public static class ShittyInfectedsSettingsManager
	{
		// SOLUCIÓN: Al no poner ruta (ej: "app:/"), el engine usa la ruta de escritura 
		// predeterminada segura del sistema, evitando errores de permisos de solo lectura.
		private static readonly string SettingsFilePath = "ShittyInfectedsSettings.xml";
		private const string RootElementName = "ShittyInfectedsSettings";

		/// <summary>
		/// Guarda la configuración actual en el archivo XML
		/// </summary>
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

		/// <summary>
		/// Carga la configuración desde el archivo XML si existe
		/// </summary>
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
						{
							ShittyInfectedsSettings.EnableCreatureAttacks = val1;
						}
					}

					XElement elem2 = root.Element("AttackOnHitCreative");
					if (elem2 != null)
					{
						if (bool.TryParse(elem2.Attribute("value")?.Value, out bool val2))
						{
							ShittyInfectedsSettings.AttackOnHitCreative = val2;
						}
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
