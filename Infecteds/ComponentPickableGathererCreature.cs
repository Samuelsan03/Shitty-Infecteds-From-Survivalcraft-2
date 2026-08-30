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
		// private bool m_lootInitialized = false;

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

		private void InitializeCreatureStartingLoot()
		{
			if (m_inventory == null) return;

			// Verificar si el inventario ya tiene algo (no es la primera vez)
			for (int i = 0; i < m_inventory.SlotsCount; i++)
			{
				if (m_inventory.GetSlotCount(i) > 0) return;
			}

			Random random = new Random();
			string creatureType = Entity.ValuesDictionary.DatabaseObject.Name;

			// ========== InfectedNormal1 ==========
			if (creatureType == "InfectedNormal1" || creatureType == "InfectedNormal2" || creatureType == "InfectedMuscle1" || creatureType == "InfectedMuscle2" || creatureType == "FatInfectedFrozen" || creatureType == "FatInfected" || creatureType == "FatInfectedArsonist" || creatureType == "FatInfectedPoisonous" || creatureType == "GhostNormal")
			{
				int[] rangedWeapons = {
			BlocksManager.GetBlockIndex("FlameThrowerBlock"),
			BlocksManager.GetBlockIndex("RepeatCrossbowBlock"),
			BlocksManager.GetBlockIndex("MusketBlock"),
			BlocksManager.GetBlockIndex("BowBlock"),
			BlocksManager.GetBlockIndex("CrossbowBlock")
		};

				int[] meleeWeapons = {
			BlocksManager.GetBlockIndex("CopperMacheteBlock"),
			BlocksManager.GetBlockIndex("IronMacheteBlock"),
			BlocksManager.GetBlockIndex("DiamondMacheteBlock"),
			BlocksManager.GetBlockIndex("WoodenClubBlock"),
			BlocksManager.GetBlockIndex("StoneClubBlock")
		};

				int[] throwableWeapons = {
			BlocksManager.GetBlockIndex("BombBlock"),
			BlocksManager.GetBlockIndex("IncendiaryBombBlock"),
			BlocksManager.GetBlockIndex("PoisonBombBlock")
		};

				int[] spearWeapons = {
			BlocksManager.GetBlockIndex("WoodenSpearBlock"),
			BlocksManager.GetBlockIndex("StoneSpearBlock"),
			BlocksManager.GetBlockIndex("IronSpearBlock"),
			BlocksManager.GetBlockIndex("CopperSpearBlock"),
			BlocksManager.GetBlockIndex("DiamondSpearBlock"),
			BlocksManager.GetBlockIndex("WoodenLongspearBlock"),
			BlocksManager.GetBlockIndex("StoneLongspearBlock"),
			BlocksManager.GetBlockIndex("IronLongspearBlock"),
			BlocksManager.GetBlockIndex("CopperLongspearBlock"),
			BlocksManager.GetBlockIndex("DiamondLongspearBlock"),
			BlocksManager.GetBlockIndex("LavaLongspearBlock")
		};

				int roll = random.Int(0, 99);

				if (roll <= 24)
				{
					// Solo arma a distancia (25%)
					int weapon = rangedWeapons[random.Int(0, rangedWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, weapon, 1);
				}
				else if (roll <= 49)
				{
					// Solo arma cuerpo a cuerpo (25%)
					int weapon = meleeWeapons[random.Int(0, meleeWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, weapon, 1);
				}
				else if (roll <= 69)
				{
					// Arma a distancia + UN lanzable (20%)
					int ranged = rangedWeapons[random.Int(0, rangedWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, ranged, 1);

					int throwable = throwableWeapons[random.Int(0, throwableWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, throwable, 5);
				}
				else if (roll <= 89)
				{
					// Arma cuerpo a cuerpo + UN lanzable (20%)
					int melee = meleeWeapons[random.Int(0, meleeWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, melee, 1);

					int throwable = throwableWeapons[random.Int(0, throwableWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, throwable, 5);
				}
				else if (roll <= 94)
				{
					// UNA sola lanza (5%)
					int spear = spearWeapons[random.Int(0, spearWeapons.Length - 1)];
					ComponentInventoryBase.AcquireItems(m_inventory, spear, 1);
				}
				// else roll 95-99: Inventario vacío (5%) - no se agrega nada
			}

			// ========== Espacio para futuras criaturas ==========
			// else if (creatureType == "NombreOtraCriatura")
			// {
			//     int[] armasEspecificas = { ... };
			//     int roll = random.Int(0, 99);
			//     // Lógica específica
			// }
		}

		/// <summary>
		/// Updates the component: scans for pickables in range and gathers them if possible.
		/// Uses the "fly to target" distance (1.75 blocks) so items can be picked up even while falling,
		/// and without any delay after they are created.
		/// </summary>
		public virtual void Update(float dt)
		{
			// if (!m_lootInitialized)
			// {
			//	m_lootInitialized = true;
			//	InitializeCreatureStartingLoot();
			// }

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
