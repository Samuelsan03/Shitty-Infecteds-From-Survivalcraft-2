using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFrozenExplosions : Subsystem, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public bool TryExplodeBlock(int x, int y, int z, int value)
		{
			int num = Terrain.ExtractContents(value);
			Block block = BlocksManager.Blocks[num];
			float explosionPressure = block.GetExplosionPressure(value);
			bool explosionIncendiary = block.GetExplosionIncendiary(value);
			if (explosionPressure > 0f)
			{
				this.AddExplosion(x, y, z, explosionPressure, explosionIncendiary, false);
				return true;
			}
			return false;
		}

		public void AddExplosion(int x, int y, int z, float pressure, bool isIncendiary, bool noExplosionSound)
		{
			if (pressure > 0f)
			{
				this.m_queuedExplosions.Add(new ExplosionData
				{
					X = x,
					Y = y,
					Z = z,
					Pressure = pressure,
					IsIncendiary = isIncendiary,
					NoExplosionSound = noExplosionSound
				});
			}
		}

		public virtual void Update(float dt)
		{
			if (this.m_queuedExplosions.Count <= 0)
				return;

			int x = this.m_queuedExplosions[0].X;
			int y = this.m_queuedExplosions[0].Y;
			int z = this.m_queuedExplosions[0].Z;
			this.m_pressureByPoint = new SparseSpatialArray<float>(x, y, z, 0f);
			bool playSound = false;

			int i = 0;
			while (i < this.m_queuedExplosions.Count)
			{
				ExplosionData explosionData = this.m_queuedExplosions[i];
				if (MathF.Abs((float)(explosionData.X - x)) <= 4f && MathF.Abs((float)(explosionData.Y - y)) <= 4f && MathF.Abs((float)(explosionData.Z - z)) <= 4f)
				{
					this.m_queuedExplosions.RemoveAt(i);
					this.SimulateExplosion(explosionData.X, explosionData.Y, explosionData.Z, explosionData.Pressure);
					playSound |= !explosionData.NoExplosionSound;
				}
				else
				{
					i++;
				}
			}

			this.PostprocessExplosions(playSound);
		}

		public void SimulateExplosion(int x, int y, int z, float pressure)
		{
			float threshold = MathUtils.Max(0.13f * MathF.Pow(pressure, 0.5f), 1f);
			SparseSpatialArray<bool> processed = new SparseSpatialArray<bool>(x, y, z, true);
			List<ProcessPoint> current = new List<ProcessPoint>();
			List<ProcessPoint> next = new List<ProcessPoint>();
			List<ProcessPoint> temp = new List<ProcessPoint>();

			this.TryAddPoint(x, y, z, -1, pressure, current, processed);

			int totalPoints = 0;
			int wave = 0;

			while (current.Count > 0 || next.Count > 0)
			{
				totalPoints += current.Count;
				wave++;
				float waveAttenuation = 5f * (float)MathUtils.Max(wave - 7, 0);
				float wavePressure = pressure / (MathF.Pow((float)totalPoints, 0.66f) + waveAttenuation);

				if (wavePressure >= threshold)
				{
					foreach (ProcessPoint point in current)
					{
						float existingPressure = this.m_pressureByPoint.Get(point.X, point.Y, point.Z);
						float combinedPressure = wavePressure + existingPressure;
						this.m_pressureByPoint.Set(point.X, point.Y, point.Z, combinedPressure);

						if (point.Axis == 0)
						{
							this.TryAddPoint(point.X - 1, point.Y, point.Z, 0, combinedPressure, temp, processed);
							this.TryAddPoint(point.X + 1, point.Y, point.Z, 0, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y - 1, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y + 1, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y, point.Z - 1, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y, point.Z + 1, -1, combinedPressure, next, processed);
						}
						else if (point.Axis == 1)
						{
							this.TryAddPoint(point.X - 1, point.Y, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X + 1, point.Y, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y - 1, point.Z, 1, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y + 1, point.Z, 1, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y, point.Z - 1, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y, point.Z + 1, -1, combinedPressure, next, processed);
						}
						else if (point.Axis == 2)
						{
							this.TryAddPoint(point.X - 1, point.Y, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X + 1, point.Y, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y - 1, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y + 1, point.Z, -1, combinedPressure, next, processed);
							this.TryAddPoint(point.X, point.Y, point.Z - 1, 2, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y, point.Z + 1, 2, combinedPressure, temp, processed);
						}
						else
						{
							this.TryAddPoint(point.X - 1, point.Y, point.Z, 0, combinedPressure, temp, processed);
							this.TryAddPoint(point.X + 1, point.Y, point.Z, 0, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y - 1, point.Z, 1, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y + 1, point.Z, 1, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y, point.Z - 1, 2, combinedPressure, temp, processed);
							this.TryAddPoint(point.X, point.Y, point.Z + 1, 2, combinedPressure, temp, processed);
						}
					}
				}

				List<ProcessPoint> swap = current;
				swap.Clear();
				current = next;
				next = temp;
				temp = swap;
			}
		}

		public void TryAddPoint(int x, int y, int z, int axis, float currentPressure, List<ProcessPoint> toProcess, SparseSpatialArray<bool> processed)
		{
			if (processed.Get(x, y, z))
				return;

			int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int contents = Terrain.ExtractContents(cellValue);

			if (contents != 0)
			{
				Block block = BlocksManager.Blocks[contents];
				float surroundingPressure = this.m_pressureByPoint.Get(x - 1, y, z) + this.m_pressureByPoint.Get(x + 1, y, z) + this.m_pressureByPoint.Get(x, y - 1, z) + this.m_pressureByPoint.Get(x, y + 1, z) + this.m_pressureByPoint.Get(x, y, z - 1) + this.m_pressureByPoint.Get(x, y, z + 1);

				if (block.IsCollidable_(cellValue))
				{
					int hash = (int)(MathUtils.Hash((uint)(x + 913 * y + 217546 * z)) % 100U);
					float resilienceMultiplier = MathUtils.Lerp(1f, 2f, (float)hash / 100f);
					if (hash % 8 == 0)
						resilienceMultiplier *= 3f;

					float resilience = MathUtils.Max(block.GetExplosionResilience(cellValue) * resilienceMultiplier, 1f);
					float effectivePressure = surroundingPressure / resilience;

					if (effectivePressure <= 1f)
						return;

					this.m_pressureByPoint.Set(x, y, z, surroundingPressure);
					return;
				}
			}

			toProcess.Add(new ProcessPoint { X = x, Y = y, Z = z, Axis = axis });
			processed.Set(x, y, z, true);
		}

		public virtual void PostprocessExplosions(bool playExplosionSound)
		{
			Point3 closestPoint = Point3.Zero;
			float minDistance = float.MaxValue;
			float totalPressure = 0f;

			foreach (KeyValuePair<Point3, float> kvp in this.m_pressureByPoint.ToDictionary())
			{
				totalPressure += kvp.Value;
				float distance = this.m_subsystemAudio.CalculateListenerDistance(new Vector3(kvp.Key));
				if (distance < minDistance)
				{
					minDistance = distance;
					closestPoint = kvp.Key;
				}

				float particleStrength = 0.001f * MathF.Pow(kvp.Value, 0.5f);
				float normalizedStrength = MathUtils.Saturate(kvp.Value / 15f - particleStrength) * this.m_random.Float(0.2f, 1f);
				if (normalizedStrength > 0.1f)
				{
					this.m_frozenExplosionParticleSystem.SetExplosionCell(kvp.Key, normalizedStrength);
				}
			}

			this.InfectCreaturesAndPlayers();

			Vector3 position = new Vector3((float)closestPoint.X, (float)closestPoint.Y, (float)closestPoint.Z);
			float delay = this.m_subsystemAudio.CalculateDelay(minDistance);

			if (playExplosionSound && totalPressure > 0f)
			{
				float volume = MathUtils.Clamp(totalPressure / 100000f, 0.5f, 1f);
				this.m_subsystemAudio.PlaySound("Audio/explosion congelante", volume, this.m_random.Float(-0.1f, 0.1f), position, 30f, delay);
			}

			this.m_subsystemNoise.MakeNoise(position, 1f, 40f);
			this.m_pressureByPoint = null;
		}

		private void InfectCreaturesAndPlayers()
		{
			if (this.m_pressureByPoint == null)
				return;

			foreach (ComponentBody body in this.m_subsystemBodies.Bodies)
			{
				Point3 cellPos = Terrain.ToCell(body.Position);
				float pressure = this.m_pressureByPoint.Get(cellPos.X, cellPos.Y, cellPos.Z);

				if (pressure > 1f)
				{
					float infectionIntensity = MathUtils.Saturate(pressure / 10f);

					ComponentCreatureFlu creatureFlu = body.Entity.FindComponent<ComponentCreatureFlu>();
					if (creatureFlu != null)
					{
						creatureFlu.TryInfect(infectionIntensity);
					}
					else
					{
						ComponentPlayer player = body.Entity.FindComponent<ComponentPlayer>();
						if (player?.ComponentFlu != null)
						{
							player.ComponentFlu.StartFlu();
						}
					}
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_frozenExplosionParticleSystem = new FrozenExplosionParticleSystem();
			this.m_subsystemParticles.AddParticleSystem(this.m_frozenExplosionParticleSystem, false);
		}

		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemBodies m_subsystemBodies;
		private FrozenExplosionParticleSystem m_frozenExplosionParticleSystem;
		private Random m_random = new Random();
		private List<ExplosionData> m_queuedExplosions = new List<ExplosionData>();
		private SparseSpatialArray<float> m_pressureByPoint;

		public class SparseSpatialArray<T>
		{
			private T[][] m_data;
			private int m_originX;
			private int m_originY;
			private int m_originZ;
			private T m_outside;

			public SparseSpatialArray(int centerX, int centerY, int centerZ, T outside)
			{
				this.m_data = new T[4096][];
				this.m_originX = centerX - 128;
				this.m_originY = centerY - 128;
				this.m_originZ = centerZ - 128;
				this.m_outside = outside;
			}

			public T Get(int x, int y, int z)
			{
				x -= this.m_originX;
				y -= this.m_originY;
				z -= this.m_originZ;
				if (x < 0 || x >= 256 || y < 0 || y >= 256 || z < 0 || z >= 256)
					return this.m_outside;

				int chunk = (x >> 4) + ((y >> 4) << 4) + ((z >> 4) << 8);
				T[] array = this.m_data[chunk];
				if (array == null)
					return default(T);

				int index = (x & 15) + ((y & 15) << 4) + ((z & 15) << 8);
				return array[index];
			}

			public void Set(int x, int y, int z, T value)
			{
				x -= this.m_originX;
				y -= this.m_originY;
				z -= this.m_originZ;
				if (x < 0 || x >= 256 || y < 0 || y >= 256 || z < 0 || z >= 256)
					return;

				int chunk = (x >> 4) + ((y >> 4) << 4) + ((z >> 4) << 8);
				T[] array = this.m_data[chunk];
				if (array == null)
				{
					array = new T[4096];
					this.m_data[chunk] = array;
				}

				int index = (x & 15) + ((y & 15) << 4) + ((z & 15) << 8);
				array[index] = value;
			}

			public Dictionary<Point3, T> ToDictionary()
			{
				Dictionary<Point3, T> dictionary = new Dictionary<Point3, T>();
				for (int i = 0; i < this.m_data.Length; i++)
				{
					T[] array = this.m_data[i];
					if (array == null)
						continue;

					int baseX = this.m_originX + ((i & 15) << 4);
					int baseY = this.m_originY + ((i >> 4 & 15) << 4);
					int baseZ = this.m_originZ + ((i >> 8 & 15) << 4);

					for (int j = 0; j < array.Length; j++)
					{
						if (!object.Equals(array[j], default(T)))
						{
							dictionary.Add(new Point3(baseX + (j & 15), baseY + ((j >> 4) & 15), baseZ + ((j >> 8) & 15)), array[j]);
						}
					}
				}
				return dictionary;
			}
		}

		public struct ExplosionData
		{
			public int X;
			public int Y;
			public int Z;
			public float Pressure;
			public bool IsIncendiary;
			public bool NoExplosionSound;
		}

		public struct ProcessPoint
		{
			public int X;
			public int Y;
			public int Z;
			public int Axis;
		}
	}
}