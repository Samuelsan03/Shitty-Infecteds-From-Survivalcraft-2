using System;
using System.IO;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public static class ShittyInfectedsSettingsManager
	{
		// Ruta donde se guardará el archivo XML (en la carpeta de documentos del juego)
		private static readonly string SettingsFilePath = "app:/ShittyInfectedsSettings.xml";
		private const string RootElementName = "ShittyInfectedsSettings";

		/// <summary>
		/// Guarda la configuración actual en el archivo XML
		/// </summary>
		public static void Save()
		{
			try
			{
				// Creamos la raíz del XML
				XElement root = new XElement(RootElementName);

				// Agregamos la configuración 1
				root.Add(new XElement("EnableCreatureAttacks",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.EnableCreatureAttacks.ToString().ToLower())
				));

				// Agregamos la configuración 2
				root.Add(new XElement("AttackOnHitCreative",
					new XAttribute("type", "bool"),
					new XAttribute("value", ShittyInfectedsSettings.AttackOnHitCreative.ToString().ToLower())
				));

				// <!-- futuros botones a agregar se actualiza el xml -->

				// Escribimos el archivo
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
				// Si el archivo no existe, usamos los valores por defecto y no hacemos nada
				if (!Storage.FileExists(SettingsFilePath)) return;

				using (Stream stream = Storage.OpenFile(SettingsFilePath, OpenFileMode.Read))
				{
					XElement root = XmlUtils.LoadXmlFromStream(stream, null, true);

					if (root.Name != RootElementName) return;

					// Leemos configuración 1
					XElement elem1 = root.Element("EnableCreatureAttacks");
					if (elem1 != null)
					{
						if (bool.TryParse(elem1.Attribute("value")?.Value, out bool val1))
						{
							ShittyInfectedsSettings.EnableCreatureAttacks = val1;
						}
					}

					// Leemos configuración 2
					XElement elem2 = root.Element("AttackOnHitCreative");
					if (elem2 != null)
					{
						if (bool.TryParse(elem2.Attribute("value")?.Value, out bool val2))
						{
							ShittyInfectedsSettings.AttackOnHitCreative = val2;
						}
					}

					// <!-- futuros botones a agregar se leen aquí -->
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ShittyInfecteds] Error al cargar la configuración: {ex}");
			}
		}
	}
}
