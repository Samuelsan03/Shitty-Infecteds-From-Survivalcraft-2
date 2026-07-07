using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentGatherPickableBehavior : ComponentBehavior, IUpdateable
	{
		public float CollectionNeed
		{
			get
			{
				return this.m_collectionNeed;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public IInventory Inventory
		{
			get
			{
				if (this.m_inventory == null)
				{
					ComponentMiner componentMiner = base.Entity.FindComponent<ComponentMiner>();
					if (componentMiner != null)
					{
						this.m_inventory = componentMiner.Inventory;
					}
					if (this.m_inventory == null)
					{
						this.m_inventory = base.Entity.FindComponent<IInventory>();
					}
				}
				return this.m_inventory;
			}
		}

		public virtual void Update(float dt)
		{
			if (this.m_collectionNeed > 0f)
			{
				this.m_collectionNeed = MathUtils.Max(this.m_collectionNeed - 0.01f * this.m_subsystemTime.GameTimeDelta, 0f);
			}
			this.m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>();

			this.m_categoryFactors = new Dictionary<string, float>();
			ValuesDictionary categoryFactorsDict = valuesDictionary.GetValue<ValuesDictionary>("CategoryFactors");
			if (categoryFactorsDict != null)
			{
				foreach (KeyValuePair<string, object> keyValuePair in categoryFactorsDict)
				{
					this.m_categoryFactors[keyValuePair.Key] = (float)keyValuePair.Value;
				}
			}

			SubsystemPickables subsystemPickables = this.m_subsystemPickables;
			subsystemPickables.PickableAdded = (Action<Pickable>)Delegate.Combine(subsystemPickables.PickableAdded, new Action<Pickable>(delegate (Pickable pickable)
			{
				if (this.TryAddPickable(pickable) && this.m_pickable == null)
				{
					this.m_pickable = pickable;
				}
			}));
			SubsystemPickables subsystemPickables2 = this.m_subsystemPickables;
			subsystemPickables2.PickableRemoved = (Action<Pickable>)Delegate.Combine(subsystemPickables2.PickableRemoved, new Action<Pickable>(delegate (Pickable pickable)
			{
				this.m_pickables.Remove(pickable);
				if (this.m_pickable == pickable)
				{
					this.m_pickable = null;
				}
			}));

			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_pickable = null;
			}, delegate
			{
				if (this.m_collectionNeed < 1f && this.HasInventorySpace())
				{
					if (this.m_pickable == null)
					{
						if (this.m_subsystemTime.GameTime > this.m_nextFindPickableTime)
						{
							this.m_nextFindPickableTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(2f, 4f);
							this.m_pickable = this.FindPickable(this.m_componentCreature.ComponentBody.Position);
						}
					}
					else
					{
						this.m_importanceLevel = this.m_random.Float(5f, 10f);
					}
				}
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Move");
					this.m_blockedCount = 0;
				}
			}, null);

			this.m_stateMachine.AddState("Move", delegate
			{
				if (this.m_pickable != null)
				{
					float speed = (this.m_collectionNeed == 0f) ? this.m_random.Float(0.5f, 0.7f) : 0.5f;
					int maxPathfindingPositions = (this.m_collectionNeed == 0f) ? 1000 : 500;
					float num = Vector3.Distance(this.m_componentCreature.ComponentCreatureModel.EyePosition, this.m_componentCreature.ComponentBody.Position);
					this.m_componentPathfinding.SetDestination(new Vector3?(this.m_pickable.Position), speed, 1f + num, maxPathfindingPositions, true, false, true, null);
					if (this.m_random.Float(0f, 1f) < 0.66f)
					{
						this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
					}
				}
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Inactive");
				}
				else if (this.m_pickable == null)
				{
					this.m_importanceLevel = 0f;
				}
				else if (this.m_componentPathfinding.IsStuck)
				{
					this.m_importanceLevel = 0f;
					this.m_collectionNeed += 0.75f;
				}
				else if (this.m_componentPathfinding.Destination == null)
				{
					this.m_stateMachine.TransitionTo("Gather");
				}
				else if (Vector3.DistanceSquared(this.m_componentPathfinding.Destination.Value, this.m_pickable.Position) > 0.0625f)
				{
					this.m_stateMachine.TransitionTo("PickableMoved");
				}
				if (this.m_random.Float(0f, 1f) < 0.1f * this.m_subsystemTime.GameTimeDelta)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
				}
				if (this.m_pickable != null)
				{
					this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_pickable.Position);
					return;
				}
				this.m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
			}, null);

			this.m_stateMachine.AddState("PickableMoved", null, delegate
			{
				if (this.m_pickable != null)
				{
					this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_pickable.Position);
				}
				if (this.m_subsystemTime.PeriodicGameTimeEvent(0.25, (double)(this.GetHashCode() % 100) * 0.01))
				{
					this.m_stateMachine.TransitionTo("Move");
				}
			}, null);

			this.m_stateMachine.AddState("Gather", delegate
			{
				this.m_gatherTime = (double)this.m_random.Float(0.5f, 0.5f);
				this.m_blockedTime = 0f;
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Inactive");
				}
				if (this.m_pickable == null)
				{
					this.m_importanceLevel = 0f;
				}
				if (this.m_pickable != null)
				{
					Vector3 eyePos = this.m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 bodyPos = this.m_componentCreature.ComponentBody.Position;
					Vector3 checkPos = new Vector3(eyePos.X, bodyPos.Y, eyePos.Z);

					if (Vector3.DistanceSquared(checkPos, this.m_pickable.Position) < 0.64000005f)
					{
						this.m_gatherTime -= (double)this.m_subsystemTime.GameTimeDelta;
						this.m_blockedTime = 0f;

						if (this.m_gatherTime <= 0.0)
						{
							if (!this.HasInventorySpace())
							{
								this.m_importanceLevel = 0f;
							}
							else
							{
								if (this.m_componentMiner != null)
								{
									this.m_componentMiner.Poke(true);
								}
								this.PerformGather();
							}
						}
					}
					else
					{
						float num = Vector3.Distance(eyePos, bodyPos);
						this.m_componentPathfinding.SetDestination(new Vector3?(this.m_pickable.Position), 0.3f, 0.5f + num, 0, false, true, false, null);
						this.m_blockedTime += this.m_subsystemTime.GameTimeDelta;
					}
					if (this.m_blockedTime > 3f)
					{
						this.m_blockedCount++;
						if (this.m_blockedCount >= 3)
						{
							this.m_importanceLevel = 0f;
							this.m_collectionNeed += 0.75f;
						}
						else
						{
							this.m_stateMachine.TransitionTo("Move");
						}
					}
				}
				this.m_componentCreature.ComponentCreatureModel.FeedOrder = true;
				if (this.m_random.Float(0f, 1f) < 0.1f * this.m_subsystemTime.GameTimeDelta)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
				}
				if (this.m_random.Float(0f, 1f) < 1.5f * this.m_subsystemTime.GameTimeDelta)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
				}
			}, null);

			this.m_stateMachine.TransitionTo("Inactive");
		}

		public virtual bool HasInventorySpace()
		{
			IInventory inventory = this.Inventory;
			if (inventory == null)
			{
				return false;
			}
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) == 0)
				{
					return true;
				}
			}
			return false;
		}

		public virtual float GetCategoryFactor(string category)
		{
			float factor;
			if (this.m_categoryFactors != null && this.m_categoryFactors.TryGetValue(category, out factor))
			{
				return factor;
			}
			return 0f;
		}

		public virtual bool ShouldCollectPickable(Pickable pickable)
		{
			if (pickable == null || pickable.StuckMatrix != null)
			{
				return false;
			}

			int contents = Terrain.ExtractContents(pickable.Value);
			Block block = BlocksManager.Blocks[contents];
			string category = block.GetCategory(pickable.Value);

			return this.GetCategoryFactor(category) > 0f;
		}

		public virtual Pickable FindPickable(Vector3 position)
		{
			if (this.m_subsystemTime.GameTime > this.m_nextPickablesUpdateTime)
			{
				this.m_nextPickablesUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(2f, 4f);
				this.m_pickables.Clear();
				foreach (Pickable pickable in this.m_subsystemPickables.Pickables)
				{
					this.TryAddPickable(pickable);
				}
				if (this.m_pickable != null && !this.m_pickables.ContainsKey(this.m_pickable))
				{
					this.m_pickable = null;
				}
			}
			foreach (Pickable pickable2 in this.m_pickables.Keys)
			{
				float num = Vector3.DistanceSquared(position, pickable2.Position);
				if (this.m_random.Float(0f, 1f) > num / 256f)
				{
					return pickable2;
				}
			}
			return null;
		}

		public virtual bool TryAddPickable(Pickable pickable)
		{
			if (!this.ShouldCollectPickable(pickable))
			{
				return false;
			}

			if (Vector3.DistanceSquared(pickable.Position, this.m_componentCreature.ComponentBody.Position) < 256f)
			{
				this.m_pickables.Add(pickable, true);
				return true;
			}
			return false;
		}

		public virtual void PerformGather()
		{
			if (this.m_pickable == null)
			{
				return;
			}

			IInventory inventory = this.Inventory;
			if (inventory == null)
			{
				this.m_importanceLevel = 0f;
				return;
			}

			int value = this.m_pickable.Value;
			int availableCapacity = this.CalculateAvailableCapacity(inventory, value);

			if (availableCapacity <= 0)
			{
				this.m_importanceLevel = 0f;
				return;
			}

			int countToGather = MathUtils.Min(this.m_pickable.Count, availableCapacity);
			int actuallyGathered = this.AddItemsToInventory(inventory, value, countToGather);

			if (actuallyGathered > 0)
			{
				this.m_pickable.Count -= actuallyGathered;
				this.m_collectionNeed += 1f;

				if (this.m_pickable.Count == 0)
				{
					this.m_pickable.ToRemove = true;
					this.m_importanceLevel = 0f;
				}
				else if (this.m_random.Float(0f, 1f) < 0.5f)
				{
					this.m_importanceLevel = 0f;
				}
			}
			else
			{
				this.m_importanceLevel = 0f;
			}
		}

		public virtual int CalculateAvailableCapacity(IInventory inventory, int value)
		{
			int totalCapacity = 0;

			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int slotValue = inventory.GetSlotValue(i);
				int slotCount = inventory.GetSlotCount(i);

				if (slotValue == 0 || slotValue == value)
				{
					int maxCapacity = inventory.GetSlotCapacity(i, value);
					int available = maxCapacity - slotCount;
					totalCapacity += MathUtils.Max(available, 0);
				}
			}

			return totalCapacity;
		}

		protected virtual int AddItemsToInventory(IInventory inventory, int value, int count)
		{
			int remaining = count;

			for (int i = 0; i < inventory.SlotsCount && remaining > 0; i++)
			{
				if (inventory.GetSlotValue(i) == value)
				{
					int currentCount = inventory.GetSlotCount(i);
					int maxCapacity = inventory.GetSlotCapacity(i, value);
					int canAdd = MathUtils.Min(remaining, maxCapacity - currentCount);

					if (canAdd > 0)
					{
						inventory.AddSlotItems(i, value, canAdd);
						remaining -= canAdd;
					}
				}
			}

			for (int i = 0; i < inventory.SlotsCount && remaining > 0; i++)
			{
				if (inventory.GetSlotValue(i) == 0 && inventory.GetSlotCount(i) == 0)
				{
					int maxCapacity = inventory.GetSlotCapacity(i, value);
					int canAdd = MathUtils.Min(remaining, maxCapacity);

					if (canAdd > 0)
					{
						inventory.AddSlotItems(i, value, canAdd);
						remaining -= canAdd;
					}
				}
			}

			return count - remaining;
		}

		public SubsystemTime m_subsystemTime;
		public SubsystemPickables m_subsystemPickables;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;

		public StateMachine m_stateMachine = new StateMachine();

		public Dictionary<Pickable, bool> m_pickables = new Dictionary<Pickable, bool>();

		public Random m_random = new Random();

		public Dictionary<string, float> m_categoryFactors;

		public float m_importanceLevel;

		public double m_nextFindPickableTime;
		public double m_nextPickablesUpdateTime;

		public Pickable m_pickable;

		public double m_gatherTime;

		public float m_collectionNeed;

		public float m_blockedTime;

		public int m_blockedCount;

		public IInventory m_inventory;

		public const float m_range = 16f;
	}
}
