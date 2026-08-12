using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBandageSmallBehavior : SubsystemBlockBehavior
	{
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private HashSet<int> m_partialHealEntities = new HashSet<int>();

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BlocksManager.GetBlock<BandageSmallBlock>().BlockIndex };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
		}

		private bool TryHeal(ComponentHealth health, int entityId, ComponentMiner componentMiner, string message, Vector3 soundPosition)
		{
			if (health.Health <= 0f || health.Health >= 1f)
				return false;

			float healAmount = 0f;

			if (m_partialHealEntities.Contains(entityId) && MathF.Abs(health.Health - 0.5f) < 0.01f)
			{
				healAmount = 0.5f;
				m_partialHealEntities.Remove(entityId);
			}
			else if (health.Health < 0.5f)
			{
				healAmount = 0.5f - health.Health;
				m_partialHealEntities.Add(entityId);
			}
			else
			{
				healAmount = 1f - health.Health;
			}

			if (healAmount <= 0f)
				return false;

			health.Heal(healAmount);
			componentMiner.Inventory?.RemoveSlotItems(componentMiner.Inventory.ActiveSlotIndex, 1);

			// false para silenciar el sonido por defecto (Audio/UI/Message)
			componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
				message,
				Color.Yellow,
				true,
				false
			);

			// Tu sonido personalizado
			m_subsystemAudio?.PlaySound("Audio/cured", 1f, 0f, soundPosition, 2f, false);

			return true;
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			if (componentMiner?.ComponentCreature == null)
				return false;

			float reach = 5f;
			Vector3 end = ray.Position + ray.Direction * reach;

			BodyRaycastResult? bodyRaycastResult = m_subsystemBodies.Raycast(ray.Position, end, 0.35f, (ComponentBody body, float dist) => body.Entity != componentMiner.Entity);

			if (bodyRaycastResult.HasValue && bodyRaycastResult.Value.ComponentBody != null)
			{
				ComponentBody hitBody = bodyRaycastResult.Value.ComponentBody;
				ComponentCreature creature = hitBody.Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature.ComponentHealth != null)
				{
					return TryHeal(
						creature.ComponentHealth,
						creature.Entity.Id,
						componentMiner,
						$"Curaste a {creature.DisplayName} parcialmente",
						hitBody.Position
					);
				}
			}

			if (componentMiner.ComponentCreature.ComponentHealth != null)
			{
				return TryHeal(
					componentMiner.ComponentCreature.ComponentHealth,
					componentMiner.Entity.Id,
					componentMiner,
					"Te has curado parcialmente",
					componentMiner.ComponentCreature.ComponentBody.Position
				);
			}

			return false;
		}
	}
}
