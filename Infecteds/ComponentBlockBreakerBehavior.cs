using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento que permite a la criatura romper bloques al atacar y, mientras persigue,
	/// rompe de forma proactiva el bloque adecuado según la posición del objetivo (arriba, abajo o al frente).
	/// </summary>
	public class ComponentBlockBreakerBehavior : ComponentBehavior, IUpdateable
	{
		private float m_breakProbability = 1f;
		private string m_breakableBlocksString = string.Empty;

		private HashSet<int> m_breakableBlockIndices = new HashSet<int>();
		private bool m_breakAllExceptBedrock = true;

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;

		private Random m_random = new Random();

		private bool m_lastAttackHitMoment;
		private double m_lastBreakTime; // control de frecuencia en persecución

		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			m_componentNewChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			m_componentZombieChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>();

			m_breakProbability = valuesDictionary.GetValue<float>("BreakBlockProbability", 1f);
			m_breakableBlocksString = valuesDictionary.GetValue<string>("BreakableBlocks", string.Empty);

			if (string.IsNullOrWhiteSpace(m_breakableBlocksString))
				m_breakAllExceptBedrock = true;
			else
			{
				m_breakAllExceptBedrock = false;
				string[] blockNames = m_breakableBlocksString.Split(',', StringSplitOptions.RemoveEmptyEntries);
				foreach (string blockName in blockNames)
				{
					string trimmed = blockName.Trim();
					int index = BlocksManager.GetBlockIndex(trimmed, false);
					if (index >= 0)
						m_breakableBlockIndices.Add(index);
					else
						Log.Warning($"ComponentBlockBreakerBehavior: bloque '{trimmed}' no encontrado.");
				}
			}
		}

		public void Update(float dt)
		{
			if (m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			bool isChasing = (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive && m_componentNewChaseBehavior.Target != null) ||
							 (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.IsActive && m_componentZombieChaseBehavior.Target != null);

			// 1. Romper bloque proactivo mientras persigue (cada 0.3s)
			if (isChasing)
			{
				if (m_subsystemTime.GameTime - m_lastBreakTime >= 0.3)
				{
					m_lastBreakTime = m_subsystemTime.GameTime;
					TryBreakBlockInPath();
				}
			}

			// 2. Romper bloque al atacar (solo en el momento del golpe)
			bool isAttacking = false;

			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive && m_componentNewChaseBehavior.Target != null)
			{
				var target = m_componentNewChaseBehavior.Target;
				if (target != null && target.ComponentHealth.Health > 0f &&
					m_componentNewChaseBehavior.IsTargetInAttackRange(target.ComponentBody))
					isAttacking = true;
			}

			if (!isAttacking && m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.IsActive && m_componentZombieChaseBehavior.Target != null)
			{
				var target = m_componentZombieChaseBehavior.Target;
				if (target != null && target.ComponentHealth.Health > 0f &&
					m_componentZombieChaseBehavior.IsTargetInAttackRange(target.ComponentBody))
					isAttacking = true;
			}

			if (isAttacking)
			{
				bool currentHit = m_componentCreatureModel.IsAttackHitMoment;
				if (currentHit && !m_lastAttackHitMoment)
					TryBreakBlockInFront();
				m_lastAttackHitMoment = currentHit;
			}
			else
			{
				m_lastAttackHitMoment = false;
			}
		}

		/// <summary>
		/// Rompe el bloque que está en la dirección adecuada según la posición del objetivo:
		/// - Si el objetivo está arriba → rompe el bloque encima (dirección Up)
		/// - Si está abajo → rompe el bloque debajo (dirección Down)
		/// - Si está a la misma altura → rompe el bloque al frente (dirección Forward)
		/// </summary>
		private void TryBreakBlockInPath()
		{
			ComponentCreature target = GetCurrentTarget();
			if (target == null) return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.Position;
			float heightDiff = targetPos.Y - myPos.Y;

			Vector3 direction;
			Vector3 start;
			float maxDist;

			if (heightDiff > 0.5f)
			{
				// Objetivo arriba: romper bloque encima
				direction = Vector3.UnitY;
				start = m_componentCreature.ComponentCreatureModel.EyePosition + Vector3.UnitY * 0.5f;
				maxDist = 1.8f;
			}
			else if (heightDiff < -0.5f)
			{
				// Objetivo abajo: romper bloque debajo
				direction = -Vector3.UnitY;
				start = m_componentCreature.ComponentCreatureModel.EyePosition - Vector3.UnitY * 0.9f;
				maxDist = 1.8f;
			}
			else
			{
				// Misma altura: romper bloque al frente
				direction = m_componentCreature.ComponentBody.Matrix.Forward;
				start = m_componentCreature.ComponentCreatureModel.EyePosition;
				maxDist = 2.0f;
			}

			var result = m_subsystemTerrain.Raycast(start, start + direction * maxDist, false, true, null);
			if (result == null) return;

			float dist = result.Value.Distance;
			if (dist > maxDist) return;

			int value = result.Value.Value;
			int blockIdx = Terrain.ExtractContents(value);

			if (!IsBlockBreakable(blockIdx)) return;
			if (m_random.Float(0f, 1f) >= m_breakProbability) return;

			CellFace cell = result.Value.CellFace;

			m_subsystemTerrain.DestroyCell(0, cell.X, cell.Y, cell.Z, 0, false, false, null);
			Vector3 pos = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);
			m_subsystemSoundMaterials.PlayImpactSound(value, pos, 1f);
		}

		private ComponentCreature GetCurrentTarget()
		{
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.IsActive)
				return m_componentNewChaseBehavior.Target;
			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.IsActive)
				return m_componentZombieChaseBehavior.Target;
			return null;
		}

		/// <summary> Rompe el bloque justo delante de la criatura (usado en el momento del ataque) </summary>
		private void TryBreakBlockInFront()
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			float maxDist = 2.5f;

			var result = m_subsystemTerrain.Raycast(eyePos, eyePos + forward * maxDist, false, true, null);
			if (result == null) return;

			float dist = result.Value.Distance;
			if (dist > maxDist) return;

			int value = result.Value.Value;
			int blockIdx = Terrain.ExtractContents(value);

			if (!IsBlockBreakable(blockIdx)) return;
			if (m_random.Float(0f, 1f) >= m_breakProbability) return;

			CellFace cell = result.Value.CellFace;

			m_subsystemTerrain.DestroyCell(0, cell.X, cell.Y, cell.Z, 0, false, false, null);
			Vector3 pos = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);
			m_subsystemSoundMaterials.PlayImpactSound(value, pos, 1f);
		}

		private bool IsBlockBreakable(int blockIndex)
		{
			if (blockIndex == BedrockBlock.Index) return false;
			if (m_breakAllExceptBedrock) return true;
			return m_breakableBlockIndices.Contains(blockIndex);
		}
	}
}
