using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		public ComponentCreature Target
		{
			get { return m_target; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return m_importanceLevel; }
		}

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (componentCreature != null && IsPlayerHerd() && componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
				return;

			if (componentCreature != null && m_componentNewHerdBehavior != null)
			{
				ComponentNewHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.HerdName == m_componentNewHerdBehavior.HerdName)
					return;
			}

			if (Suppressed)
				return;

			m_target = componentCreature;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = (isPersistent ? ImportanceLevelPersistent : ImportanceLevelNonPersistent);
			m_autoChaseSuppressionTime = 0f;
			m_hasDestroyedBlocksWhileStuck = false;
			m_targetInRangeTime = TargetInRangeTimeToChase + 1f;
			m_targetUnsuitableTime = 0f;
			IsActive = true;

			if (m_componentNewHerdBehavior != null)
			{
				m_componentNewHerdBehavior.m_importanceLevel = 0f;
			}

			if (m_stateMachine.CurrentState != "Chasing")
			{
				m_stateMachine.TransitionTo("Chasing");
			}
		}

		private bool IsPlayerHerd()
		{
			return m_componentNewHerdBehavior != null && m_componentNewHerdBehavior.HerdName == "player";
		}

		public virtual void StopAttack()
		{
			m_stateMachine.TransitionTo("LookingForTarget");
			IsActive = false;
			m_target = null;
			m_nextUpdateTime = 0.0;
			m_range = 0f;
			m_chaseTime = 0f;
			m_isPersistent = false;
			m_importanceLevel = 0f;
			m_hasDestroyedBlocksWhileStuck = false;
			m_targetInRangeTime = 0f;
			m_targetUnsuitableTime = 0f;
		}

		/// <summary>
		/// Llamado externamente (ej. por el ModLoader) para forzar a la criatura aliada a proteger al jugador.
		/// Ignora supresiones y otorga prioridad máxima para no ser sobrescrito por la Protección Extrema.
		/// </summary>
		public void CallRangeHelp(ComponentCreature attacker)
		{
			if (attacker == null || attacker.ComponentHealth == null || attacker.ComponentHealth.Health <= 0f)
				return;

			Suppressed = false;

			m_target = attacker;
			m_nextUpdateTime = 0.0;
			m_range = 999999f;
			m_chaseTime = 60f;
			m_isPersistent = true;
			m_importanceLevel = 600f; // Prioridad máxima
			m_autoChaseSuppressionTime = 0f;
			m_hasDestroyedBlocksWhileStuck = false;
			m_targetInRangeTime = 0f;
			m_targetUnsuitableTime = 0f;
			IsActive = true;

			if (m_componentNewHerdBehavior != null)
			{
				m_componentNewHerdBehavior.m_importanceLevel = 0f;
			}

			if (m_stateMachine.CurrentState != "Chasing")
			{
				m_stateMachine.TransitionTo("Chasing");
			}
		}

		private void UpdateExtremeProtection(float dt)
		{
			bool isGreenNightActive = m_subsystemGreenNight != null && m_subsystemGreenNight.IsGreenNightActive;

			if (m_isExtremeProtectionActive && !isGreenNightActive)
			{
				m_isExtremeProtectionActive = false;
				if (m_target != null && m_target.ComponentHealth != null && m_target.ComponentHealth.Health > 0f)
				{
					ComponentZombieHerdBehavior targetZombieHerd = m_target.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (targetZombieHerd != null && targetZombieHerd.HerdName == "Zombie" && !m_wasChasingBeforeProtection)
					{
						StopAttack();
					}
				}
				m_wasChasingBeforeProtection = false;
				return;
			}

			if (!isGreenNightActive)
				return;

			m_isExtremeProtectionActive = true;

			if (m_componentCreature == null || m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			if (m_target != null && m_target.ComponentHealth != null && m_target.ComponentHealth.Health > 0f)
			{
				ComponentZombieHerdBehavior targetZombieHerd = m_target.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (targetZombieHerd != null && targetZombieHerd.HerdName == "Zombie")
				{
					m_chaseTime = MathUtils.Max(m_chaseTime, 10f);
					if (m_importanceLevel < 600f)
						m_importanceLevel = 600f;
					m_autoChaseSuppressionTime = 0f;
					m_targetUnsuitableTime = 0f;

					if (m_componentPathfinding != null && m_target.ComponentBody != null)
					{
						Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
						Vector3 myPos = m_componentCreature.ComponentBody.BoundingBox.Center();
						float distance = Vector3.Distance(myPos, targetPos);
						float predict = (distance < 4f) ? 0.3f : 0f;
						int maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;
						m_componentPathfinding.SetDestination(
							targetPos + predict * distance * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPos, true, false, true, m_target.ComponentBody);
					}
					return;
				}
				// CORRECCIÓN: Si fue una orden forzada con CallRangeHelp (Importancia 600), NO lo sobrescribimos
				else if (m_importanceLevel >= 600f && m_isPersistent)
				{
					m_chaseTime = MathUtils.Max(m_chaseTime, 10f);
					m_autoChaseSuppressionTime = 0f;
					m_targetUnsuitableTime = 0f;

					if (m_componentPathfinding != null && m_target.ComponentBody != null)
					{
						Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
						Vector3 myPos = m_componentCreature.ComponentBody.BoundingBox.Center();
						float distance = Vector3.Distance(myPos, targetPos);
						float predict = (distance < 4f) ? 0.3f : 0f;
						int maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;
						m_componentPathfinding.SetDestination(
							targetPos + predict * distance * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPos, true, false, true, m_target.ComponentBody);
					}
					return;
				}
			}

			float protectionRange = 35f;
			Vector3 position = m_componentCreature.ComponentBody.Position;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), protectionRange, m_componentBodies);

			ComponentCreature closestZombie = null;
			float closestDistance = float.MaxValue;

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature == null) continue;
				if (creature == m_componentCreature) continue;
				if (!creature.Entity.IsAddedToProject) continue;
				if (creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f) continue;

				ComponentZombieHerdBehavior zombieHerd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (zombieHerd != null && zombieHerd.HerdName == "Zombie")
				{
					float distance = Vector3.Distance(position, creature.ComponentBody.Position);
					if (distance < closestDistance)
					{
						closestDistance = distance;
						closestZombie = creature;
					}
				}
			}

			if (closestZombie != null)
			{
				m_wasChasingBeforeProtection = (m_target != null && m_target != closestZombie);

				m_target = closestZombie;
				m_range = protectionRange;
				m_chaseTime = 999f;
				m_isPersistent = true;
				m_importanceLevel = 600f;
				m_autoChaseSuppressionTime = 0f;
				m_targetUnsuitableTime = 0f;
				m_targetInRangeTime = 0f;
				IsActive = true;
				m_nextUpdateTime = 0.0;

				if (m_componentNewHerdBehavior != null)
				{
					m_componentNewHerdBehavior.m_importanceLevel = 0f;
				}

				if (m_stateMachine.CurrentState != "Chasing")
				{
					m_stateMachine.TransitionTo("Chasing");
				}

				if (m_componentPathfinding != null && closestZombie.ComponentBody != null)
				{
					Vector3 targetPos = closestZombie.ComponentBody.BoundingBox.Center();
					int maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;
					m_componentPathfinding.SetDestination(
						targetPos, 1f, 1.5f, maxPos, true, false, true, closestZombie.ComponentBody);
				}
			}
		}

		private bool IsPrioritizedTargetDuringGreenNight()
		{
			if (!m_isExtremeProtectionActive) return false;
			if (m_target == null) return false;

			ComponentZombieHerdBehavior targetZombieHerd = m_target.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (targetZombieHerd != null && targetZombieHerd.HerdName == "Zombie") return true;

			// Reconoce la orden forzada por CallRangeHelp
			if (m_importanceLevel >= 600f && m_isPersistent) return true;

			return false;
		}

		public virtual void Update(float dt)
		{
			UpdateExtremeProtection(dt);

			if (Suppressed && !m_isExtremeProtectionActive)
				StopAttack();

			m_autoChaseSuppressionTime -= dt;

			if (m_target != null && m_chaseTime > 0f)
			{
				m_chaseTime -= dt;

				if (m_isExtremeProtectionActive && IsPrioritizedTargetDuringGreenNight() && m_chaseTime < 5f)
				{
					m_chaseTime = 10f;
				}

				m_componentCreature.ComponentCreatureModel.LookAtOrder = m_target.ComponentCreatureModel.EyePosition;

				bool targetVisible = IsTargetVisible(m_target);

				if (IsTargetInAttackRange(m_target.ComponentBody) && targetVisible)
					m_componentCreatureModel.AttackOrder = true;

				if (m_componentCreatureModel.IsAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
					if (hitBody != null)
					{
						float x = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						if (m_isExtremeProtectionActive && IsPrioritizedTargetDuringGreenNight())
						{
							x = MathUtils.Max(x, 10f);
						}
						m_chaseTime = MathUtils.Max(m_chaseTime, x);
						m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();

						if (m_pushVictimOnHit && m_random.Float(0f, 1f) < 0.1f)
						{
							Vector3 directionFromAttackerToVictim = m_target.ComponentBody.Position - m_componentCreature.ComponentBody.Position;
							float horizontalDistance = new Vector2(directionFromAttackerToVictim.X, directionFromAttackerToVictim.Z).Length();
							Vector3 pushDirection;
							if (horizontalDistance > 0.01f)
							{
								pushDirection = Vector3.Normalize(new Vector3(directionFromAttackerToVictim.X, 0f, directionFromAttackerToVictim.Z));
							}
							else
							{
								pushDirection = m_componentCreature.ComponentBody.Matrix.Forward;
								pushDirection.Y = 0f;
								if (pushDirection.LengthSquared() > 0.01f)
								{
									pushDirection = Vector3.Normalize(pushDirection);
								}
								else
								{
									pushDirection = Vector3.UnitZ;
								}
							}
							hitBody.ApplyImpulse(Vector3.Normalize(pushDirection + Vector3.UnitY * 0.5f) * 1e+9f);
						}

						if (m_invokeLightningOnHit && m_target != null && m_random.Float(0f, 1f) < 0.1f)
						{
							m_subsystemSky.MakeLightningStrike(m_target.ComponentBody.Position, false);
						}

						if (m_explodeOnHit && m_target != null && m_random.Float(0f, 1f) < 0.1f)
						{
							Vector3 pos = m_target.ComponentBody.Position;
							int cellX = Terrain.ToCell(pos.X);
							int cellY = Terrain.ToCell(pos.Y);
							int cellZ = Terrain.ToCell(pos.Z);
							m_subsystemExplosions.AddExplosion(
								x: cellX,
								y: cellY,
								z: cellZ,
								pressure: 555f,
								isIncendiary: false,
								noExplosionSound: false
							);
						}
					}
				}
			}

			if (m_destroyBlocksWhenStuck && m_componentPathfinding.IsStuck && !m_hasDestroyedBlocksWhileStuck)
			{
				DestroyBlocksInLookDirection();
				m_hasDestroyedBlocksWhileStuck = true;
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		private void DestroyBlocksInLookDirection()
		{
			if (!IsActive || m_stateMachine.CurrentState != "Chasing" || m_target == null)
				return;

			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			Vector3 targetPos = m_target.ComponentBody.Position;

			Vector3 dirToTarget = targetPos - myPos;
			float distToTarget = dirToTarget.Length();
			if (distToTarget < 0.01f)
				return;

			float verticalRatio = MathF.Abs(dirToTarget.Y) / distToTarget;
			float horizontalLength = new Vector2(dirToTarget.X, dirToTarget.Z).Length();

			Point3 cell1, cell2;
			bool destroyBlocks = true;

			if (verticalRatio > 0.6f)
			{
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
			}
			else
			{
				Vector2 horizontalDir;
				if (horizontalLength > 0.01f)
				{
					horizontalDir = new Vector2(dirToTarget.X, dirToTarget.Z) / horizontalLength;
				}
				else
				{
					Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
					horizontalDir = new Vector2(forward.X, forward.Z);
					float len = horizontalDir.Length();
					if (len > 0.01f)
						horizontalDir /= len;
					else
						horizontalDir = new Vector2(0f, 1f);
				}

				Vector3 targetBlockPos = myPos + new Vector3(horizontalDir.X, 0f, horizontalDir.Y) * 1.5f;
				int baseY = Terrain.ToCell(myPos.Y + m_componentCreature.ComponentBody.BoxSize.Y * 0.5f);

				if (dirToTarget.Y > 0.3f)
				{
					baseY += 1;
				}
				else if (dirToTarget.Y < -0.3f)
				{
					baseY -= 1;
				}

				cell1 = new Point3(Terrain.ToCell(targetBlockPos.X), baseY, Terrain.ToCell(targetBlockPos.Z));
				cell2 = new Point3(cell1.X, baseY + 1, cell1.Z);
			}

			if (destroyBlocks)
			{
				int value1 = m_subsystemTerrain.Terrain.GetCellValue(cell1.X, cell1.Y, cell1.Z);
				if (Terrain.ExtractContents(value1) != BedrockBlock.Index)
				{
					m_subsystemSoundMaterials.PlayImpactSound(value1, new Vector3(cell1.X + 0.5f, cell1.Y + 0.5f, cell1.Z + 0.5f), 1f);
					m_subsystemTerrain.DestroyCell(4, cell1.X, cell1.Y, cell1.Z, 0, false, false, null);
				}

				int value2 = m_subsystemTerrain.Terrain.GetCellValue(cell2.X, cell2.Y, cell2.Z);
				if (Terrain.ExtractContents(value2) != BedrockBlock.Index)
				{
					m_subsystemSoundMaterials.PlayImpactSound(value2, new Vector3(cell2.X + 0.5f, cell2.Y + 0.5f, cell2.Z + 0.5f), 1f);
					m_subsystemTerrain.DestroyCell(4, cell2.X, cell2.Y, cell2.Z, 0, false, false, null);
				}
			}
		}

		private bool IsTargetVisible(ComponentCreature target)
		{
			if (target == null) return false;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEyePos = target.ComponentCreatureModel.EyePosition;
			Vector3 dirToTarget = targetEyePos - eyePos;
			float distSq = dirToTarget.LengthSquared();
			if (distSq < 0.01f) return true;
			float dist = MathF.Sqrt(distSq);

			Vector3 forward = m_componentCreature.ComponentBody.Matrix.Forward;
			float flatForwardLen = new Vector2(forward.X, forward.Z).Length();
			if (flatForwardLen < 0.001f) return false;

			Vector2 flatDir = new Vector2(dirToTarget.X, dirToTarget.Z);
			float flatDist = flatDir.Length();
			if (flatDist < 0.001f) return true;

			float dot = Vector2.Dot(
				new Vector2(forward.X, forward.Z) / flatForwardLen,
				flatDir / flatDist);
			if (dot < MathF.Cos(0.785398f))
				return false;

			TerrainRaycastResult? terrainResult = m_subsystemTerrain.Raycast(eyePos, targetEyePos, false, true, null);
			if (terrainResult != null && terrainResult.Value.Distance < dist - 0.01f)
			{
				return false;
			}

			Ray3 ray = new Ray3(eyePos, dirToTarget / dist);
			BodyRaycastResult? bodyResult = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyResult != null && bodyResult.Value.Distance < dist - 0.01f)
			{
				ComponentBody hitBody = bodyResult.Value.ComponentBody;
				if (hitBody != target.ComponentBody &&
					!hitBody.IsChildOfBody(target.ComponentBody) &&
					!target.ComponentBody.IsChildOfBody(hitBody))
				{
					return false;
				}
			}

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_subsystemGreenNight = Project.FindSubsystem<SubsystemGreenNightSky>(false);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);
			m_componentNewHerdBehavior = Entity.FindComponent<ComponentNewHerdBehavior>();

			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			m_invokeLightningOnHit = valuesDictionary.GetValue<bool>("InvokeLightningOnHit", false);
			m_explodeOnHit = valuesDictionary.GetValue<bool>("ExplodeOnHit", false);
			m_destroyBlocksWhenStuck = valuesDictionary.GetValue<bool>("DestroyBlocksWhenStuck", false);
			m_pushVictimOnHit = valuesDictionary.GetValue<bool>("PushVictimOnHit", false);

			ComponentBody componentBody = m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag2 = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;

						if ((AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
							(AttacksNonPlayerCreature && !isPlayer && flag2))
						{
							Attack(componentCreature, ChaseRangeOnTouch, ChaseTimeOnTouch, false);
						}
					}
				}

				if (m_target != null && JumpWhenTargetStanding && body == m_target.ComponentBody && body.StandingOnBody == m_componentCreature.ComponentBody)
				{
					m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (m_random.Float(0f, 1f) < m_chaseWhenAttackedProbability)
				{
					bool persistent = false;
					float range, time;
					if (m_chaseWhenAttackedProbability >= 1f)
					{
						range = 30f;
						time = 60f;
						persistent = true;
					}
					else
					{
						range = 7f;
						time = 7f;
					}
					range = ChaseRangeOnAttacked.GetValueOrDefault(range);
					time = ChaseTimeOnAttacked.GetValueOrDefault(time);
					persistent = ChasePersistentOnAttacked.GetValueOrDefault(persistent);
					Attack(attacker, range, time, persistent);
				}
			}));

			m_stateMachine.AddState("LookingForTarget", delegate
			{
				if (!IsActive && m_target == null)
				{
					m_importanceLevel = 0f;
				}
			}, delegate
			{
				if (m_isExtremeProtectionActive)
				{
					return;
				}

				if (m_target != null && m_chaseTime > 0f)
				{
					m_stateMachine.TransitionTo("Chasing");
					return;
				}

				if (IsActive && m_target != null)
				{
					m_stateMachine.TransitionTo("Chasing");
					return;
				}

				if (!Suppressed && m_autoChaseSuppressionTime <= 0f &&
					(m_target == null || ScoreTarget(m_target) <= 0f) &&
					m_componentCreature.ComponentHealth.Health > MinHealthToAttackActively)
				{
					m_range = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;
					m_range *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
					ComponentCreature creature = FindTarget();

					if (creature != null)
						m_targetInRangeTime += m_dt;
					else
						m_targetInRangeTime = 0f;

					if (m_targetInRangeTime > TargetInRangeTimeToChase)
					{
						bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
						float maxTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
						Attack(creature, maxRange, maxTime, !isDay);
					}
				}
			}, null);

			m_stateMachine.AddState("RandomMoving", delegate
			{
				m_componentPathfinding.SetDestination(
					m_componentCreature.ComponentBody.Position + new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f)),
					1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
					m_stateMachine.TransitionTo("Chasing");
				if (!IsActive && m_target == null)
					m_stateMachine.TransitionTo("LookingForTarget");
			}, delegate
			{
				m_componentPathfinding.Stop();
			});

			m_stateMachine.AddState("Chasing", delegate
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
				if (PlayIdleSoundWhenStartToChase)
					m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				m_nextUpdateTime = 0.0;
			}, delegate
			{
				if (m_isExtremeProtectionActive && IsPrioritizedTargetDuringGreenNight())
				{
					if (m_target == null || m_target.ComponentHealth == null || m_target.ComponentHealth.Health <= 0f)
					{
						m_importanceLevel = 0f;
						m_target = null;
						IsActive = false;
						m_stateMachine.TransitionTo("LookingForTarget");
						return;
					}

					if (m_componentPathfinding != null && m_target.ComponentBody != null)
					{
						BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox bb2 = m_target.ComponentBody.BoundingBox;
						Vector3 center1 = 0.5f * (bb1.Min + bb1.Max);
						Vector3 center2 = 0.5f * (bb2.Min + bb2.Max);
						float dist = Vector3.Distance(center1, center2);
						float predict = (dist < 4f) ? 0.3f : 0f;
						int maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;

						m_componentPathfinding.SetDestination(
							center2 + predict * dist * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPos, true, false, true, m_target.ComponentBody);
					}

					if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.5f * m_dt)
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();

					return;
				}

				if (!IsActive && m_target == null)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
					return;
				}

				if (m_target == null || m_chaseTime <= 0f)
				{
					if (m_chaseTime <= 0f && m_target != null)
					{
						m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					}
					m_importanceLevel = 0f;
					m_target = null;
					IsActive = false;
				}
				else if (m_target.ComponentHealth.Health <= 0f)
				{
					if (m_componentFeedBehavior != null)
					{
						ComponentCreature deadTarget = m_target;
						m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + (double)m_random.Float(1f, 3f), delegate
						{
							if (deadTarget != null && deadTarget.ComponentBody != null)
								m_componentFeedBehavior.Feed(deadTarget.ComponentBody.Position);
						});
					}
					m_importanceLevel = 0f;
					m_target = null;
					IsActive = false;
				}
				else if (!m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_importanceLevel = 0f;
					m_target = null;
					IsActive = false;
				}
				else if (m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_stateMachine.TransitionTo("RandomMoving");
				}
				else
				{
					if (ScoreTarget(m_target) <= 0f)
						m_targetUnsuitableTime += m_dt;
					else
						m_targetUnsuitableTime = 0f;

					if (m_targetUnsuitableTime > 3f)
					{
						m_importanceLevel = 0f;
						m_target = null;
						IsActive = false;
					}
					else
					{
						int maxPos = 0;
						if (m_isPersistent)
							maxPos = (m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;

						BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox bb2 = m_target.ComponentBody.BoundingBox;
						Vector3 center1 = 0.5f * (bb1.Min + bb1.Max);
						Vector3 center2 = 0.5f * (bb2.Min + bb2.Max);
						float dist = Vector3.Distance(center1, center2);
						float predict = (dist < 4f) ? 0.2f : 0f;

						m_componentPathfinding.SetDestination(
							center2 + predict * dist * m_target.ComponentBody.Velocity,
							1f, 1.5f, maxPos, true, false, true, m_target.ComponentBody);

						if (PlayAngrySoundWhenChasing && m_random.Float(0f, 1f) < 0.33f * m_dt)
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}, null);

			m_stateMachine.TransitionTo("LookingForTarget");
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						result = creature;
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			if (m_componentNewHerdBehavior != null)
			{
				ComponentNewHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (targetHerd != null && targetHerd.HerdName == m_componentNewHerdBehavior.HerdName)
					return 0f;
			}

			float score = 0f;
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool notWater = m_componentCreature.Category != CreatureCategory.WaterPredator && m_componentCreature.Category != CreatureCategory.WaterOther;
			bool isTargetOrGameMode = componentCreature == Target || m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool matchesMask = (componentCreature.Category & m_autoChaseMask) > (CreatureCategory)0;
			bool randomPass = componentCreature == Target || (matchesMask && MathUtils.Remainder(0.004999999888241291 * m_subsystemTime.GameTime + (double)((float)(GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)m_chaseNonPlayerProbability);

			if (m_componentNewHerdBehavior != null && m_componentNewHerdBehavior.HerdName == "player" && isPlayer)
			{
				score = 0f;
			}
			else if (componentCreature != m_componentCreature && ((!isPlayer && randomPass) || (isPlayer && isTargetOrGameMode)) &&
					 componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f &&
					 (notWater || IsTargetInWater(componentCreature.ComponentBody)))
			{
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (dist < m_range)
					score = m_range - dist;
			}
			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target))
				return true;
			BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bb2 = target.BoundingBox;
			Vector3 c1 = 0.5f * (bb1.Min + bb1.Max);
			Vector3 c2 = 0.5f * (bb2.Min + bb2.Max) - c1;
			float len = c2.Length();
			Vector3 dir = c2 / len;
			float width = 0.5f * (bb1.Max.X - bb1.Min.X + bb2.Max.X - bb2.Min.X);
			float height = 0.5f * (bb1.Max.Y - bb1.Min.Y + bb2.Max.Y - bb2.Min.Y);

			if (MathF.Abs(c2.Y) < height * 0.99f)
			{
				if (len < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (len < height + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bb1 = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox bb2 = target.BoundingBox;
			Vector3 c1 = 0.5f * (bb1.Min + bb1.Max);
			Vector3 c2 = 0.5f * (bb2.Min + bb2.Max) - c1;
			float len = c2.Length();
			Vector3 dir = c2 / len;
			float width = 0.5f * (bb1.Max.X - bb1.Min.X + bb2.Max.X - bb2.Min.X);
			float height = 0.5f * (bb1.Max.Y - bb1.Min.Y + bb2.Max.Y - bb2.Min.Y);

			if (MathF.Abs(c2.Y) < height * 0.99f)
			{
				if (len < width + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (len < height + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 eye = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Ray3 ray = new Ray3(eye, Vector3.Normalize(targetCenter - eye));
			BodyRaycastResult? raycast = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (raycast != null && raycast.Value.Distance < MaxAttackRange &&
				(raycast.Value.ComponentBody == target || raycast.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(raycast.Value.ComponentBody) ||
				 (target.StandingOnBody == raycast.Value.ComponentBody && AllowAttackingStandingOnBody)))
			{
				hitPoint = raycast.Value.HitPoint();
				return raycast.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemExplosions m_subsystemExplosions;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemGreenNightSky m_subsystemGreenNight;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public ComponentFactors m_componentFactors;
		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;
		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public float m_autoChaseSuppressionTime;
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public bool AttacksPlayer = true;
		public bool AttacksNonPlayerCreature = true;
		public float ChaseRangeOnTouch = 7f;
		public float ChaseTimeOnTouch = 7f;
		public float? ChaseRangeOnAttacked;
		public float? ChaseTimeOnAttacked;
		public bool? ChasePersistentOnAttacked;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;
		public ComponentNewHerdBehavior m_componentNewHerdBehavior;

		public bool m_invokeLightningOnHit = false;
		public bool m_explodeOnHit = false;
		public bool m_destroyBlocksWhenStuck = false;
		public bool m_pushVictimOnHit = false;

		private bool m_hasDestroyedBlocksWhileStuck;

		private bool m_isExtremeProtectionActive = false;
		private bool m_wasChasingBeforeProtection = false;
	}
}
