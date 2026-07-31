using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBlockBreakerBehavior : Component, IUpdateable
	{
		/// <summary>
		/// Enum que representa los pasos del comportamiento de romper bloques mientras persigue a su víctima
		/// </summary>
		public enum BlockBreakingState
		{
			Idle,
			Identifying,
			Breaking
		}

		public float BreakBlockProbability { get; set; }

		public Dictionary<int, bool> BreakableBlocks { get; set; } = new Dictionary<int, bool>();

		/// <summary>
		/// El único campo que no va al load, usa el valor por defecto
		/// </summary>
		public float TimeToBreakBlocksAgain { get; set; } = 0.5f;

		public BlockBreakingState CurrentState { get; private set; } = BlockBreakingState.Idle;

		public Point3? TargetCell { get; private set; }

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTime m_subsystemTime;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;

		private ComponentCreature m_componentCreature;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentPathfinding m_componentPathfinding;

		private ComponentNewChaseBehavior m_componentNewChase;
		private ComponentZombieChaseBehavior m_componentZombieChase;

		private Random m_random = new Random();
		private float m_cooldownTimer;
		private bool m_hasBrokenThisCycle;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			m_componentNewChase = Entity.FindComponent<ComponentNewChaseBehavior>(false);
			m_componentZombieChase = Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			BreakBlockProbability = valuesDictionary.GetValue<float>("BreakBlockProbability");

			string breakableBlocksStr = valuesDictionary.GetValue<string>("BreakableBlocks", "");
			ParseBreakableBlocks(breakableBlocksStr);
		}

		private void ParseBreakableBlocks(string input)
		{
			BreakableBlocks.Clear();

			if (string.IsNullOrWhiteSpace(input))
				return;

			string[] parts = input.Split(',');
			foreach (string part in parts)
			{
				string blockName = part.Trim();

				if (blockName.StartsWith("x "))
					blockName = blockName.Substring(2).Trim();

				if (string.IsNullOrEmpty(blockName))
					continue;

				int blockIndex = BlocksManager.GetBlockIndex(blockName);
				if (blockIndex >= 0)
				{
					BreakableBlocks[blockIndex] = true;
				}
				else
				{
					Log.Warning($"[ComponentBlockBreakerBehavior] Block '{blockName}' not found for entity '{Entity.GetType().Name}'");
				}
			}
		}

		public void Update(float dt)
		{
			m_cooldownTimer -= dt;
			if (m_cooldownTimer > 0f)
			{
				return;
			}

			ComponentCreature target = GetActiveChaseTarget();

			if (target == null || !m_componentPathfinding.IsStuck)
			{
				ResetToIdle();
				return;
			}

			switch (CurrentState)
			{
				case BlockBreakingState.Idle:
					HandleIdleState();
					break;

				case BlockBreakingState.Identifying:
					HandleIdentifyingState(target);
					break;

				case BlockBreakingState.Breaking:
					HandleBreakingState();
					break;
			}
		}

		private void ResetToIdle()
		{
			CurrentState = BlockBreakingState.Idle;
			TargetCell = null;
			m_hasBrokenThisCycle = false;
		}

		private void HandleIdleState()
		{
			if (m_random.Float(0f, 1f) < BreakBlockProbability)
			{
				CurrentState = BlockBreakingState.Identifying;
			}
		}

		private void HandleIdentifyingState(ComponentCreature target)
		{
			Point3? cellToBreak = FindBlockToBreak(target);

			if (cellToBreak.HasValue)
			{
				TargetCell = cellToBreak.Value;
				CurrentState = BlockBreakingState.Breaking;
			}
			else
			{
				CurrentState = BlockBreakingState.Idle;
			}
		}

		private void HandleBreakingState()
		{
			if (TargetCell.HasValue)
			{
				if (TryBreakBlock(TargetCell.Value.X, TargetCell.Value.Y, TargetCell.Value.Z))
				{
					m_hasBrokenThisCycle = true;
					m_cooldownTimer = TimeToBreakBlocksAgain;
				}
			}

			CurrentState = BlockBreakingState.Idle;
			TargetCell = null;
		}

		private ComponentCreature GetActiveChaseTarget()
		{
			if (m_componentNewChase != null && m_componentNewChase.IsActive && m_componentNewChase.Target != null)
			{
				return m_componentNewChase.Target;
			}

			if (m_componentZombieChase != null && m_componentZombieChase.IsActive && m_componentZombieChase.Target != null)
			{
				return m_componentZombieChase.Target;
			}

			return null;
		}

		private Point3? FindBlockToBreak(ComponentCreature target)
		{
			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = target.ComponentBody.Position;
			Vector3 dirToTarget = targetPos - myPos;

			float dist = dirToTarget.Length();

			if (dist < 0.01f)
				return null;

			Vector3 dir = dirToTarget / dist;

			if (MathF.Abs(dir.Y) / dist > 0.6f)
			{
				return FindVerticalBlockToBreak(myPos, dir.Y > 0f);
			}

			return FindHorizontalBlockToBreak(myPos, dir, dist);
		}

		private Point3? FindVerticalBlockToBreak(Vector3 myPos, bool goingUp)
		{
			int x = Terrain.ToCell(myPos.X);
			int z = Terrain.ToCell(myPos.Z);
			int y;

			if (goingUp)
			{
				y = Terrain.ToCell(myPos.Y + m_componentCreature.ComponentBody.BoxSize.Y + 0.5f);
				if (CanBreakBlock(x, y, z)) return new Point3(x, y, z);
				if (CanBreakBlock(x, y + 1, z)) return new Point3(x, y + 1, z);
			}
			else
			{
				y = Terrain.ToCell(myPos.Y - 0.1f);
				if (CanBreakBlock(x, y, z)) return new Point3(x, y, z);
				if (CanBreakBlock(x, y - 1, z)) return new Point3(x, y - 1, z);
			}

			return null;
		}

		private Point3? FindHorizontalBlockToBreak(Vector3 myPos, Vector3 dir, float dist)
		{
			Vector3 fromPos = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 rayEnd = fromPos + dir * MathUtils.Min(dist, 3f);

			TerrainRaycastResult? result = m_subsystemTerrain.Raycast(fromPos, rayEnd, false, true, null);

			if (result.HasValue)
			{
				CellFace hit = result.Value.CellFace;
				if (CanBreakBlock(hit.X, hit.Y, hit.Z))
					return new Point3(hit.X, hit.Y, hit.Z);
				if (CanBreakBlock(hit.X, hit.Y + 1, hit.Z))
					return new Point3(hit.X, hit.Y + 1, hit.Z);
			}

			return null;
		}

		private bool CanBreakBlock(int x, int y, int z)
		{
			if (y < 0 || y >= 256)
				return false;

			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockIndex = Terrain.ExtractContents(cellValue);

			if (blockIndex == 0)
				return false;

			if (blockIndex == BedrockBlock.Index)
				return false;

			return IsBlockBreakable(blockIndex, cellValue);
		}

		private bool IsBlockBreakable(int blockIndex, int cellValue)
		{
			Block block = BlocksManager.Blocks[blockIndex];

			if (BreakableBlocks.Count > 0)
			{
				return BreakableBlocks.ContainsKey(blockIndex);
			}

			if (blockIndex == BedrockBlock.Index)
				return false;

			if (!block.IsCollidable_(cellValue))
				return false;

			if (block.IsTransparent_(cellValue))
				return false;

			return true;
		}

		private bool TryBreakBlock(int x, int y, int z)
		{
			if (!CanBreakBlock(x, y, z))
				return false;

			int cellValue = m_subsystemTerrain.Terrain.GetCellValue(x, y, z);
			int blockIndex = Terrain.ExtractContents(cellValue);
			Block block = BlocksManager.Blocks[blockIndex];

			Vector3 blockCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

			m_subsystemSoundMaterials.PlayImpactSound(cellValue, blockCenter, 1f);

			m_subsystemTerrain.DestroyCell(
				toolLevel: 4,
				x: x,
				y: y,
				z: z,
				newValue: 0,
				noDrop: false,
				noParticleSystem: false,
				movingBlock: null
			);

			return true;
		}
	}
}
