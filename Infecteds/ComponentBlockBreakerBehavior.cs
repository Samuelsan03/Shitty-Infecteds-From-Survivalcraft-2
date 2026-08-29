using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBlockBreakerBehavior : Component, IUpdateable
	{
		// Probabilidad de romper bloques (aplica igual a todos los bloques rompibles)
		public float BreakBlockProbability { get; set; }

		// Diccionario/Lista de bloques que puede romper (plantilla: nombre del bloque -> índice)
		public Dictionary<string, int> BreakableBlocks { get; set; }

		// Cooldown NO está en el XML, valor por defecto 0.5
		public float m_cooldown = 0.5f;
		public float m_cooldownTimer;

		// Subsistemas necesarios
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentHealth m_componentHealth;
		public Random m_random = new Random();

		// Índice de bedrock para exclusión
		public static int BedrockIndex = -1;

		// ============================================
		// COMPATIBILIDAD CON CHASES
		// ============================================
		public ComponentZombieChaseBehavior m_componentZombieChase;
		public ComponentNewChaseBehavior m_componentNewChase;

		// Para evitar demora al inicio
		private bool m_initialized;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);

			// ============================================
			// BUSCAR CHASES PARA COMPATIBILIDAD
			// ============================================
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentNewChase = Entity.FindComponent<ComponentNewChaseBehavior>(false);

			m_cooldownTimer = 0f;
			m_initialized = false;
			BedrockIndex = BlocksManager.GetBlockIndex("BedrockBlock");

			// ============================================
			// CARGAR FLOAT: BreakBlockProbability
			// ============================================
			BreakBlockProbability = valuesDictionary.GetValue<float>("BreakBlockProbability");

			// ============================================
			// CARGAR DICCIONARIO: BreakableBlocks (Plantilla)
			// ============================================
			BreakableBlocks = new Dictionary<string, int>();
			try
			{
				ValuesDictionary blocksDict = valuesDictionary.GetValue<ValuesDictionary>("BreakableBlocks");
				foreach (var key in blocksDict.Keys)
				{
					string blockName = key.Trim().Trim('"');
					if (!string.IsNullOrEmpty(blockName))
					{
						int blockIndex = BlocksManager.GetBlockIndex(blockName);
						// Excluir roca madre (bedrock) - es indestructible
						if (blockIndex >= 0 && blockIndex != BedrockIndex)
						{
							BreakableBlocks[blockName] = blockIndex;
						}
					}
				}
			}
			catch
			{
				// Si falla al cargar, dejar vacío (romperá todo menos bedrock)
			}
		}

		public void Update(float dt)
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= dt;
			}

			// ============================================
			// NO ROMPER SI LA CRIATURA ESTÁ MUERTA
			// ============================================
			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
			{
				m_initialized = false;
				return;
			}

			// ============================================
			// ROMPER BLOQUES MIENTRAS PERSIGUE (no solo atascado)
			// ============================================
			ComponentCreature target = GetCurrentChaseTarget();
			if (target != null && m_componentCreature != null && m_componentCreature.ComponentBody != null)
			{
				// Romper mientras persigue - sin esperar a estar atascado
				TryBreakBlocksWhileChasing(target);
			}
			else
			{
				m_initialized = false;
			}
		}

		/// <summary>
		/// Obtiene el target actual de cualquier chase activo (compatibilidad)
		/// </summary>
		public ComponentCreature GetCurrentChaseTarget()
		{
			// Priorizar ComponentNewChaseBehavior
			if (m_componentNewChase != null && m_componentNewChase.IsActive && m_componentNewChase.Target != null)
			{
				if (m_componentNewChase.Target.ComponentHealth != null && m_componentNewChase.Target.ComponentHealth.Health > 0f)
				{
					return m_componentNewChase.Target;
				}
			}

			// Luego ComponentZombieChaseBehavior
			if (m_componentZombieChase != null && m_componentZombieChase.IsActive && m_componentZombieChase.Target != null)
			{
				if (m_componentZombieChase.Target.ComponentHealth != null && m_componentZombieChase.Target.ComponentHealth.Health > 0f)
				{
					return m_componentZombieChase.Target;
				}
			}

			return null;
		}

		/// <summary>
		/// Verifica si hay algún chase activo
		/// </summary>
		public bool IsChasing()
		{
			if (m_componentNewChase != null && m_componentNewChase.IsActive) return true;
			if (m_componentZombieChase != null && m_componentZombieChase.IsActive) return true;
			return false;
		}

		/// <summary>
		/// Verifica si un bloque puede ser roto según la lista
		/// Si la lista está vacía, puede romper TODO menos bedrock
		/// </summary>
		public bool CanBreakBlock(int blockIndex)
		{
			// No romper aire
			if (blockIndex == 0) return false;

			// No romper bedrock (roca madre) - es indestructible
			if (blockIndex == BedrockIndex) return false;

			// Si el diccionario está vacío, puede romper TODO menos bedrock
			if (BreakableBlocks == null || BreakableBlocks.Count == 0)
			{
				return true;
			}

			// Verificar si el bloque está en la lista de rompibles
			foreach (var kvp in BreakableBlocks)
			{
				if (kvp.Value == blockIndex)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Intenta romper un bloque en la posición dada
		/// </summary>
		public bool TryBreakBlock(Point3 cellPosition)
		{
			if (m_cooldownTimer > 0f) return false;
			if (m_componentCreature == null || m_componentCreature.ComponentBody == null) return false;
			if (!m_subsystemTerrain.Terrain.IsCellValid(cellPosition)) return false;

			// NO ROMPER SI ESTÁ MUERTO
			if (m_componentHealth == null || m_componentHealth.Health <= 0f) return false;

			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(cellPosition.X, cellPosition.Y, cellPosition.Z);
			int blockIndex = Terrain.ExtractContents(cellValue);

			// No romper aire
			if (blockIndex == 0) return false;

			// No romper bedrock (roca madre) - es indestructible
			if (blockIndex == BedrockIndex) return false;

			// Verificar si el bloque está en la lista de rompibles
			if (!CanBreakBlock(blockIndex)) return false;

			// Aplicar probabilidad (misma para todos los bloques)
			if (m_random.Float(0f, 1f) > BreakBlockProbability) return false;

			// Romper el bloque
			Vector3 soundPos = new Vector3(cellPosition.X + 0.5f, cellPosition.Y + 0.5f, cellPosition.Z + 0.5f);
			m_subsystemSoundMaterials.PlayImpactSound(cellValue, soundPos, 1f);
			m_subsystemTerrain.DestroyCell(4, cellPosition.X, cellPosition.Y, cellPosition.Z, 0, false, false, null);

			m_cooldownTimer = m_cooldown;
			return true;
		}

		/// <summary>
		/// Intenta romper bloques en dirección dada
		/// </summary>
		public bool TryBreakBlockInDirection(Vector3 direction)
		{
			if (m_cooldownTimer > 0f) return false;
			if (m_componentCreature == null || m_componentCreature.ComponentBody == null) return false;

			// NO ROMPER SI ESTÁ MUERTO
			if (m_componentHealth == null || m_componentHealth.Health <= 0f) return false;

			Vector3 fromPos = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 toPos = fromPos + direction * 2f;

			TerrainRaycastResult? result = m_subsystemTerrain.Raycast(fromPos, toPos, false, true, null);
			if (result.HasValue)
			{
				return TryBreakBlock(result.Value.CellFace.Point);
			}
			return false;
		}

		/// <summary>
		/// Intenta romper bloques mientras persigue a la víctima
		/// AHORA ROMPE MIENTRAS PERSIGUE, no solo atascado
		/// </summary>
		public void TryBreakBlocksWhileChasing(ComponentCreature target)
		{
			if (target == null) return;
			if (m_cooldownTimer > 0f) return;
			if (m_componentCreature == null || m_componentCreature.ComponentBody == null) return;
			if (target.ComponentBody == null) return;

			// NO ROMPER SI ESTÁ MUERTO
			if (m_componentHealth == null || m_componentHealth.Health <= 0f) return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 dirToTarget = targetPos - myPos;
			float distToTarget = dirToTarget.Length();
			if (distToTarget < 0.01f) return;

			// Al inicio no verificar distancia para romper rápido
			bool shouldTryBreak = m_initialized || m_componentPathfinding == null || m_componentPathfinding.IsStuck;

			if (!shouldTryBreak)
			{
				// Si no está atascado y es el inicio, esperar un poco
				if (!m_initialized)
				{
					m_initialized = true;
					return;
				}
			}

			// Si la diferencia vertical es grande, intentar romper arriba/abajo
			if (MathF.Abs(dirToTarget.Y) / distToTarget > 0.6f)
			{
				Point3 cell1, cell2;
				if (dirToTarget.Y > 0)
				{
					int baseY = Terrain.ToCell(myPos.Y + m_componentCreature.ComponentBody.BoxSize.Y + 0.5f);
					cell1 = new Point3(Terrain.ToCell(myPos.X), baseY, Terrain.ToCell(myPos.Z));
					cell2 = new Point3(cell1.X, baseY + 1, cell1.Z);
				}
				else
				{
					int baseY = Terrain.ToCell(myPos.Y - 0.1f);
					cell1 = new Point3(Terrain.ToCell(myPos.X), baseY, Terrain.ToCell(myPos.Z));
					cell2 = new Point3(cell1.X, baseY - 1, cell1.Z);
				}
				for (int i = 0; i < 2; i++)
				{
					Point3 cell = (i == 0) ? cell1 : cell2;
					if (TryBreakBlock(cell)) return;
				}
			}
			else
			{
				// Raycast hacia el objetivo - SIEMPRE intentar mientras persigue
				Vector3 fromPos = m_componentCreature.ComponentBody.BoundingBox.Center();
				Vector3 toPos = target.ComponentBody.BoundingBox.Center();
				Vector3 dir = toPos - fromPos;
				float dist = dir.Length();
				if (dist < 0.01f) return;
				dir /= dist;
				Vector3 rayEnd = fromPos + dir * MathUtils.Min(dist, 3f);
				TerrainRaycastResult? terrainResult = m_subsystemTerrain.Raycast(fromPos, rayEnd, false, true, null);

				if (terrainResult.HasValue)
				{
					CellFace hitFace = terrainResult.Value.CellFace;
					for (int dy = 0; dy <= 1; dy++)
					{
						if (TryBreakBlock(new Point3(hitFace.X, hitFace.Y + dy, hitFace.Z))) return;
					}
				}
			}
		}

		/// <summary>
		/// Rompe bloques cuando está atascado mientras persigue (método legacy)
		/// </summary>
		public void TryBreakBlocksWhenStuck(ComponentCreature target)
		{
			if (target == null) return;
			if (m_componentPathfinding == null || !m_componentPathfinding.IsStuck) return;

			TryBreakBlocksWhileChasing(target);
		}

		/// <summary>
		/// Rompe bloques automáticamente usando el target del chase activo
		/// </summary>
		public void TryBreakBlocksAuto()
		{
			ComponentCreature target = GetCurrentChaseTarget();
			if (target != null)
			{
				TryBreakBlocksWhileChasing(target);
			}
		}
	}
}
