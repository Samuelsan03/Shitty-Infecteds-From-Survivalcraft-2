using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCreaturesNormalSpawn : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemSpawn = Project.FindSubsystem<SubsystemSpawn>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true);

			InicializarTiposDeCriatura();

			SubsystemSpawn subsystemSpawn = m_subsystemSpawn;
			subsystemSpawn.SpawningChunk = (Action<SpawnChunk>)Delegate.Combine(subsystemSpawn.SpawningChunk, new Action<SpawnChunk>(delegate (SpawnChunk chunk)
			{
				m_spawnChunks.Add(chunk);
				if (!chunk.IsSpawned)
				{
					m_newSpawnChunks.Add(chunk);
				}
			}));
		}

		public override void OnEntityAdded(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				m_creatures.Add(key, true);
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			foreach (ComponentCreature key in entity.FindComponents<ComponentCreature>())
			{
				m_creatures.Remove(key);
			}
		}

		public virtual void InicializarTiposDeCriatura()
		{
			// ==========================================
			// LOGICA 1: NORMAL (Cualquier hora, 100%)
			// ==========================================
			m_creatureTypes.Add(new CreatureType("WerewolfShit", SpawnLocationType.Surface, true, false)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					int terrainBlock = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
					if (terrainBlock != 2 && terrainBlock != 6 && terrainBlock != 78)
					{
						return 0f;
					}
					return 1f; // 100%
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "WerewolfShit", point, 1).Count)
			});

			// ==========================================
			// LOGICA 2: CONSTANT (Solo noche, 50%)
			// ==========================================
			m_creatureTypes.Add(new CreatureType("WerewolfShit Constant", SpawnLocationType.Surface, false, true)
			{
				SpawnSuitabilityFunction = delegate (CreatureType _, Point3 point)
				{
					if (m_subsystemSky.SkyLightIntensity < 0.1f)
					{
						int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(point.X, point.Y + 1, point.Z);
						if (cellLightFast <= 7)
						{
							int terrainBlock = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
							if (terrainBlock == 2 || terrainBlock == 6 || terrainBlock == 78)
							{
								return 0.5f; // 50%
							}
						}
					}
					return 0f;
				},
				SpawnFunction = ((CreatureType creatureType, Point3 point) => SpawnCreatures(creatureType, "WerewolfShit Constant", point, 1).Count)
			});
		}

		public virtual void Update(float dt)
		{
			if (m_subsystemGameInfo.WorldSettings.EnvironmentBehaviorMode == EnvironmentBehaviorMode.Living)
			{
				if (m_newSpawnChunks.Count > 0)
				{
					m_newSpawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in m_newSpawnChunks)
					{
						SpawnChunkCreatures(chunk, 10, false);
					}
					m_newSpawnChunks.Clear();
				}
				if (m_spawnChunks.Count > 0)
				{
					m_spawnChunks.RandomShuffle((int max) => m_random.Int(0, max - 1));
					foreach (SpawnChunk chunk in m_spawnChunks)
					{
						SpawnChunkCreatures(chunk, 2, true);
					}
					m_spawnChunks.Clear();
				}
				if (m_subsystemTime.PeriodicGameTimeEvent(60.0, 2.0))
				{
					SpawnRandomCreature();
				}
			}
		}

		public virtual void SpawnRandomCreature()
		{
			if (CountCreatures(false) < m_totalLimit)
			{
				foreach (GameWidget gameWidget in m_subsystemViews.GameWidgets)
				{
					Vector2 v = new Vector2(gameWidget.ActiveCamera.ViewPosition.X, gameWidget.ActiveCamera.ViewPosition.Z);
					if (CountCreaturesInArea(v - new Vector2(68f), v + new Vector2(68f), false) >= 52)
					{
						break;
					}
					SpawnLocationType spawnLocationType = SpawnLocationType.Surface;
					Point3? spawnPoint = GetRandomSpawnPoint(gameWidget.ActiveCamera, spawnLocationType);
					if (spawnPoint != null)
					{
						Vector2 c3 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) - new Vector2(16f);
						Vector2 c2 = new Vector2((float)spawnPoint.Value.X, (float)spawnPoint.Value.Z) + new Vector2(16f);
						if (CountCreaturesInArea(c3, c2, false) >= 3)
						{
							break;
						}

						IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
															   where c.SpawnLocationType == spawnLocationType && c.RandomSpawn
															   select c;
						IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray<CreatureType>();
						IEnumerable<float> items = from c in source
												   select CalculateSpawnSuitability(c, spawnPoint.Value);
						int randomWeightedItem = GetRandomWeightedItem(items);
						if (randomWeightedItem >= 0)
						{
							CreatureType creatureType = source.ElementAt(randomWeightedItem);
							creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						}
					}
				}
			}
		}

		public virtual void SpawnChunkCreatures(SpawnChunk chunk, int maxAttempts, bool constantSpawn)
		{
			int num = constantSpawn ? ((m_subsystemGameInfo.WorldSettings.GameMode >= GameMode.Challenging) ? m_totalLimitConstantChallenging : m_totalLimitConstant) : m_totalLimit;
			int num2 = constantSpawn ? m_areaLimitConstant : m_areaLimit;
			float v = constantSpawn ? m_areaRadiusConstant : m_areaRadius;
			int num3 = CountCreatures(constantSpawn);
			Vector2 c3 = new Vector2((float)(chunk.Point.X * 16), (float)(chunk.Point.Y * 16)) - new Vector2(v);
			Vector2 c2 = new Vector2((float)((chunk.Point.X + 1) * 16), (float)((chunk.Point.Y + 1) * 16)) + new Vector2(v);
			int num4 = CountCreaturesInArea(c3, c2, constantSpawn);
			for (int i = 0; i < maxAttempts; i++)
			{
				if (num3 >= num || num4 >= num2)
				{
					break;
				}
				Point3? spawnPoint = GetRandomChunkSpawnPoint(chunk, SpawnLocationType.Surface);
				if (spawnPoint != null)
				{
					IEnumerable<CreatureType> enumerable = from c in m_creatureTypes
														   where c.SpawnLocationType == SpawnLocationType.Surface && c.ConstantSpawn == constantSpawn
														   select c;
					IEnumerable<CreatureType> source = (enumerable as CreatureType[]) ?? enumerable.ToArray<CreatureType>();
					IEnumerable<float> items = from c in source
											   select CalculateSpawnSuitability(c, spawnPoint.Value);
					int randomWeightedItem = GetRandomWeightedItem(items);
					if (randomWeightedItem >= 0)
					{
						CreatureType creatureType = source.ElementAt(randomWeightedItem);
						int num5 = creatureType.SpawnFunction(creatureType, spawnPoint.Value);
						num3 += num5;
						num4 += num5;
					}
				}
			}
		}

		public virtual List<Entity> SpawnCreatures(CreatureType creatureType, string templateName, Point3 point, int count)
		{
			List<Entity> list = new List<Entity>();
			int num = 0;
			while (count > 0 && num < 50)
			{
				Point3 spawnPoint = point;
				if (num > 0)
				{
					spawnPoint.X += m_random.Int(-8, 8);
					spawnPoint.Y += m_random.Int(-4, 8);
					spawnPoint.Z += m_random.Int(-8, 8);
				}
				Point3? point2 = ProcessSpawnPoint(spawnPoint, creatureType.SpawnLocationType);
				if (point2 != null && CalculateSpawnSuitability(creatureType, point2.Value) > 0f)
				{
					Vector3 position = new Vector3((float)point2.Value.X + m_random.Float(0.4f, 0.6f), (float)point2.Value.Y + 1.1f, (float)point2.Value.Z + m_random.Float(0.4f, 0.6f));
					Entity entity = SpawnCreature(templateName, position, creatureType.ConstantSpawn);
					if (entity != null)
					{
						list.Add(entity);
						count--;
					}
				}
				num++;
			}
			return list;
		}

		public virtual Entity SpawnCreature(string templateName, Vector3 position, bool constantSpawn)
		{
			Entity result;
			try
			{
				Entity entity = DatabaseManager.CreateEntity(Project, templateName, true);
				entity.FindComponent<ComponentBody>(true).Position = position;
				entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m_random.Float(0f, 6.2831855f));
				entity.FindComponent<ComponentCreature>(true).ConstantSpawn = constantSpawn;
				Project.AddEntity(entity);
				result = entity;
			}
			catch (Exception value)
			{
				Log.Error($"Unable to spawn creature with template \"{templateName}\". Reason: {value}");
				result = null;
			}
			return result;
		}

		public virtual Point3? GetRandomChunkSpawnPoint(SpawnChunk chunk, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 5; i++)
			{
				int x = 16 * chunk.Point.X + m_random.Int(0, 15);
				int y = m_random.Int(10, 246);
				int z = 16 * chunk.Point.Y + m_random.Int(0, 15);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? GetRandomSpawnPoint(Camera camera, SpawnLocationType spawnLocationType)
		{
			for (int i = 0; i < 10; i++)
			{
				int x = Terrain.ToCell(camera.ViewPosition.X) + m_random.Sign() * m_random.Int(24, 48);
				int y = Math.Clamp(Terrain.ToCell(camera.ViewPosition.Y) + m_random.Int(-30, 30), 2, 254);
				int z = Terrain.ToCell(camera.ViewPosition.Z) + m_random.Sign() * m_random.Int(24, 48);
				Point3? result = ProcessSpawnPoint(new Point3(x, y, z), spawnLocationType);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public virtual Point3? ProcessSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int num = Math.Clamp(spawnPoint.Y, 1, 254);
			int z = spawnPoint.Z;
			TerrainChunk chunkAtCell = m_subsystemTerrain.Terrain.GetChunkAtCell(x, z);
			if (chunkAtCell != null && chunkAtCell.State > TerrainChunkState.InvalidPropagatedLight)
			{
				for (int i = 0; i < 30; i++)
				{
					Point3 point = new Point3(x, num + i, z);
					if (TestSpawnPoint(point, spawnLocationType))
					{
						return new Point3?(point);
					}
					Point3 point2 = new Point3(x, num - i, z);
					if (TestSpawnPoint(point2, spawnLocationType))
					{
						return new Point3?(point2);
					}
				}
			}
			return null;
		}

		public virtual bool TestSpawnPoint(Point3 spawnPoint, SpawnLocationType spawnLocationType)
		{
			int x = spawnPoint.X;
			int y = spawnPoint.Y;
			int z = spawnPoint.Z;
			if (y <= 3 || y >= 253)
			{
				return false;
			}
			if (spawnLocationType == SpawnLocationType.Surface)
			{
				int cellLightFast = m_subsystemTerrain.Terrain.GetCellLightFast(x, y, z);
				if (m_subsystemSky.SkyLightValue - cellLightFast > 3)
				{
					return false;
				}
				int cellValueFast = m_subsystemTerrain.Terrain.GetCellValueFast(x, y - 1, z);
				int cellValueFast2 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y, z);
				int cellValueFast3 = m_subsystemTerrain.Terrain.GetCellValueFast(x, y + 1, z);
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast)];
				Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast2)];
				Block block3 = BlocksManager.Blocks[Terrain.ExtractContents(cellValueFast3)];
				return (block.IsCollidable_(cellValueFast) || block is WaterBlock) && !block2.IsCollidable_(cellValueFast2) && !(block2 is WaterBlock) && !block3.IsCollidable_(cellValueFast3) && !(block3 is WaterBlock);
			}
			return false;
		}

		public virtual float CalculateSpawnSuitability(CreatureType creatureType, Point3 spawnPoint)
		{
			float num = creatureType.SpawnSuitabilityFunction(creatureType, spawnPoint);
			if (CountCreatures(creatureType) > 8)
			{
				num *= 0.25f;
			}
			return num;
		}

		public virtual int CountCreatures(CreatureType creatureType)
		{
			int num = 0;
			using (Dictionary<ComponentBody, Point2>.KeyCollection.Enumerator enumerator = m_subsystemBodies.Bodies.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.Entity.ValuesDictionary.DatabaseObject.Name == creatureType.Name)
					{
						num++;
					}
				}
			}
			return num;
		}

		public virtual int CountCreatures(bool constantSpawn)
		{
			int num = 0;
			foreach (ComponentBody componentBody in m_subsystemBodies.Bodies)
			{
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					num++;
				}
			}
			return num;
		}

		public virtual int CountCreaturesInArea(Vector2 c1, Vector2 c2, bool constantSpawn)
		{
			int num = 0;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesInArea(c1, c2, m_componentBodies);
			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentBody componentBody = m_componentBodies.Array[i];
				ComponentCreature componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null && componentCreature.ConstantSpawn == constantSpawn)
				{
					Vector3 position = componentBody.Position;
					if (position.X >= c1.X && position.X <= c2.X && position.Z >= c1.Y && position.Z <= c2.Y)
					{
						num++;
					}
				}
			}
			Point2 point = Terrain.ToChunk(c1);
			Point2 point2 = Terrain.ToChunk(c2);
			for (int j = point.X; j <= point2.X; j++)
			{
				for (int k = point.Y; k <= point2.Y; k++)
				{
					SpawnChunk spawnChunk = m_subsystemSpawn.GetSpawnChunk(new Point2(j, k));
					if (spawnChunk != null)
					{
						foreach (SpawnEntityData spawnEntityData in spawnChunk.SpawnsData)
						{
							if (spawnEntityData.ConstantSpawn == constantSpawn)
							{
								Vector3 position2 = spawnEntityData.Position;
								if (position2.X >= c1.X && position2.X <= c2.X && position2.Z >= c1.Y && position2.Z <= c2.Y)
								{
									num++;
								}
							}
						}
					}
				}
			}
			return num;
		}

		public virtual int GetRandomWeightedItem(IEnumerable<float> items)
		{
			float[] array = (items as float[]) ?? items.ToArray<float>();
			float max = MathUtils.Max(array.Sum(), 1f);
			float num = m_random.Float(0f, max);
			int num2 = 0;
			foreach (float num3 in ((IEnumerable<float>)array))
			{
				if (num < num3)
				{
					return num2;
				}
				num -= num3;
				num2++;
			}
			return -1;
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemSpawn m_subsystemSpawn;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameWidgets m_subsystemViews;
		public Random m_random = new Random();
		public List<CreatureType> m_creatureTypes = new List<CreatureType>();
		public Dictionary<ComponentCreature, bool> m_creatures = new Dictionary<ComponentCreature, bool>();
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public List<SpawnChunk> m_newSpawnChunks = new List<SpawnChunk>();
		public List<SpawnChunk> m_spawnChunks = new List<SpawnChunk>();

		public static int m_totalLimit = 10;
		public static int m_areaLimit = 2;
		public static int m_areaRadius = 16;
		public static int m_totalLimitConstant = 4;
		public static int m_totalLimitConstantChallenging = 8;
		public static int m_areaLimitConstant = 3;
		public static int m_areaRadiusConstant = 42;

		public class CreatureType
		{
			public CreatureType()
			{
			}
			public CreatureType(string name, SpawnLocationType spawnLocationType, bool randomSpawn, bool constantSpawn)
			{
				Name = name;
				SpawnLocationType = spawnLocationType;
				RandomSpawn = randomSpawn;
				ConstantSpawn = constantSpawn;
			}
			public override string ToString()
			{
				return Name;
			}
			public string Name;
			public SpawnLocationType SpawnLocationType;
			public bool RandomSpawn;
			public bool ConstantSpawn;
			public Func<CreatureType, Point3, float> SpawnSuitabilityFunction;
			public Func<CreatureType, Point3, int> SpawnFunction;
		}
	}
}