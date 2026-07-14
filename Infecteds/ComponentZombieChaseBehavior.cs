using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentBehavior, IUpdateable
	{
		public float ChaseRangeDay { get; set; }
		public float ChaseRangeNight { get; set; }
		public float ChaseTimeDay { get; set; }
		public float ChaseTimeNight { get; set; }
		public float ChaseNonPlayerProbability { get; set; }
		public float ChaseWhenAttackedProbability { get; set; }
		public float ChaseOnTouchProbability { get; set; }
		public bool AttacksPlayer { get; set; }
		public bool AttacksNonPlayerCreature { get; set; }
		public bool MoreAggressiveOnGreenNight { get; set; }
		public CreatureCategory AutoChaseMask { get; set; }
		public float TargetInRangeTimeToChase { get; set; } = 3f;

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemGreenNightSky m_subsystemGreenNight;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentFactors m_componentFactors;
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Random m_random = new Random();
		private StateMachine m_stateMachine = new StateMachine();
		private ComponentCreature m_target;
		private float m_importanceLevel;
		private float m_targetUnsuitableTime;
		private float m_targetInRangeTime;
		private double m_nextUpdateTime;
		private float m_dt;
		private float m_range;
		private float m_chaseTime;
		private bool m_isPersistent;
		private float m_autoChaseSuppressionTime;
		private string m_myHerdName;
		private const float MaxAttackRange = 1.75f;

		private bool m_wasGreenNightActive = false;
		private double m_lastGreenNightForcedSearch = 0.0;
		private bool m_isChasingGreenNightAttacker = false;
		private bool m_wasForcedByGreenNight = false;

		public ComponentCreature Target => m_target;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public override float ImportanceLevel => m_importanceLevel;

		private bool IsTargetFriendlyZombie(ComponentCreature target)
		{
			if (target == null) return false;
			ComponentZombieHerdBehavior targetHerd = target.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (targetHerd != null && targetHerd.HerdName == "Zombie") return true;
			if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && targetHerd.HerdName == m_myHerdName) return true;
			return false;
		}

		public void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null) return;
			if (IsTargetFriendlyZombie(target)) return;

			m_target = target;
			m_nextUpdateTime = 0.0;
			m_range = maxRange;
			m_chaseTime = maxChaseTime;
			m_isPersistent = isPersistent;
			m_importanceLevel = m_isPersistent ? 100f : 50f;
			m_targetUnsuitableTime = 0f;
			m_targetInRangeTime = 0f;
			m_wasForcedByGreenNight = false;
			IsActive = true;
		}

		public void StopAttack()
		{
			m_stateMachine.TransitionTo("LookingForTarget");
			IsActive = false;
			m_target = null;
			m_nextUpdateTime = 0.0;
			m_range = 0f;
			m_chaseTime = 0f;
			m_isPersistent = false;
			m_importanceLevel = 0f;
			m_targetUnsuitableTime = 0f;
			m_targetInRangeTime = 0f;
			m_wasForcedByGreenNight = false;
		}

		public void Update(float dt)
		{
			m_autoChaseSuppressionTime -= dt;

			bool isGreenNightActiveNow = MoreAggressiveOnGreenNight && m_subsystemGreenNight != null && m_subsystemGreenNight.IsGreenNightActive;

			if (!isGreenNightActiveNow && m_wasGreenNightActive)
			{
				m_isChasingGreenNightAttacker = false;
				if (m_wasForcedByGreenNight && m_target != null && m_target.ComponentHealth != null && m_target.ComponentHealth.Health > 0f)
				{
					bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
					m_chaseTime = isDay ? (ChaseTimeDay * m_random.Float(0.75f, 1f)) : (ChaseTimeNight * m_random.Float(0.75f, 1f));
					m_isPersistent = !isDay;
					m_importanceLevel = m_isPersistent ? 100f : 50f;
					m_range = isDay ? (ChaseRangeDay + 6f) : (ChaseRangeNight + 6f);
					m_wasForcedByGreenNight = false;
				}
			}

			if (isGreenNightActiveNow && !m_wasGreenNightActive)
			{
				m_lastGreenNightForcedSearch = 0.0;
			}
			m_wasGreenNightActive = isGreenNightActiveNow;

			if (isGreenNightActiveNow)
			{
				bool isAlreadyChasingPlayer = m_target != null && m_subsystemPlayers.IsPlayer(m_target.Entity) && m_target.ComponentHealth.Health > 0f;
				if (!isAlreadyChasingPlayer)
				{
					ForceChasePlayerOnGreenNight();
				}
			}

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;

				if (m_chaseTime <= 0f && isGreenNightActiveNow && m_wasForcedByGreenNight && m_subsystemPlayers.IsPlayer(m_target.Entity))
				{
					m_chaseTime = 1f;
				}

				m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(m_target.ComponentCreatureModel.EyePosition);

				if (IsTargetInAttackRange(m_target.ComponentBody))
				{
					m_componentCreatureModel.AttackOrder = true;
				}

				if (m_componentCreatureModel.IsAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
					if (hitBody != null)
					{
						float newChaseTime = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
						m_chaseTime = MathUtils.Max(m_chaseTime, newChaseTime);
						m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
						m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + (double)m_dt;
				m_stateMachine.Update();
			}
		}

		private void ForceChasePlayerOnGreenNight()
		{
			if (m_isChasingGreenNightAttacker && m_target != null && m_target.ComponentHealth.Health > 0f) return;
			if (m_subsystemTime.GameTime - m_lastGreenNightForcedSearch < 1.0) return;
			m_lastGreenNightForcedSearch = m_subsystemTime.GameTime;
			if (!AttacksPlayer) return;

			float range = ChaseRangeNight + 50f;
			Vector3 position = m_componentCreature.ComponentBody.Position;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, m_componentBodies);

			ComponentCreature bestPlayer = null;
			float bestDistance = float.MaxValue;

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature == null) continue;
				if (IsTargetFriendlyZombie(creature)) continue;

				if (m_subsystemPlayers.IsPlayer(creature.Entity) && creature.ComponentHealth.Health > 0f)
				{
					float distance = Vector3.Distance(position, creature.ComponentBody.Position);
					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestPlayer = creature;
					}
				}
			}

			if (bestPlayer != null)
			{
				m_target = bestPlayer;
				m_range = range;
				m_chaseTime = 100f;
				m_isPersistent = true;
				m_importanceLevel = 1000f;
				m_autoChaseSuppressionTime = 0f;
				m_targetUnsuitableTime = 0f;
				m_targetInRangeTime = 0f;
				m_nextUpdateTime = 0.0;
				m_isChasingGreenNightAttacker = false;
				m_wasForcedByGreenNight = true;
				IsActive = true;
				m_stateMachine.TransitionTo("Chasing");
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemGreenNight = Project.FindSubsystem<SubsystemGreenNightSky>(false);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);

			ChaseRangeDay = valuesDictionary.GetValue<float>("ChaseRangeDay");
			ChaseRangeNight = valuesDictionary.GetValue<float>("ChaseRangeNight");
			ChaseTimeDay = valuesDictionary.GetValue<float>("ChaseTimeDay");
			ChaseTimeNight = valuesDictionary.GetValue<float>("ChaseTimeNight");
			ChaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			ChaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			ChaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
			AttacksPlayer = valuesDictionary.GetValue<bool>("AttacksPlayer");
			AttacksNonPlayerCreature = valuesDictionary.GetValue<bool>("AttacksNonPlayerCreature");
			MoreAggressiveOnGreenNight = valuesDictionary.GetValue<bool>("MoreAggressiveOnGreenNight", false);
			AutoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask", (CreatureCategory)0);

			ComponentZombieHerdBehavior herd = Entity.FindComponent<ComponentZombieHerdBehavior>();
			m_myHerdName = (herd != null) ? herd.HerdName : null;

			ComponentBody body = m_componentCreature.ComponentBody;
			body.CollidedWithBody += delegate (ComponentBody otherBody)
			{
				if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < ChaseOnTouchProbability)
				{
					ComponentCreature creature = otherBody.Entity.FindComponent<ComponentCreature>();
					if (creature != null && !IsTargetFriendlyZombie(creature))
					{
						bool isPlayer = m_subsystemPlayers.IsPlayer(otherBody.Entity);
						bool isInMask = (creature.Category & AutoChaseMask) > (CreatureCategory)0;
						bool canChasePlayer = AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
						bool canChaseCreature = AttacksNonPlayerCreature && !isPlayer && isInMask;

						if (canChasePlayer || canChaseCreature)
						{
							Attack(creature, 7f, 7f, false);
						}
					}
				}
			};

			ComponentHealth health = m_componentCreature.ComponentHealth;
			health.Injured += delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (attacker == null || IsTargetFriendlyZombie(attacker)) return;

				if (m_random.Float(0f, 1f) < ChaseWhenAttackedProbability)
				{
					float range = (ChaseWhenAttackedProbability >= 1f) ? 30f : 7f;
					float time = (ChaseWhenAttackedProbability >= 1f) ? 60f : 7f;
					bool persistent = ChaseWhenAttackedProbability >= 1f;

					bool isGreenNightActive = MoreAggressiveOnGreenNight && m_subsystemGreenNight != null && m_subsystemGreenNight.IsGreenNightActive;
					m_isChasingGreenNightAttacker = isGreenNightActive && !m_subsystemPlayers.IsPlayer(attacker.Entity);

					Attack(attacker, range, time, persistent);
					m_importanceLevel = 1000f;
					m_autoChaseSuppressionTime = 0f;
				}
			};

			m_stateMachine.AddState("LookingForTarget", delegate
			{
				m_importanceLevel = 0f;
				m_target = null;
			}, delegate
			{
				if (IsActive)
				{
					m_stateMachine.TransitionTo("Chasing");
					return;
				}

				if (m_autoChaseSuppressionTime <= 0f && (m_target == null || ScoreTarget(m_target) <= 0f))
				{
					m_range = ((m_subsystemSky.SkyLightIntensity < 0.2f) ? ChaseRangeNight : ChaseRangeDay);
					m_range *= m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);

					ComponentCreature target = FindTarget();
					if (target != null)
					{
						m_targetInRangeTime += m_dt;
					}
					else
					{
						m_targetInRangeTime = 0f;
					}

					if (m_targetInRangeTime > TargetInRangeTimeToChase)
					{
						bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = isDay ? (ChaseRangeDay + 6f) : (ChaseRangeNight + 6f);
						float maxTime = isDay ? (ChaseTimeDay * m_random.Float(0.75f, 1f)) : (ChaseTimeNight * m_random.Float(0.75f, 1f));
						Attack(target, maxRange, maxTime, !isDay);
					}
				}
			}, null);

			m_stateMachine.AddState("Chasing", delegate
			{
				m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
				m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				m_nextUpdateTime = 0.0;
			}, delegate
			{
				bool isGreenNightActiveNow = MoreAggressiveOnGreenNight && m_subsystemGreenNight != null && m_subsystemGreenNight.IsGreenNightActive;
				bool isChasingPlayerOnGreenNight = isGreenNightActiveNow && m_wasForcedByGreenNight && m_target != null && m_subsystemPlayers.IsPlayer(m_target.Entity);
				bool isChasingGreenNightAttackerValid = isGreenNightActiveNow && m_isChasingGreenNightAttacker;

				if (!IsActive)
				{
					m_stateMachine.TransitionTo("LookingForTarget");
				}
				else if (isChasingPlayerOnGreenNight && m_chaseTime <= 0f)
				{
					m_chaseTime = 1f;
				}
				else if (isChasingGreenNightAttackerValid && m_chaseTime <= 0f)
				{
					m_isChasingGreenNightAttacker = false;
					m_importanceLevel = 0f;
				}
				// CORRECCIÓN PRINCIPAL: Comportamiento EXACTO del original.
				// Solo bajar importancia, NO limpiar m_target ni desactivar IsActive.
				else if (!isChasingPlayerOnGreenNight && !isChasingGreenNightAttackerValid && m_chaseTime <= 0f)
				{
					m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
					m_importanceLevel = 0f;
				}
				else if (!isChasingPlayerOnGreenNight && !isChasingGreenNightAttackerValid && !m_isPersistent && m_componentPathfinding.IsStuck)
				{
					m_importanceLevel = 0f;
				}
				else
				{
					if (ScoreTarget(m_target) <= 0f)
						m_targetUnsuitableTime += m_dt;
					else
						m_targetUnsuitableTime = 0f;

					if (m_targetUnsuitableTime > 3f)
					{
						if (isChasingGreenNightAttackerValid)
							m_isChasingGreenNightAttacker = false;

						m_importanceLevel = 0f;
					}
					else
					{
						int maxPathfinding = (m_isPersistent || isGreenNightActiveNow) ? ((m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;
						Vector3 targetPos = m_target.ComponentBody.BoundingBox.Center();
						float distance = Vector3.Distance(m_componentCreature.ComponentBody.BoundingBox.Center(), targetPos);
						float slowDown = (distance < 4f) ? 0.2f : 0f;
						m_componentPathfinding.SetDestination(new Vector3?(targetPos + slowDown * distance * m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfinding, true, false, true, m_target.ComponentBody);

						if (m_random.Float(0f, 1f) < 0.33f * m_dt)
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}, null);

			m_stateMachine.TransitionTo("LookingForTarget");
		}

		public ComponentCreature FindTarget()
		{
			Vector3 position = m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					float score = ScoreTarget(creature);
					if (score > bestScore)
					{
						bestScore = score;
						bestTarget = creature;
					}
				}
			}
			return bestTarget;
		}

		public float ScoreTarget(ComponentCreature target)
		{
			if (target == m_componentCreature) return 0f;
			if (IsTargetFriendlyZombie(target)) return 0f;

			bool isPlayer = target.Entity.FindComponent<ComponentPlayer>() != null;
			bool canAttackPlayer = AttacksPlayer && isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool isInMask = (target.Category & AutoChaseMask) > (CreatureCategory)0;
			bool canChaseByCategory = isInMask && MathUtils.Remainder(0.005 * m_subsystemTime.GameTime + (double)((float)(GetHashCode() % 1000) / 1000f) + (double)((float)(target.GetHashCode() % 1000) / 1000f), 1.0) < (double)ChaseNonPlayerProbability;
			bool canAttackCreature = AttacksNonPlayerCreature && !isPlayer && (target == m_target || canChaseByCategory);

			if ((canAttackPlayer || canAttackCreature) && target.Entity.IsAddedToProject && target.ComponentHealth.Health > 0f)
			{
				float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.ComponentBody.Position);
				if (distance < m_range)
				{
					return m_range - distance;
				}
			}
			return 0f;
		}

		public bool IsTargetInAttackRange(ComponentBody target) => IsBodyInAttackRange(target);

		public bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox myBox = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;
			Vector3 myCenter = 0.5f * (myBox.Min + myBox.Max);
			Vector3 offset = 0.5f * (targetBox.Min + targetBox.Max) - myCenter;
			float distance = offset.Length();
			if (distance == 0f) return false;

			Vector3 direction = offset / distance;
			float widthSum = 0.5f * (myBox.Max.X - myBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float heightSum = 0.5f * (myBox.Max.Y - myBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathF.Abs(offset.Y) < heightSum * 0.99f)
			{
				if (distance < widthSum + 0.99f && Vector3.Dot(direction, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f) return true;
			}
			else if (distance < heightSum + 0.3f && MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		public ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 start = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 end = target.BoundingBox.Center();
			Ray3 ray = new Ray3(start, Vector3.Normalize(end - start));
			BodyRaycastResult? result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (result != null && result.Value.Distance < MaxAttackRange)
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
		}
	}
}
