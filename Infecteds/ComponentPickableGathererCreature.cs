using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Component that allows a creature to automatically pick up nearby items of certain categories
	/// and store them in its inventory, without moving towards the items.
	/// The creature will collect items within a range of 1.75 blocks (the same as the player's attraction range),
	/// even if they are still in the air and without any delay.
	/// </summary>
	public class ComponentPickableGathererCreature : Component, IUpdateable
	{
		// IUpdateable implementation
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Subsystems
		private SubsystemPickables m_subsystemPickables;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;

		// Required components
		private ComponentBody m_componentBody;
		private ComponentHealth m_componentHealth;
		private IInventory m_inventory;

		// Configuration fields (loaded from template)
		private List<string> m_categoriesOfInterest = new List<string>();
		private bool m_canPickUp = false;

		/// <summary>
		/// Gets the creature's position (from its body component).
		/// </summary>
		public Vector3 Position => m_componentBody != null ? m_componentBody.Position : Vector3.Zero;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Get required subsystems
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

			// Get required entity components
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);

			// Find the creature's inventory. 
			// We look for ComponentInventoryBase (which implements IInventory).
			var inventoryBase = Entity.FindComponent<ComponentInventoryBase>();
			if (inventoryBase != null)
			{
				m_inventory = inventoryBase;
			}
			else
			{
				Log.Warning("ComponentPickableGathererCreature: No inventory found on entity. Component will be disabled.");
				return;
			}

			// Parse categories of interest from a comma-separated string.
			string categoriesString = valuesDictionary.GetValue<string>("CategoriesOfInterest", string.Empty);
			if (!string.IsNullOrEmpty(categoriesString))
			{
				string[] parts = categoriesString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string part in parts)
				{
					string trimmed = part.Trim();
					if (!string.IsNullOrEmpty(trimmed))
						m_categoriesOfInterest.Add(trimmed);
				}
			}

			// Read the boolean that enables/disables pickup behavior
			m_canPickUp = valuesDictionary.GetValue<bool>("CanPickUp", false);
		}

		/// <summary>
		/// Determines whether this creature can gather the given pickable.
		/// </summary>
		public virtual bool CanGatherPickable(Pickable pickable)
		{
			// Creature must be alive
			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
				return false;

			// Item must not already be flying to a gatherer
			if (pickable.FlyToPosition != null)
				return false;

			// Item must not be marked for removal
			if (pickable.ToRemove)
				return false;

			// No time delay: pick up immediately when in range

			// Check category interest (if list is not empty, item must belong to one of the listed categories)
			if (m_categoriesOfInterest.Count > 0)
			{
				int contents = Terrain.ExtractContents(pickable.Value);
				Block block = BlocksManager.Blocks[contents];
				string category = block.GetCategory(pickable.Value);
				if (!m_categoriesOfInterest.Contains(category))
					return false;
			}

			// Check if there is room in the inventory for this item
			if (ComponentInventoryBase.FindAcquireSlotForItem(m_inventory, pickable.Value) < 0)
				return false;

			return true;
		}

		/// <summary>
		/// Performs the actual gathering: adds the item to inventory and removes the pickable if fully collected.
		/// </summary>
		public virtual void GatherPickable(Pickable pickable)
		{
			// Directly add items to inventory (no block behavior calls)
			pickable.Count = ComponentInventoryBase.AcquireItems(m_inventory, pickable.Value, pickable.Count);
			if (pickable.Count == 0)
			{
				pickable.ToRemove = true;
				// Play a simple collection sound
				m_subsystemAudio.PlaySound("Audio/PickableCollected", 0.7f, -0.4f, this.Position, 2f, false);
			}
		}

		/// <summary>
		/// Updates the component: scans for pickables in range and gathers them if possible.
		/// Uses the "fly to target" distance (1.75 blocks) so items can be picked up even while falling,
		/// and without any delay after they are created.
		/// </summary>
		public virtual void Update(float dt)
		{
			// If disabled or cannot pick up, do nothing
			if (!m_canPickUp)
				return;
			if (m_inventory == null || m_componentBody == null)
				return;

			// Iterate over all pickables in the subsystem
			for (int i = 0; i < m_subsystemPickables.Pickables.Count; i++)
			{
				Pickable pickable = m_subsystemPickables.Pickables[i];
				float distanceSq = (this.Position - pickable.Position).LengthSquared();

				// Use the same range as the player's attraction range (1.75 blocks)
				// This allows picking up items even when they are in mid‑air.
				if (distanceSq < pickable.DistanceToFlyToTarget * pickable.DistanceToFlyToTarget)
				{
					if (CanGatherPickable(pickable))
					{
						// Lock the pickable to avoid concurrency issues
						lock (pickable)
						{
							GatherPickable(pickable);
							if (!pickable.ToRemove && pickable.Count == 0)
							{
								pickable.ToRemove = true;
							}
						}
					}
				}
			}
		}
	}
}
