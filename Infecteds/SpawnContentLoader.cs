using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using Game;
using GameEntitySystem;

public class SpawnContentLoader : ModLoader
{
	private static MethodInfo m_spawnCreaturesMethod;

	// 1. Usamos nombres de tipos seguros (<DirtBlock>) en vez de números o strings.
	// Se cachean estáticamente para que se calculen UNA SOLA VEZ al iniciar.
	private static readonly int DirtId = BlocksManager.GetBlockIndex<DirtBlock>();
	private static readonly int GrassId = BlocksManager.GetBlockIndex<GrassBlock>();
	private static readonly int SandId = BlocksManager.GetBlockIndex<SandBlock>();
	private static readonly int GravelId = BlocksManager.GetBlockIndex<GravelBlock>();

	public override void __ModInitialize()
	{
		ModsManager.RegisterHook("InitializeCreatureTypes", this);
	}

	public override void InitializeCreatureTypes(SubsystemCreatureSpawn spawn, List<SubsystemCreatureSpawn.CreatureType> creatureTypes)
	{
		if (m_spawnCreaturesMethod == null)
		{
			m_spawnCreaturesMethod = typeof(SubsystemCreatureSpawn).GetMethod("SpawnCreatures", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (m_spawnCreaturesMethod == null)
			{
				Log.Error("[SpawnContentLoader] No se pudo encontrar el método SpawnCreatures en SubsystemCreatureSpawn.");
				return;
			}
		}

		// 2. Obtenemos el terreno FUERA del delegate para no crear basura ni hacer búsquedas repetitivas
		SubsystemTerrain subsystemTerrain = spawn.Project.FindSubsystem<SubsystemTerrain>(true);

		string[] infectedCreatures = new string[]
		{
			"InfectedNormal1",
			"InfectedNormal2",
			"FlyingInfected1",
			"InfectedFast1",
			"InfectedFast2",
			"InfectedMuscle1",
			"InfectedMuscle2",
			"GhostNormal"
		};

		foreach (string creatureName in infectedCreatures)
		{
			string templateName = creatureName;

			creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType(templateName, SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					// Solo spawnea de forma natural si es la Noche Verde
					if (SubsystemGreenNightSky.Instance != null && SubsystemGreenNightSky.Instance.IsGreenNightActive)
					{
						int blockBelow = Terrain.ExtractContents(subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));

						// 3. Cero campos basura (Zero Allocation). 
						// Comparación directa de enteros puros tal como lo hace el juego base (ej: num2 != 8).
						// Ya no usa HashSet, evitando sobrecarga de memoria.
						if (blockBelow == DirtId || blockBelow == GrassId || blockBelow == SandId || blockBelow == GravelId)
						{
							return 1.0f;
						}
					}

					return 0f;
				},

				SpawnFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
					try
					{
						dynamic spawnResult = m_spawnCreaturesMethod.Invoke(spawn, new object[] { ct, templateName, point, 1 });
						return spawnResult != null ? spawnResult.Count : 0;
					}
					catch (Exception ex)
					{
						Log.Error($"[SpawnContentLoader] Error al intentar spawnear {templateName}: {ex.Message}");
						return 0;
					}
				}
			});
		}
	}
}
