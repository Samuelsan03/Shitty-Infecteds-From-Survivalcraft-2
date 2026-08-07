using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonExplosions : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemBodies m_subsystemBodies;

		private List<ExplosionData> m_queuedExplosions = new List<ExplosionData>();
		private SparseSpatialArray<float> m_pressureByPoint;
		private PoisonExplosionParticleSystem m_poisonParticleSystem;
		private Random m_random = new Random();

		public void AddPoisonExplosion(int x, int y, int z, float pressure)
		{
			if (pressure > 0f)
			{
				m_queuedExplosions.Add(new ExplosionData { X = x, Y = y, Z = z, Pressure = pressure });
			}
		}

		public virtual void Update(float dt)
		{
			if (m_queuedExplosions.Count <= 0) return;

			int x = m_queuedExplosions[0].X;
			int y = m_queuedExplosions[0].Y;
			int z = m_queuedExplosions[0].Z;

			m_pressureByPoint = new SparseSpatialArray<float>(x, y, z, 0f);
			bool anyExploded = false;

			int i = 0;
			while (i < m_queuedExplosions.Count)
			{
				ExplosionData explosionData = m_queuedExplosions[i];
				if (MathF.Abs((float)(explosionData.X - x)) <= 4f && MathF.Abs((float)(explosionData.Y - y)) <= 4f && MathF.Abs((float)(explosionData.Z - z)) <= 4f)
				{
					m_queuedExplosions.RemoveAt(i);
					SimulatePoisonCloud(explosionData.X, explosionData.Y, explosionData.Z, explosionData.Pressure);
					anyExploded = true;
				}
				else
				{
					i++;
				}
			}

			if (anyExploded)
			{
				PostprocessPoisonExplosions();
			}

			m_pressureByPoint = null;
		}

		private void SimulatePoisonCloud(int x, int y, int z, float pressure)
		{
			SparseSpatialArray<bool> processed = new SparseSpatialArray<bool>(x, y, z, true);
			List<ProcessPoint> list = new List<ProcessPoint>();
			List<ProcessPoint> list2 = new List<ProcessPoint>();
			List<ProcessPoint> list3 = new List<ProcessPoint>();

			TryAddPoisonPoint(x, y, z, -1, pressure, list, processed);

			int num2 = 0;
			int num3 = 0;
			float minPressure = MathUtils.Max(0.13f * MathF.Pow(pressure, 0.5f), 1f);

			while (list.Count > 0 || list2.Count > 0)
			{
				num2 += list.Count;
				num3++;
				float num4 = 5f * (float)MathUtils.Max(num3 - 7, 0);
				float currentWavePressure = pressure / (MathF.Pow((float)num2, 0.66f) + num4);

				if (currentWavePressure >= minPressure)
				{
					foreach (ProcessPoint processPoint in list)
					{
						if (processPoint.Axis == 0)
						{
							TryAddPoisonPoint(processPoint.X - 1, processPoint.Y, processPoint.Z, 0, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X + 1, processPoint.Y, processPoint.Z, 0, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y - 1, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y + 1, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z - 1, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z + 1, -1, currentWavePressure, list2, processed);
						}
						else if (processPoint.Axis == 1)
						{
							TryAddPoisonPoint(processPoint.X - 1, processPoint.Y, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X + 1, processPoint.Y, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y - 1, processPoint.Z, 1, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y + 1, processPoint.Z, 1, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z - 1, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z + 1, -1, currentWavePressure, list2, processed);
						}
						else if (processPoint.Axis == 2)
						{
							TryAddPoisonPoint(processPoint.X - 1, processPoint.Y, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X + 1, processPoint.Y, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y - 1, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y + 1, processPoint.Z, -1, currentWavePressure, list2, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z - 1, 2, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z + 1, 2, currentWavePressure, list3, processed);
						}
						else
						{
							TryAddPoisonPoint(processPoint.X - 1, processPoint.Y, processPoint.Z, 0, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X + 1, processPoint.Y, processPoint.Z, 0, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y - 1, processPoint.Z, 1, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y + 1, processPoint.Z, 1, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z - 1, 2, currentWavePressure, list3, processed);
							TryAddPoisonPoint(processPoint.X, processPoint.Y, processPoint.Z + 1, 2, currentWavePressure, list3, processed);
						}
					}
				}
				List<ProcessPoint> list4 = list;
				list4.Clear();
				list = list2;
				list2 = list3;
				list3 = list4;
			}
		}

		private void TryAddPoisonPoint(int x, int y, int z, int axis, float currentPressure, List<ProcessPoint> toProcess, SparseSpatialArray<bool> processed)
		{
			if (processed.Get(x, y, z)) return;

			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (contents != 0)
			{
				Block block = BlocksManager.Blocks[contents];
				if (block.IsCollidable_(cellValue))
				{
					return;
				}
			}

			m_pressureByPoint.Set(x, y, z, MathUtils.Max(m_pressureByPoint.Get(x, y, z), currentPressure));

			toProcess.Add(new ProcessPoint { X = x, Y = y, Z = z, Axis = axis });
			processed.Set(x, y, z, true);
		}

		private void PostprocessPoisonExplosions()
		{
			Point3 closestPointToListener = Point3.Zero;
			float minDistance = float.MaxValue;
			float totalPressure = 0f;

			foreach (KeyValuePair<Point3, float> kvp in m_pressureByPoint.ToDictionary())
			{
				totalPressure += kvp.Value;

				float distToListener = m_subsystemAudio.CalculateListenerDistance(new Vector3(kvp.Key));
				if (distToListener < minDistance)
				{
					minDistance = distToListener;
					closestPointToListener = kvp.Key;
				}

				float strength = MathUtils.Saturate(kvp.Value / 10f);
				if (strength > 0.1f)
				{
					m_poisonParticleSystem.SetExplosionCell(kvp.Key, strength);
				}
			}

			// 2. INFECTAR A LAS CRIATURAS Y AL JUGADOR
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					Point3 p = Terrain.ToCell(body.Position);
					float poisonPressure = m_pressureByPoint.Get(p.X, p.Y, p.Z);

					if (poisonPressure > 0f)
					{
						// PRIMERO: Revisar si es una criatura con tu componente personalizado de veneno
						ComponentInfectedWithPoison poisonComp = creature.Entity.FindComponent<ComponentInfectedWithPoison>();
						if (poisonComp != null)
						{
							float intensity = MathUtils.Clamp(poisonPressure / 15f, 0.1f, 1f);
							poisonComp.TryInfect(intensity);
						}
						else
						{
							// SEGUNDO: Respetando el código original, si es el jugador usamos ComponentSickness
							ComponentPlayer player = creature as ComponentPlayer;
							if (player != null && player.ComponentSickness != null)
							{
								player.ComponentSickness.StartSickness();
							}
						}
					}
				}
			}

			// 3. SONIDO Y RUIDO 
			Vector3 position = new Vector3((float)closestPointToListener.X, (float)closestPointToListener.Y, (float)closestPointToListener.Z);
			float delay = m_subsystemAudio.CalculateDelay(minDistance);

			float volume = MathUtils.Clamp(totalPressure / 5000f, 0.5f, 1f);
			m_subsystemAudio.PlaySound("Audio/Explosion Smoke", volume, m_random.Float(-0.1f, 0.1f), position, 25f, delay);

			m_subsystemNoise.MakeNoise(position, 0.6f, 30f);
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);

			m_poisonParticleSystem = new PoisonExplosionParticleSystem();
			m_subsystemParticles.AddParticleSystem(m_poisonParticleSystem, false);
		}

		public struct ExplosionData
		{
			public int X;
			public int Y;
			public int Z;
			public float Pressure;
		}

		public struct ProcessPoint
		{
			public int X;
			public int Y;
			public int Z;
			public int Axis;
		}

		public class SparseSpatialArray<T>
		{
			public T[][] m_data;
			public int m_originX;
			public int m_originY;
			public int m_originZ;
			public T m_outside;

			public SparseSpatialArray(int centerX, int centerY, int centerZ, T outside)
			{
				m_data = new T[4096][];
				m_originX = centerX - 128;
				m_originY = centerY - 128;
				m_originZ = centerZ - 128;
				m_outside = outside;
			}

			public T Get(int x, int y, int z)
			{
				x -= m_originX;
				y -= m_originY;
				z -= m_originZ;
				if (x < 0 || x >= 256 || y < 0 || y >= 256 || z < 0 || z >= 256) return m_outside;

				int num = x >> 4;
				int num2 = y >> 4;
				int num3 = z >> 4;
				int num4 = num + (num2 << 4) + (num3 << 8);

				T[] array = m_data[num4];
				if (array != null)
				{
					int num5 = x & 15;
					int num6 = y & 15;
					int num7 = z & 15;
					int num8 = num5 + (num6 << 4) + (num7 << 8);
					return array[num8];
				}
				return default(T);
			}

			public void Set(int x, int y, int z, T value)
			{
				x -= m_originX;
				y -= m_originY;
				z -= m_originZ;
				if (x >= 0 && x < 256 && y >= 0 && y < 256 && z >= 0 && z < 256)
				{
					int num = x >> 4;
					int num2 = y >> 4;
					int num3 = z >> 4;
					int num4 = num + (num2 << 4) + (num3 << 8);

					T[] array = m_data[num4];
					if (array == null) { array = new T[4096]; m_data[num4] = array; }

					int num5 = x & 15;
					int num6 = y & 15;
					int num7 = z & 15;
					int num8 = num5 + (num6 << 4) + (num7 << 8);
					array[num8] = value;
				}
			}

			public Dictionary<Point3, T> ToDictionary()
			{
				Dictionary<Point3, T> dictionary = new Dictionary<Point3, T>();
				for (int i = 0; i < m_data.Length; i++)
				{
					T[] array = m_data[i];
					if (array != null)
					{
						int num = m_originX + ((i & 15) << 4);
						int num2 = m_originY + ((i >> 4 & 15) << 4);
						int num3 = m_originZ + ((i >> 8 & 15) << 4);
						for (int j = 0; j < array.Length; j++)
						{
							if (array[j] != null && !array[j].Equals(default(T)))
							{
								int num4 = j & 15;
								int num5 = (j >> 4) & 15;
								int num6 = (j >> 8) & 15;
								dictionary.Add(new Point3(num + num4, num2 + num5, num3 + num6), array[j]);
							}
						}
					}
				}
				return dictionary;
			}
		}
	}
}