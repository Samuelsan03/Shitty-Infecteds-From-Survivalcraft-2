using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPickPickableBehavior : ComponentBehavior, IUpdateable
	{
		private SubsystemTime m_subsystemTime;
		private SubsystemPickables m_subsystemPickables;
		private Dictionary<Pickable, bool> m_pickables = new Dictionary<Pickable, bool>();
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private StateMachine m_stateMachine = new StateMachine();
		private Dictionary<string, float> m_pickFactors;
		private float m_importanceLevel;
		private double m_nextFindPickableTime;
		private double m_nextPickablesUpdateTime;
		private Pickable m_pickable;
		private double m_pickTime;
		private float m_satiation;
		private float m_blockedTime;
		private SubsystemAudio m_subsystemAudio;
		private int m_blockedCount;
		private ComponentMiner m_componentMiner;
		private Random m_random = new Random();

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override float ImportanceLevel => m_importanceLevel;

		public void Update(float dt)
		{
			if (m_satiation > 0f)
			{
				m_satiation = MathUtils.Max(m_satiation - 0.01f * m_subsystemTime.GameTimeDelta, 0f);
			}
			m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_pickFactors = new Dictionary<string, float>();

			foreach (KeyValuePair<string, object> pair in valuesDictionary.GetValue<ValuesDictionary>("PickFactors"))
			{
				if (BlocksManager.Categories.Contains(pair.Key))
				{
					m_pickFactors.Add(pair.Key, (float)pair.Value);
				}
				else
				{
					string translated = LanguageControl.Get(new string[] { "BlocksManager", pair.Key });
					if (BlocksManager.Categories.Contains(translated))
					{
						m_pickFactors.Add(translated, (float)pair.Value);
					}
				}
			}

			m_subsystemPickables.PickableAdded += OnPickableAdded;
			m_subsystemPickables.PickableRemoved += OnPickableRemoved;

			m_stateMachine.AddState("Inactive", Inactive_Enter, Inactive_Update, null);
			m_stateMachine.AddState("Move", Move_Enter, Move_Update, null);
			m_stateMachine.AddState("PickableMoved", null, PickableMoved_Update, null);
			m_stateMachine.AddState("Pick", Pick_Enter, Pick_Update, null);
			m_stateMachine.TransitionTo("Inactive");
		}

		public Pickable FindPickable(Vector3 position)
		{
			if (m_subsystemTime.GameTime > m_nextPickablesUpdateTime)
			{
				m_nextPickablesUpdateTime = m_subsystemTime.GameTime + m_random.Float(2f, 4f);
				m_pickables.Clear();
				foreach (Pickable p in m_subsystemPickables.Pickables)
				{
					TryAddPickable(p);
				}
				if (m_pickable != null && !m_pickables.ContainsKey(m_pickable))
				{
					m_pickable = null;
				}
			}

			foreach (Pickable p in m_pickables.Keys)
			{
				float num = Vector3.DistanceSquared(position, p.Position);
				// Probabilidad de seleccionar este pickable: cuanto más cerca, más probable
				float probability = 1f - (num / 256f);
				probability = MathUtils.Clamp(probability, 0f, 1f);
				if (m_random.Bool(probability))
				{
					return p;
				}
			}
			return null;
		}

		public bool TryAddPickable(Pickable pickable)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(pickable.Value)];
			string category = block.GetCategory(pickable.Value);
			if (m_pickFactors.ContainsKey(category) && m_pickFactors[category] > 0f &&
				Vector3.DistanceSquared(pickable.Position, m_componentCreature.ComponentBody.Position) < 256f)
			{
				m_pickables.Add(pickable, true);
				return true;
			}
			return false;
		}

		private void OnPickableAdded(Pickable pickable)
		{
			if (TryAddPickable(pickable) && m_pickable == null)
			{
				m_pickable = pickable;
			}
		}

		private void OnPickableRemoved(Pickable pickable)
		{
			m_pickables.Remove(pickable);
			if (m_pickable == pickable)
			{
				m_pickable = null;
			}
		}

		private void Inactive_Enter()
		{
			m_importanceLevel = 0f;
			m_pickable = null;
		}

		private void Inactive_Update()
		{
			if (m_satiation < 1f)
			{
				if (m_pickable == null)
				{
					if (m_subsystemTime.GameTime > m_nextFindPickableTime)
					{
						m_nextFindPickableTime = m_subsystemTime.GameTime + m_random.Float(2f, 4f);
						m_pickable = FindPickable(m_componentCreature.ComponentBody.Position);
					}
				}
				else
				{
					m_importanceLevel = m_random.Float(5f, 10f);
				}
			}

			if (IsActive)
			{
				m_stateMachine.TransitionTo("Move");
				m_blockedCount = 0;
			}
		}

		private void Move_Enter()
		{
			if (m_pickable != null)
			{
				float speed = (m_satiation == 0f) ? m_random.Float(0.5f, 0.7f) : 0.5f;
				int maxPathfindingPositions = (m_satiation == 0f) ? 1000 : 500;
				m_componentPathfinding.SetDestination(m_pickable.Position, speed, 2f, maxPathfindingPositions, true, false, true, null);

				if (m_random.Bool(0.66f))
				{
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
				}
			}
		}

		private void Move_Update()
		{
			if (!IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}
			else if (m_pickable == null)
			{
				m_importanceLevel = 0f;
			}
			else if (m_componentPathfinding.IsStuck)
			{
				m_importanceLevel = 0f;
			}
			else if (m_componentPathfinding.Destination == null)
			{
				m_stateMachine.TransitionTo("Pick");
			}
			else if (Vector3.DistanceSquared(m_componentPathfinding.Destination.Value, m_pickable.Position) > 0.0625f)
			{
				m_stateMachine.TransitionTo("PickableMoved");
			}

			if (m_random.Bool(0.1f * m_subsystemTime.GameTimeDelta))
			{
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
			}

			if (m_pickable != null)
			{
				m_componentCreature.ComponentCreatureModel.LookAtOrder = m_pickable.Position;
			}
			else
			{
				m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
			}
		}

		private void PickableMoved_Update()
		{
			if (m_pickable != null)
			{
				m_componentCreature.ComponentCreatureModel.LookAtOrder = m_pickable.Position;
			}

			if (m_subsystemTime.PeriodicGameTimeEvent(0.25, (GetHashCode() % 100) * 0.01))
			{
				m_stateMachine.TransitionTo("Move");
			}
		}

		private void Pick_Enter()
		{
			m_pickTime = m_random.Float(0.2f, 0.5f);
			m_blockedTime = 0f;
		}

		private void Pick_Update()
		{
			if (!IsActive)
			{
				m_stateMachine.TransitionTo("Inactive");
			}

			if (m_pickable == null)
			{
				m_importanceLevel = 0f;
				return;
			}

			Vector3 eyePos = new Vector3(
				m_componentCreature.ComponentCreatureModel.EyePosition.X,
				m_componentCreature.ComponentBody.Position.Y,
				m_componentCreature.ComponentCreatureModel.EyePosition.Z);

			if (Vector3.DistanceSquared(eyePos, m_pickable.Position) < 0.5625f)
			{
				m_pickTime -= m_subsystemTime.GameTimeDelta;
				m_blockedTime = 0f;

				if (m_pickTime <= 0.0)
				{
					int count = m_pickable.Count;
					m_pickable.Count = Pick(m_pickable);

					if (count == m_pickable.Count)
					{
						m_satiation += 1f;
					}

					if (m_pickable.Count == 0)
					{
						m_pickable.ToRemove = true;
						m_importanceLevel = 0f;
						m_subsystemAudio.PlaySound("Audio/PickableCollected", 0.7f, -0.4f, m_pickable.Position, 2f, false);
					}
					else if (m_random.Bool(0.5f))
					{
						m_importanceLevel = 0f;
					}
				}
			}
			else
			{
				m_componentPathfinding.SetDestination(m_pickable.Position, 0.4f, 1.5f, 0, false, true, false, null);
				m_blockedTime += m_subsystemTime.GameTimeDelta;

				if (m_blockedTime > 3f)
				{
					m_blockedCount++;
					if (m_blockedCount >= 3)
					{
						m_importanceLevel = 0f;
					}
					else
					{
						m_stateMachine.TransitionTo("Move");
					}
				}
			}

			m_componentCreature.ComponentCreatureModel.FeedOrder = true;

			if (m_random.Bool(0.1f * m_subsystemTime.GameTimeDelta))
			{
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
			}

			if (m_random.Bool(1.5f * m_subsystemTime.GameTimeDelta))
			{
				m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
			}
		}

		private int Pick(Pickable pickable)
		{
			IInventory inventory = m_componentMiner.Inventory;
			if (ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value) >= 0)
			{
				Entity.FindComponent<ComponentMiner>(true).Poke(false);
				pickable.Count = ComponentInventoryBase.AcquireItems(inventory, pickable.Value, pickable.Count);
			}
			return pickable.Count;
		}
	}
}
