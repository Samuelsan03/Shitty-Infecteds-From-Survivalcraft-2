using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemNarcosInvasion : Subsystem, IUpdateable
	{
		public static SubsystemNarcosInvasion Instance { get; private set; }

		public bool HasAcceptedWar { get; set; }
		public bool IsInvasionActive { get; private set; }

		// Cache estatico: se carga UNA sola vez y es accesible sin depender
		// del orden de carga de subsystems.
		private static List<BanditEntry> s_banditEntries;
		public static List<BanditEntry> BanditEntries
		{
			get
			{
				if (s_banditEntries == null)
				{
					s_banditEntries = new List<BanditEntry>();
					LoadBanditXmlStatic(s_banditEntries);
				}
				return s_banditEntries;
			}
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemTimeOfDay m_subsystemTimeOfDay;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemTerrain m_subsystemTerrain;

		// ============================================================
		//  Spawning de narcotraficantes durante la invasion nocturna.
		//  Los bandits generados aqui tienen m_isNarcosInvasion = true,
		//  por lo que ComponentBanditChaseBehavior activara su modo
		//  ultra-agresivo (caza extrema al jugador sin importar GameMode).
		//  Al amanecer IsInvasionActive = false y el modo ultra-agresivo
		//  se desactiva solo en ComponentBanditChaseBehavior.
		// ============================================================
		private Game.Random m_rng = new Game.Random();
		private double m_nextSpawnTime;
		private const double SpawnIntervalSeconds = 60.0;
		private const int MaxBanditsPerPlayer = 5;
		private const float SpawnMinDistance = 25f;
		private const float SpawnMaxDistance = 45f;
		private const float CountRadius = 60f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			Instance = this;

			m_subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);

			// FIX: valor por defecto false si la clave no existe (mundo viejo)
			HasAcceptedWar = valuesDictionary.GetValue<bool>("HasAcceptedWar", false);

			// Forzar carga del XML (lazy)
			int count = BanditEntries.Count;

			m_nextSpawnTime = 0.0;
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			valuesDictionary.SetValue<bool>("HasAcceptedWar", HasAcceptedWar);
		}

		// ============================================================
		//  Carga estatica del XML via ContentManager.Get<XElement>
		//  Se llama la primera vez que alguien accede a BanditEntries.
		// ============================================================
		private static void LoadBanditXmlStatic(List<BanditEntry> target)
		{
			try
			{
				XElement root = ContentManager.Get<XElement>("Waves/NarcosInvasion");
				if (root == null)
				{
					Log.Error("[NarcosInvasion] XML 'Waves/NarcosInvasion' no encontrado en ContentManager.");
					return;
				}

				foreach (XElement elem in root.Elements("Entity"))
				{
					XAttribute nameAttr = elem.Attribute("name");
					XAttribute probAttr = elem.Attribute("probability");

					if (nameAttr == null || probAttr == null)
					{
						Log.Warning("[NarcosInvasion] Entrada <Entity> sin name/probability. Saltando.");
						continue;
					}

					string name = nameAttr.Value;
					float prob;
					if (!float.TryParse(probAttr.Value, out prob))
					{
						Log.Warning("[NarcosInvasion] probability invalida para {0}: '{1}'", name, probAttr.Value);
						continue;
					}

					target.Add(new BanditEntry
					{
						EntityName = name,
						Probability = prob
					});
				}
			}
			catch (Exception ex)
			{
				Log.Error("[NarcosInvasion] Error cargando XML: {0}", ex.Message);
			}
		}

		public bool IsNightTime()
		{
			if (m_subsystemTimeOfDay == null) return false;

			float tod = m_subsystemTimeOfDay.TimeOfDay;
			float nightStart = m_subsystemTimeOfDay.NightStart;
			float dawnStart = m_subsystemTimeOfDay.DawnStart;

			if (nightStart <= dawnStart)
				return tod >= nightStart && tod < dawnStart;
			else
				return tod >= nightStart || tod < dawnStart;
		}

		public void Update(float dt)
		{
			if (!HasAcceptedWar) return;
			if (BanditEntries.Count == 0) return;

			bool isNight = IsNightTime();

			if (isNight && !IsInvasionActive)
			{
				IsInvasionActive = true;
				NotifyAllPlayers(
					LanguageControl.Get("SubsystemNarcosInvasion", 1),
					new Color(255, 50, 50));
				// Primera oleada pronto (5s despues del anochecer)
				m_nextSpawnTime = m_subsystemTime.GameTime + 5.0;
			}
			else if (!isNight && IsInvasionActive)
			{
				IsInvasionActive = false;
				NotifyAllPlayers(
					LanguageControl.Get("SubsystemNarcosInvasion", 2),
					Color.White);
			}

			// Spawning de oleadas mientras la invasion este activa.
			if (IsInvasionActive && m_subsystemTime.GameTime >= m_nextSpawnTime)
			{
				SpawnNarcotraficantesWave();
				m_nextSpawnTime = m_subsystemTime.GameTime + SpawnIntervalSeconds;
			}
		}

		// ============================================================
		//  Genera una oleada de narcotraficantes cerca de cada jugador.
		//  Respeta un limite MaxBanditsPerPlayer para no saturar el mundo.
		// ============================================================
		private void SpawnNarcotraficantesWave()
		{
			if (m_subsystemPlayers == null || m_subsystemTerrain == null) return;
			if (m_subsystemTerrain.Terrain == null) return;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player == null || player.ComponentBody == null) continue;
				if (player.ComponentHealth != null && player.ComponentHealth.Health <= 0f) continue;

				Vector3 playerPos = player.ComponentBody.Position;

				// No saturar: contar narcotraficantes vivos cerca del jugador.
				int existing = CountNarcosBanditsNear(playerPos, CountRadius);
				int toSpawn = Math.Max(0, MaxBanditsPerPlayer - existing);
				if (toSpawn <= 0) continue;

				for (int i = 0; i < toSpawn; i++)
				{
					Vector3? spawnPos = FindSpawnPositionNear(playerPos);
					if (spawnPos == null) continue;

					string entityName = PickBanditEntity(m_rng);
					if (string.IsNullOrEmpty(entityName)) continue;

					Entity entity = DatabaseManager.CreateEntity(Project, entityName, null, true);
					if (entity == null) continue;

					// Setear el flag de narcotraficante directamente en el componente.
					// Funciona sin importar el nombre del componente en el template.
					// Como Load() ya se ejecuto dentro de CreateEntity, asignar aqui
					// hace que el flag este activo antes del primer Update() del bandido.
					ComponentBanditChaseBehavior chase = entity.FindComponent<ComponentBanditChaseBehavior>();
					if (chase != null)
					{
						chase.m_isNarcosInvasion = true;
					}

					ComponentBody body = entity.FindComponent<ComponentBody>(true);
					if (body != null)
					{
						body.Position = spawnPos.Value;
					}

					Project.AddEntity(entity);
				}
			}
		}

		// ============================================================
		//  Busca una posicion valida para spawnear un bandido cerca
		//  del jugador: entre SpawnMinDistance y SpawnMaxDistance, en
		//  suelo firme (no agua), probando hasta 10 veces.
		// ============================================================
		private Vector3? FindSpawnPositionNear(Vector3 playerPos)
		{
			for (int attempt = 0; attempt < 10; attempt++)
			{
				float angle = m_rng.Float(0f, MathF.PI * 2f);
				float distance = m_rng.Float(SpawnMinDistance, SpawnMaxDistance);
				int x = Terrain.ToCell(playerPos.X + MathF.Cos(angle) * distance);
				int z = Terrain.ToCell(playerPos.Z + MathF.Sin(angle) * distance);

				int y = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(x, z) + 1;
				if (y < 1 || y >= 255) continue;

				// Evitar spawn en agua
				int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
				if (block is WaterBlock) continue;

				return new Vector3(x + 0.5f, y + 0.1f, z + 0.5f);
			}
			return null;
		}

		// ============================================================
		//  Cuenta cuantos bandidos narcotraficantes vivos hay cerca
		//  de una posicion (dentro de radius). Itera Project.Entities.
		//  Se llama una vez por oleada (cada 60s) - aceptable.
		// ============================================================
		private int CountNarcosBanditsNear(Vector3 position, float radius)
		{
			int count = 0;
			float radiusSq = radius * radius;
			foreach (Entity entity in Project.Entities)
			{
				ComponentBanditChaseBehavior chase = entity.FindComponent<ComponentBanditChaseBehavior>();
				if (chase == null || !chase.m_isNarcosInvasion) continue;

				ComponentBody body = entity.FindComponent<ComponentBody>();
				if (body == null) continue;

				if (Vector3.DistanceSquared(body.Position, position) < radiusSq)
				{
					count++;
				}
			}
			return count;
		}

		public string PickBanditEntity(Game.Random rng)
		{
			var entries = BanditEntries;
			if (entries.Count == 0) return null;

			float total = 0f;
			foreach (var e in entries) total += e.Probability;

			if (total <= 0f) return entries[0].EntityName;

			float r = rng.Float(0f, total);
			foreach (var e in entries)
			{
				if (r < e.Probability) return e.EntityName;
				r -= e.Probability;
			}
			return entries[entries.Count - 1].EntityName;
		}

		private void NotifyAllPlayers(string text, Color color)
		{
			if (m_subsystemPlayers == null) return;
			foreach (ComponentPlayer p in m_subsystemPlayers.ComponentPlayers)
			{
				p.ComponentGui.DisplaySmallMessage(text, color, false, true);
			}
		}

		public class BanditEntry
		{
			public string EntityName;
			public float Probability;
		}
	}
}
