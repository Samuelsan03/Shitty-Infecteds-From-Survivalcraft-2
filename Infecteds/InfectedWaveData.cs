using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Game
{
	public class InfectedWaveData
	{
		public int WaveNumber { get; set; }
		public List<string> InfectedList { get; set; } = new List<string>();
	}

	public static class InfectedWavesParser
	{
		public static List<InfectedWaveData> ParseWaves(XElement root)
		{
			List<InfectedWaveData> waves = new List<InfectedWaveData>();

			if (root == null)
			{
				throw new ArgumentNullException(nameof(root), "El nodo raíz del XML es nulo.");
			}

			foreach (var waveElement in root.Elements("Wave"))
			{
				InfectedWaveData wave = new InfectedWaveData();

				XAttribute waveNumberAttr = waveElement.Attribute("number");
				if (waveNumberAttr != null)
				{
					wave.WaveNumber = int.Parse(waveNumberAttr.Value);
				}
				else
				{
					wave.WaveNumber = waves.Count + 1;
				}

				foreach (var infectedElement in waveElement.Elements("infected"))
				{
					XAttribute personAttr = infectedElement.Attribute("person");
					if (personAttr != null && !string.IsNullOrEmpty(personAttr.Value))
					{
						wave.InfectedList.Add(personAttr.Value);
					}
				}

				if (wave.InfectedList.Count > 0)
				{
					waves.Add(wave);
				}
			}

			waves.Sort((a, b) => a.WaveNumber.CompareTo(b.WaveNumber));
			return waves;
		}
	}
}
