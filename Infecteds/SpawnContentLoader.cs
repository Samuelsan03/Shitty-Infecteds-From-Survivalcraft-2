using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using Game;
using GameEntitySystem;

public class SpawnContentLoader : ModLoader
{
	private static MethodInfo m_spawnCreaturesMethod;

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

		SubsystemTerrain subsystemTerrain = spawn.Project.FindSubsystem<SubsystemTerrain>(true);

		int dirtIndex = BlocksManager.GetBlockIndex("DirtBlock");
		int grassIndex = BlocksManager.GetBlockIndex("GrassBlock");
		int sandIndex = BlocksManager.GetBlockIndex("SandBlock");
		int gravelIndex = BlocksManager.GetBlockIndex("GravelBlock");

		HashSet<int> allowedBlocks = new HashSet<int> { dirtIndex, grassIndex, sandIndex, gravelIndex };

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
				// --- SOLUCIÓN AQUÍ ---
				// Desactivamos completamente el spawn natural de estas criaturas.
				// Solo deben aparecer a través de tu XML (SubsystemInfectedWaves).
				SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType ct, Point3 point)
				{
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
