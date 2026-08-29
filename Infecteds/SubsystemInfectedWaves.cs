using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemInfectedWaves : Subsystem, IUpdateable
	{
		public static SubsystemInfectedWaves Instance { get; private set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public List<InfectedWaveData> AllWaves { get; private set; } = new List<InfectedWaveData>();

		public int CurrentWaveIndex { get; private set; } = -1;

		public int CurrentSpawnIndex { get; private set; }

		public bool IsWaveActive { get; private set; }

		public int TotalWaves => AllWaves.Count;

		private SubsystemGreenNightSky m_subsystemGreenNight;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemSpawn m_subsystemSpawn;

		private Random m_random = new Random();

		private bool m_wasGreenNightActive;
		private bool m_allWavesCompleted;
		private float m_spawnTimer;

		private const float SPAWN_INTERVAL = 1f;
		private const float SPAWN_RADIUS = 40f;
		private const float MIN_SPAWN_RADIUS = 15f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			Instance = this;

			m_subsystemGreenNight = Project.FindSubsystem<SubsystemGreenNightSky>(false);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);

			LoadWavesFromXml();
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
		}

		public override void Dispose()
		{
			if (Instance == this)
				Instance = null;
			base.Dispose();
		}

		public void Update(float dt)
		{
			if (m_subsystemGreenNight == null)
			{
				m_subsystemGreenNight = Project.FindSubsystem<SubsystemGreenNightSky>(false);
				if (m_subsystemGreenNight == null) return;
			}

			bool isGreenNightActive = m_subsystemGreenNight.IsGreenNightActive;

			if (isGreenNightActive && !m_wasGreenNightActive)
			{
				OnGreenNightStarted();
			}

			if (!isGreenNightActive && m_wasGreenNightActive)
			{
				OnGreenNightEnded();
			}

			m_wasGreenNightActive = isGreenNightActive;

			if (isGreenNightActive && IsWaveActive)
			{
				UpdateWaveSystem(dt);
			}
		}

		private void LoadWavesFromXml()
		{
			try
			{
				XElement wavesXml = ContentManager.Get<XElement>("Waves/Infecteds Waves");
				AllWaves = InfectedWavesParser.ParseWaves(wavesXml);
				Log.Information("Cargadas " + AllWaves.Count + " olas de infectados.");
			}
			catch (Exception ex)
			{
				Log.Error("Error al cargar olas: " + ex.Message);
				AllWaves = new List<InfectedWaveData>();
			}
		}

		private void OnGreenNightStarted()
		{
			if (m_allWavesCompleted) return;

			CurrentWaveIndex++;
			CurrentSpawnIndex = 0;

			if (CurrentWaveIndex < AllWaves.Count)
			{
				IsWaveActive = true;
				m_spawnTimer = 0f;

				Log.Information("Iniciando Ola " + AllWaves[CurrentWaveIndex].WaveNumber);
				NotifyPlayersWaveStarted(AllWaves[CurrentWaveIndex].WaveNumber);
			}
			else
			{
				m_allWavesCompleted = true;
			}
		}

		private void OnGreenNightEnded()
		{
			IsWaveActive = false;
		}

		private void UpdateWaveSystem(float dt)
		{
			if (CurrentWaveIndex < 0 || CurrentWaveIndex >= AllWaves.Count) return;

			m_spawnTimer -= dt;
			if (m_spawnTimer <= 0f)
			{
				m_spawnTimer = SPAWN_INTERVAL;

				InfectedWaveData currentWave = AllWaves[CurrentWaveIndex];

				// LOOP CONTINUO: Reiniciar al llegar al final
				if (CurrentSpawnIndex >= currentWave.InfectedList.Count)
				{
					CurrentSpawnIndex = 0;
				}

				string entityName = currentWave.InfectedList[CurrentSpawnIndex];
				SpawnInfected(entityName);
				CurrentSpawnIndex++;
			}
		}

		private void SpawnInfected(string entityName)
		{
			try
			{
				Vector3 spawnPosition = GetSpawnPosition();
				if (spawnPosition == Vector3.Zero) return;

				// ConstantSpawn = false: Spawn temporal, no se guarda en chunks
				// Así no se acumulan al recargar el mundo
				SpawnEntityData data = new SpawnEntityData
				{
					TemplateName = entityName,
					Position = spawnPosition,
					ConstantSpawn = false
				};

				m_subsystemSpawn.SpawnEntity(data);
			}
			catch (Exception ex)
			{
				Log.Error("Error al spawnear " + entityName + ": " + ex.Message);
			}
		}

		private Vector3 GetSpawnPosition()
		{
			if (m_subsystemPlayers == null || m_subsystemPlayers.ComponentPlayers.Count == 0)
				return Vector3.Zero;

			ComponentPlayer targetPlayer = m_subsystemPlayers.ComponentPlayers[m_random.Int(0, m_subsystemPlayers.ComponentPlayers.Count - 1)];
			if (targetPlayer?.ComponentBody == null) return Vector3.Zero;

			Vector3 playerPosition = targetPlayer.ComponentBody.Position;

			// Intentar hasta 5 veces encontrar un punto válido
			for (int i = 0; i < 5; i++)
			{
				float angle = m_random.Float(0f, MathF.PI * 2f);
				float distance = m_random.Float(MIN_SPAWN_RADIUS, SPAWN_RADIUS);

				float x = playerPosition.X + MathF.Cos(angle) * distance;
				float z = playerPosition.Z + MathF.Sin(angle) * distance;

				int cellX = Terrain.ToCell(x);
				int cellZ = Terrain.ToCell(z);

				int terrainHeight = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(cellX, cellZ);
				int blockBelow = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(cellX, terrainHeight - 1, cellZ));

				// Validar que el bloque de abajo sea Dirt, Grass, Sand o Gravel
				if (blockBelow == BlocksManager.GetBlockIndex<DirtBlock>() ||
					blockBelow == BlocksManager.GetBlockIndex<GrassBlock>() ||
					blockBelow == BlocksManager.GetBlockIndex<SandBlock>() ||
					blockBelow == BlocksManager.GetBlockIndex<GravelBlock>())
				{
					return new Vector3(x, terrainHeight + 1f, z);
				}
			}

			// Si después de 5 intentos no encuentra un bloque válido, no spawnea
			return Vector3.Zero;
		}

		private void NotifyPlayersWaveStarted(int waveNumber)
		{
			if (m_subsystemPlayers == null) return;

			foreach (var player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentGui != null && player.ComponentHealth?.Health > 0)
				{
					player.ComponentGui.DisplayLargeMessage(
						"Estás en la ola " + waveNumber,
						"",
						5f,
						0f
					);
				}
			}
		}
	}
}
