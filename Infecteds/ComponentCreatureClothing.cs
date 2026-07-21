using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Engine.Serialization;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente de ropa para criaturas
	/// No hereda de ComponentClothing, implementa IInventory e IUpdateable
	/// </summary>
	public class ComponentCreatureClothing : Component, IUpdateable, IInventory
	{
		// ===== Propiedades estáticas =====
		public static ClothingSlot[] InnerSlotsOrder => m_innerSlotsOrderList.ToArray();
		public static ClothingSlot[] OuterSlotsOrder => m_outerSlotsOrderList.ToArray();

		// ===== Propiedades públicas =====
		public Texture2D InnerClothedTexture => m_innerClothedTexture;
		public Texture2D OuterClothedTexture => m_outerClothedTexture;

		public float Insulation { get; set; }
		public ClothingSlot LeastInsulatedSlot { get; set; }
		public float SteedMovementSpeedFactor { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ===== Implementación IInventory =====
		public Project Project => base.Project;
		public int SlotsCount => ClothingSlot.ClothingSlots.Count;
		public int VisibleSlotsCount { get => this.SlotsCount; set { } }
		public int ActiveSlotIndex { get => -1; set { } }

		// ===== Campos =====
		protected SubsystemGameInfo m_subsystemGameInfo;
		protected SubsystemParticles m_subsystemParticles;
		protected SubsystemAudio m_subsystemAudio;
		protected SubsystemTime m_subsystemTime;
		protected SubsystemTerrain m_subsystemTerrain;
		protected SubsystemPickables m_subsystemPickables;
		protected SubsystemModelsRenderer m_subsystemModelsRenderer;

		// Componentes de la criatura
		protected ComponentBody m_componentBody;
		protected ComponentCreatureModel m_componentCreatureModel;
		protected ComponentModel m_componentOuterClothingModel;
		protected ComponentHealth m_componentHealth;
		protected ComponentVitalStats m_componentVitalStats;
		protected ComponentLocomotion m_componentLocomotion;
		protected ComponentCreature m_componentCreature;

		// Texturas
		protected Texture2D m_baseTexture;
		protected string m_baseTextureName;
		protected RenderTarget2D m_innerClothedTexture;
		protected RenderTarget2D m_outerClothedTexture;
		protected PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();

		protected Random m_random = new Random();
		protected float m_densityModifierApplied;
		protected double? m_lastTotalElapsedGameTime;
		protected bool m_clothedTexturesValid;

		protected List<int> m_clothesList = new List<int>();
		protected Dictionary<ClothingSlot, List<int>> m_clothes = new Dictionary<ClothingSlot, List<int>>();
		protected Dictionary<ClothingSlot, float> m_insulationBySlots = new Dictionary<ClothingSlot, float>();

		protected static List<ClothingSlot> m_innerSlotsOrderList = new List<ClothingSlot>();
		protected static List<ClothingSlot> m_outerSlotsOrderList = new List<ClothingSlot>();

		// ===== Métodos públicos =====
		public virtual ReadOnlyList<int> GetClothes(ClothingSlot slot)
		{
			return new ReadOnlyList<int>(m_clothes[slot]);
		}

		public virtual void SetClothes(ClothingSlot slot, IEnumerable<int> clothes)
		{
			var enumerable = (clothes as List<int>) ?? clothes.ToList();
			if (!m_clothes[slot].SequenceEqual(enumerable))
			{
				m_clothes[slot].Clear();
				m_clothes[slot].AddRange(enumerable);
				m_clothedTexturesValid = false;

				float oldDensityMod = m_densityModifierApplied;
				m_densityModifierApplied = 0f;
				SteedMovementSpeedFactor = 1f;

				// Resetear aislamiento base
				foreach (ClothingSlot cs in ClothingSlot.ClothingSlots.Values)
				{
					m_insulationBySlots[cs] = cs.BasicInsulation;
				}

				// Aplicar estadísticas de cada prenda
				foreach (var kvp in m_clothes)
				{
					foreach (int value in kvp.Value)
					{
						ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
						if (data != null)
						{
							m_insulationBySlots[data.Slot] += data.Insulation;
							SteedMovementSpeedFactor *= data.SteedMovementSpeedFactor;
							m_densityModifierApplied += data.DensityModifier;
						}
					}
				}

				if (m_componentBody != null)
				{
					m_componentBody.Density += (m_densityModifierApplied - oldDensityMod);
				}

				CalculateInsulationFromSlots();
			}
		}

		public float ApplyArmorProtection(Attackment attackment)
		{
			float remainingPower = attackment.AttackPower;

			float r = m_random.Float(0f, 1f);
			ClothingSlot slot = (r < 0.1f) ? ClothingSlot.Feet :
								(r < 0.3f) ? ClothingSlot.Legs :
								(r < 0.9f) ? ClothingSlot.Torso :
								ClothingSlot.Head;

			List<int> before = new List<int>(GetClothes(slot));
			List<int> after = new List<int>(before);

			for (int i = 0; i < before.Count; i++)
			{
				int val = before[i];
				ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetClothingData(val);
				if (data == null) continue;

				try
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(val)];
					float maxDurability = block.GetDurability(val) + 1;
					float durabilityFactor = (maxDurability - block.GetDamage(val)) / maxDurability * data.Sturdiness;
					float reduction = MathF.Min(remainingPower * MathUtils.Saturate(data.ArmorProtection / attackment.ArmorProtectionDivision), durabilityFactor);

					if (reduction > 0f)
					{
						remainingPower -= reduction;
						if (m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
						{
							float damageAmount = reduction / data.Sturdiness * maxDurability + 0.001f;
							int damageCount = (int)MathF.Floor(damageAmount) + (m_random.Bool(MathUtils.Remainder(damageAmount, 1f)) ? 1 : 0);
							after[i] = BlocksManager.DamageItem(val, damageCount, base.Entity);
							if (!BlocksManager.Blocks[Terrain.ExtractContents(after[i])].CanWear(after[i]))
							{
								m_subsystemParticles?.AddParticleSystem(
									new BlockDebrisParticleSystem(m_subsystemTerrain,
										m_componentBody.Position + m_componentBody.StanceBoxSize / 2f,
										1f, 1f, Color.White, 0), false);
							}
						}
						if (!string.IsNullOrEmpty(data.ImpactSoundsFolder))
						{
							m_subsystemAudio?.PlayRandomSound(data.ImpactSoundsFolder, 1f,
								m_random.Float(-0.3f, 0.3f),
								m_componentBody.Position, 4f, 0.15f);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error($"Error en protección de {data.DisplayName}: {ex}");
				}
			}

			int j = 0;
			while (j < after.Count)
			{
				if (!BlocksManager.Blocks[Terrain.ExtractContents(after[j])].CanWear(after[j]))
					after.RemoveAt(j);
				else j++;
			}

			after.Sort((a, b) =>
			{
				var da = BlocksManager.Blocks[Terrain.ExtractContents(a)].GetClothingData(a);
				var db = BlocksManager.Blocks[Terrain.ExtractContents(b)].GetClothingData(b);
				return ((da != null) ? da.Layer : 0) - ((db != null) ? db.Layer : 0);
			});

			SetClothes(slot, after);
			return MathF.Max(remainingPower, 0f);
		}

		public float CalculateInsulationFromSlots()
		{
			float sum = 0f;
			float minIns = float.MaxValue;
			int leastId = 0;
			int count = Math.Min(SlotsCount, m_insulationBySlots.Count);
			for (int i = 0; i < count; i++)
			{
				float ins = m_insulationBySlots[(ClothingSlot)i];
				sum += 1f / ins;
				if (ins < minIns) { minIns = ins; leastId = i; }
			}
			Insulation = 1f / sum;
			LeastInsulatedSlot = leastId;
			return Insulation;
		}

		// ===== Implementación IInventory =====
		public virtual int GetSlotValue(int slotIndex) => GetClothes(slotIndex).LastOrDefault();
		public virtual int GetSlotCount(int slotIndex) => GetClothes(slotIndex).Count > 0 ? 1 : 0;
		public virtual int GetSlotCapacity(int slotIndex, int value) => 0;

		public virtual int GetSlotProcessCapacity(int slotIndex, int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (block.GetNutritionalValue(value) > 0f) return 1;
			if (block.CanWear(value) && CanWearClothing(value)) return 1;
			return 0;
		}

		public virtual void AddSlotItems(int slotIndex, int value, int count) { }

		public virtual void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = 0;
			processedCount = 0;
			if (processCount != 1) return;

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];

			if (block.GetNutritionalValue(value) > 0f)
			{
				if (block is BucketBlock)
				{
					processedValue = EmptyBucketBlock.Index;
					processedCount = 1;
				}
				else if (m_componentVitalStats != null)
				{
					if (m_componentVitalStats.Eat(value))
					{
						processedValue = value;
						processedCount = processCount;
					}
				}
			}

			if (block.CanWear(value))
			{
				ClothingData data = block.GetClothingData(value);
				if (data != null)
				{
					List<int> newList = new List<int>(GetClothes(data.Slot)) { value };
					SetClothes(data.Slot, newList);
					processedValue = value;
					processedCount = 1;
				}
			}
		}

		public virtual int RemoveSlotItems(int slotIndex, int count)
		{
			if (count != 1) return 0;
			List<int> list = new List<int>(GetClothes(slotIndex));
			if (list.Count == 0) return 0;
			list.RemoveAt(list.Count - 1);
			SetClothes(slotIndex, list);
			return 1;
		}

		public virtual void DropAllItems(Vector3 position)
		{
			if (m_subsystemPickables == null) return;
			Random rand = new Random();
			for (int i = 0; i < SlotsCount; i++)
			{
				int cnt = GetSlotCount(i);
				if (cnt > 0)
				{
					int val = GetSlotValue(i);
					int removed = RemoveSlotItems(i, cnt);
					Vector3 vel = rand.Float(5f, 10f) * Vector3.Normalize(new Vector3(rand.Float(-1f, 1f), rand.Float(1f, 2f), rand.Float(-1f, 1f)));
					m_subsystemPickables.AddPickable(val, removed, position, vel, null, base.Entity);
				}
			}
		}

		// ===== Renderizado de texturas =====
		public virtual void UpdateRenderTargets()
		{
			if (m_baseTexture == null || m_componentCreatureModel == null)
				return;

			if (m_innerClothedTexture == null || m_innerClothedTexture.Width != m_baseTexture.Width || m_innerClothedTexture.Height != m_baseTexture.Height)
			{
				Utilities.Dispose(ref m_innerClothedTexture);
				m_innerClothedTexture = new RenderTarget2D(m_baseTexture.Width, m_baseTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_componentCreatureModel.TextureOverride = m_innerClothedTexture;
				m_clothedTexturesValid = false;
			}

			if (m_outerClothedTexture == null || m_outerClothedTexture.Width != m_baseTexture.Width || m_outerClothedTexture.Height != m_baseTexture.Height)
			{
				Utilities.Dispose(ref m_outerClothedTexture);
				m_outerClothedTexture = new RenderTarget2D(m_baseTexture.Width, m_baseTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				if (m_componentOuterClothingModel != null)
					m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;
				m_clothedTexturesValid = false;
			}

			if (!m_clothedTexturesValid)
			{
				m_clothedTexturesValid = true;
				Rectangle scissor = Display.ScissorRectangle;
				RenderTarget2D prevTarget = Display.RenderTarget;

				try
				{
					// ---- Inner ----
					Display.RenderTarget = m_innerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);

					int batchIndex = 0;
					var batch = m_primitivesRenderer.TexturedBatch(m_baseTexture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
					batch.QueueQuad(Vector2.Zero, new Vector2(m_innerClothedTexture.Width, m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);

					foreach (ClothingSlot slot in InnerSlotsOrder)
					{
						foreach (int val in GetClothes(slot))
						{
							ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetClothingData(val);
							if (data != null && !data.IsOuter)
							{
								if (data.Texture == null)
									data.Texture = ContentManager.Get<Texture2D>(data._textureName);

								Color color = GetClothingColor(data, val);
								batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
								batch.QueueQuad(Vector2.Zero, new Vector2(m_innerClothedTexture.Width, m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
							}
						}
					}
					m_primitivesRenderer.Flush(true, int.MaxValue);

					// ---- Outer ----
					if (m_componentOuterClothingModel != null)
					{
						Display.RenderTarget = m_outerClothedTexture;
						Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
						batchIndex = 0;

						foreach (ClothingSlot slot in OuterSlotsOrder)
						{
							foreach (int val in GetClothes(slot))
							{
								ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetClothingData(val);
								if (data != null && data.IsOuter)
								{
									if (data.Texture == null)
										data.Texture = ContentManager.Get<Texture2D>(data._textureName);

									Color color = GetClothingColor(data, val);
									batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
									batch.QueueQuad(Vector2.Zero, new Vector2(m_outerClothedTexture.Width, m_outerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
								}
							}
						}
						m_primitivesRenderer.Flush(true, int.MaxValue);
					}
				}
				finally
				{
					Display.RenderTarget = prevTarget;
					Display.ScissorRectangle = scissor;
				}
			}
		}

		private Color GetClothingColor(ClothingData data, int value)
		{
			int dataVal = Terrain.ExtractData(value);
			return SubsystemPalette.GetFabricColor(m_subsystemTerrain, new int?(ClothingBlock.GetClothingColor(dataVal)));
		}

		// ===== Métodos virtuales =====
		public virtual bool CanWearClothing(int value)
		{
			ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
			if (data == null) return false;
			var list = GetClothes(data.Slot);
			if (list.Count == 0) return true;
			int lastVal = list[list.Count - 1];
			ClothingData lastData = BlocksManager.Blocks[Terrain.ExtractContents(lastVal)].GetClothingData(lastVal);
			return lastData != null && data.Layer > lastData.Layer;
		}

		// ===== Ciclo de vida =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_innerSlotsOrderList.Clear();
			m_innerSlotsOrderList.AddRange(ClothingSlot.ClothingSlots.Values);
			m_innerSlotsOrderList.Reverse(2, 2);
			m_outerSlotsOrderList.Clear();
			m_outerSlotsOrderList.AddRange(ClothingSlot.ClothingSlots.Values);

			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);

			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentOuterClothingModel = Entity.FindComponent<ComponentModel>(false);
			if (m_componentOuterClothingModel == m_componentCreatureModel)
				m_componentOuterClothingModel = null;
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentVitalStats = Entity.FindComponent<ComponentVitalStats>(false);
			m_componentLocomotion = Entity.FindComponent<ComponentLocomotion>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			m_baseTextureName = valuesDictionary.GetValue<string>("BaseTextureName", null);
			if (!string.IsNullOrEmpty(m_baseTextureName))
				m_baseTexture = ContentManager.Get<Texture2D>(m_baseTextureName);

			SteedMovementSpeedFactor = 1f;
			Insulation = 0f;
			LeastInsulatedSlot = ClothingSlot.Feet;

			foreach (ClothingSlot slot in m_innerSlotsOrderList)
				m_clothes[slot] = new List<int>();

			ValuesDictionary clothesDict = valuesDictionary.GetValue<ValuesDictionary>("Clothes");
			if (clothesDict != null)
			{
				foreach (string key in ClothingSlot.ClothingSlots.Keys)
				{
					string val = clothesDict.GetValue<string>(key);
					if (!string.IsNullOrEmpty(val))
					{
						SetClothes(ClothingSlot.ClothingSlots[key],
							HumanReadableConverter.ValuesListFromString<int>(';', val));
					}
				}
			}

			Display.DeviceReset += OnDeviceReset;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			ValuesDictionary clothesDict = new ValuesDictionary();
			valuesDictionary.SetValue("Clothes", clothesDict);
			foreach (string key in ClothingSlot.ClothingSlots.Keys)
			{
				var list = m_clothes[ClothingSlot.ClothingSlots[key]];
				clothesDict.SetValue(key, HumanReadableConverter.ValuesListToString(';', list.ToArray()));
			}
			if (!string.IsNullOrEmpty(m_baseTextureName))
				valuesDictionary.SetValue("BaseTextureName", m_baseTextureName);
		}

		public override void Dispose()
		{
			base.Dispose();
			if (m_baseTexture != null && !ContentManager.IsContent(m_baseTexture))
				m_baseTexture.Dispose();
			Utilities.Dispose(ref m_innerClothedTexture);
			Utilities.Dispose(ref m_outerClothedTexture);
			Display.DeviceReset -= OnDeviceReset;
		}

		private void OnDeviceReset()
		{
			m_clothedTexturesValid = false;
		}

		public virtual void Update(float dt)
		{
			// Desgaste gradual
			if (m_subsystemGameInfo != null &&
				m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
				m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled &&
				m_subsystemTime != null &&
				m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
			{
				bool moving = (m_componentLocomotion != null) &&
					((m_componentLocomotion.LastWalkOrder != null && m_componentLocomotion.LastWalkOrder.Value != Vector2.Zero) ||
					 (m_componentLocomotion.LastSwimOrder != null && m_componentLocomotion.LastSwimOrder.Value != Vector3.Zero) ||
					 m_componentLocomotion.LastJumpOrder != 0f);

				if (moving && m_lastTotalElapsedGameTime != null)
				{
					foreach (ClothingSlot slot in ClothingSlot.ClothingSlots.Values)
					{
						bool changed = false;
						m_clothesList.Clear();
						m_clothesList.AddRange(GetClothes(slot));

						for (int i = 0; i < m_clothesList.Count; i++)
						{
							int val = m_clothesList[i];
							ClothingData data = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetClothingData(val);
							if (data != null)
							{
								float wetness = (m_componentVitalStats != null) ? m_componentVitalStats.Wetness : 0f;
								float interval = (wetness > 0f) ? (10f * data.Sturdiness) : (20f * data.Sturdiness);
								double lastCycle = Math.Floor(m_lastTotalElapsedGameTime.Value / interval);
								double currentCycle = Math.Floor(m_subsystemGameInfo.TotalElapsedGameTime / interval);
								if (currentCycle > lastCycle && m_random.Float(0f, 1f) < 0.75f)
								{
									int damaged = BlocksManager.DamageItem(val, 1, base.Entity);
									m_clothesList[i] = damaged;
									if (!BlocksManager.Blocks[Terrain.ExtractContents(damaged)].CanWear(damaged))
									{
										m_subsystemParticles?.AddParticleSystem(
											new BlockDebrisParticleSystem(m_subsystemTerrain,
												m_componentBody.Position + m_componentBody.StanceBoxSize / 2f,
												1f, 1f, Color.White, 0), false);
									}
									changed = true;
								}
							}
						}

						int j = 0;
						while (j < m_clothesList.Count)
						{
							if (!BlocksManager.Blocks[Terrain.ExtractContents(m_clothesList[j])].CanWear(m_clothesList[j]))
							{
								m_clothesList.RemoveAt(j);
								changed = true;
							}
							else j++;
						}

						if (changed) SetClothes(slot, m_clothesList);
					}
				}
				m_lastTotalElapsedGameTime = m_subsystemGameInfo.TotalElapsedGameTime;
			}

			UpdateRenderTargets();
		}

		protected virtual ClothingSlot GetSlotFromIndex(int index)
		{
			if (index >= 0 && index < ClothingSlot.ClothingSlots.Count)
				return (ClothingSlot)index;
			return ClothingSlot.Feet;
		}

		protected virtual ClothingSlot GetRandomSlot()
		{
			float r = m_random.Float(0f, 1f);
			return (r < 0.1f) ? ClothingSlot.Feet :
				   (r < 0.3f) ? ClothingSlot.Legs :
				   (r < 0.9f) ? ClothingSlot.Torso :
				   ClothingSlot.Head;
		}
	}
}
