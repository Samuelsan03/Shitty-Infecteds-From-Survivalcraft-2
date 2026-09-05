using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBandageBlockBehavior : SubsystemBlockBehavior
	{
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private HashSet<int> m_partialHealEntities = new HashSet<int>();

		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BlocksManager.GetBlock<BandageBlock>().BlockIndex };
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			if (componentMiner?.ComponentCreature == null)
				return false;

			// CORRECCIÓN: Obtenemos el índice del bloque registrado
			int bandageBlockIndex = BlocksManager.GetBlock<BandageBlock>().BlockIndex;

			// Verificamos si el item en mano es efectivamente un BandageBlock
			int value = componentMiner.Inventory?.GetSlotValue(componentMiner.Inventory.ActiveSlotIndex) ?? 0;

			// Usamos la variable bandageBlockIndex en lugar de BlockIndex
			if (Terrain.ExtractContents(value) != bandageBlockIndex)
				return false;

			BandageBlock.BandageType bandageType = BandageBlock.GetBandageType(Terrain.ExtractData(value));

			float reach = 5f;
			Vector3 end = ray.Position + ray.Direction * reach;

			BodyRaycastResult? bodyRaycastResult = m_subsystemBodies.Raycast(ray.Position, end, 0.35f, (ComponentBody body, float dist) => body.Entity != componentMiner.Entity);

			bool success = false;

			// Intento de curar a otros
			if (bodyRaycastResult.HasValue && bodyRaycastResult.Value.ComponentBody != null)
			{
				ComponentBody hitBody = bodyRaycastResult.Value.ComponentBody;
				ComponentCreature creature = hitBody.Entity.FindComponent<ComponentCreature>();

				if (creature != null && creature.ComponentHealth != null)
				{
					// Claves de idioma originales: Large usaba 3, Small usaba 1
					int messageKeyId = (bandageType == BandageBlock.BandageType.Large) ? 3 : 1;
					string message = string.Format(LanguageControl.Get("SubsystemBandageBehavior", messageKeyId), creature.DisplayName);

					success = TryHeal(
						creature.ComponentHealth,
						creature.Entity.Id,
						componentMiner,
						message,
						hitBody.Position,
						bandageType
					);
				}
			}

			// Intento de curarse a sí mismo si no golpeó a nadie
			if (!success && componentMiner.ComponentCreature.ComponentHealth != null)
			{
				// Claves de idioma originales: Large usaba 4, Small usaba 2
				int messageKeyId = (bandageType == BandageBlock.BandageType.Large) ? 4 : 2;

				success = TryHeal(
					componentMiner.ComponentCreature.ComponentHealth,
					componentMiner.Entity.Id,
					componentMiner,
					LanguageControl.Get("SubsystemBandageBehavior", messageKeyId),
					componentMiner.ComponentCreature.ComponentBody.Position,
					bandageType
				);
			}

			return success;
		}

		private bool TryHeal(ComponentHealth health, int entityId, ComponentMiner componentMiner, string message, Vector3 soundPosition, BandageBlock.BandageType type)
		{
			if (health.Health <= 0f || health.Health >= 1f)
				return false;

			float healAmount = 0f;

			if (type == BandageBlock.BandageType.Large)
			{
				// Lógica Vendaje Grande: Curación completa inmediata
				healAmount = 1f - health.Health;
			}
			else
			{
				// Lógica Vendaje Pequeño: Curación por mitades (tracking parcial)
				if (m_partialHealEntities.Contains(entityId) && MathF.Abs(health.Health - 0.5f) < 0.01f)
				{
					// Segunda aplicación: curar el 50% restante
					healAmount = 0.5f;
					m_partialHealEntities.Remove(entityId);
				}
				else if (health.Health < 0.5f)
				{
					// Primera aplicación: llevar al 50%
					healAmount = 0.5f - health.Health;
					m_partialHealEntities.Add(entityId);
				}
				else
				{
					// Caso borde o ya encima del 50% sin marca (completar)
					healAmount = 1f - health.Health;
				}
			}

			if (healAmount <= 0f)
				return false;

			health.Heal(healAmount);
			componentMiner.Inventory?.RemoveSlotItems(componentMiner.Inventory.ActiveSlotIndex, 1);

			componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
				message,
				Color.Yellow,
				true,
				false
			);

			m_subsystemAudio?.PlaySound("Audio/cured", 1f, 0f, soundPosition, 2f, false);

			return true;
		}
	}
}
