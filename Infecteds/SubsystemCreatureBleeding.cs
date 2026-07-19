using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCreatureBleeding : Subsystem, IUpdateable
	{
		public class BleedingData
		{
			public ComponentCreature Creature;
			public CreatureBleedingState State;
			public BloodParticleSystem Particles;
			public Vector3 BleedPosition;
			public Vector3 BleedDirection;
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public float m_criticalHealthThreshold = 0.2f;
		public Dictionary<ComponentCreature, SubsystemCreatureBleeding.BleedingData> m_bleedingData = new Dictionary<ComponentCreature, SubsystemCreatureBleeding.BleedingData>();
		public List<ComponentCreature> m_toRemove = new List<ComponentCreature>();
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemTime m_subsystemTime;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemGameInfo m_subsystemGameInfo;

		// Lista de criaturas que no sangran
		public static readonly HashSet<string> m_nonBleedingCreatures = new HashSet<string>
		{
			"GhostNormal"
            // Agregar más criaturas aquí
        };

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
		}

		public virtual void Update(float dt)
		{
			if (!ShittyInfectedsSettings.EnableCreatureBleeding)
			{
				if (m_bleedingData.Count > 0)
				{
					m_toRemove.Clear();
					foreach (ComponentCreature creature in m_bleedingData.Keys.ToList())
					{
						m_toRemove.Add(creature);
					}
					foreach (ComponentCreature key in m_toRemove)
					{
						CleanupBleeding(key);
					}
				}
				return;
			}

			this.m_toRemove.Clear();

			foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.m_creatures.Keys)
			{
				if (creature != null && creature.ComponentHealth != null && !creature.ComponentHealth.IsInvulnerable)
				{
					this.ProcessCreature(creature);
				}
			}

			if (this.m_subsystemPlayers != null)
			{
				foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
				{
					if (player != null && player.ComponentHealth != null)
					{
						this.ProcessCreature(player);
					}
				}
			}

			foreach (ComponentCreature key in this.m_toRemove)
			{
				this.CleanupBleeding(key);
			}
		}

		public void ProcessCreature(ComponentCreature creature)
		{
			ComponentHealth componentHealth = creature.ComponentHealth;
			ComponentBody componentBody = creature.ComponentBody;
			if (componentHealth == null || componentBody == null)
			{
				if (this.m_bleedingData.ContainsKey(creature))
				{
					this.m_toRemove.Add(creature);
				}
				return;
			}
			SubsystemCreatureBleeding.BleedingData bleedingData;
			if (this.m_bleedingData.TryGetValue(creature, out bleedingData))
			{
				// CORRECCIÓN: Posicionado en las piernas (25% de la altura total desde los pies)
				if (bleedingData.Particles != null && bleedingData.Particles.SubsystemParticles != null)
				{
					Vector3 position = componentBody.Position + Vector3.UnitY * componentBody.StanceBoxSize.Y * 0.25f + componentBody.Matrix.Forward * 0.15f;
					bleedingData.Particles.Position = position;
					bleedingData.BleedPosition = position;
				}
				switch (bleedingData.State)
				{
					case CreatureBleedingState.Dying:
						if (componentHealth.Health <= 0f)
						{
							bleedingData.State = CreatureBleedingState.Dead;
							return;
						}
						if (componentHealth.Health > this.m_criticalHealthThreshold * 2f)
						{
							bleedingData.State = CreatureBleedingState.Fading;
							if (bleedingData.Particles != null)
							{
								bleedingData.Particles.IsStopped = true;
							}
							return;
						}
						break;
					case CreatureBleedingState.Dead:
						if (componentHealth.DeathTime == null || componentHealth.CorpseDuration <= 0f)
						{
							bleedingData.State = CreatureBleedingState.Fading;
							if (bleedingData.Particles != null)
							{
								bleedingData.Particles.IsStopped = true;
							}
							return;
						}
						double num = this.m_subsystemGameInfo.TotalElapsedGameTime - componentHealth.DeathTime.Value;

						// Verificamos si el tiempo de muerte superó el CorpseDuration (el momento exacto en que el cuerpo desaparece)
						if (num >= (double)componentHealth.CorpseDuration)
						{
							bleedingData.State = CreatureBleedingState.Fading;
							if (bleedingData.Particles != null)
							{
								bleedingData.Particles.IsStopped = true;
							}
							return;
						}
						break;
					case CreatureBleedingState.Fading:
						if (bleedingData.Particles == null || bleedingData.Particles.SubsystemParticles == null || bleedingData.Particles.IsStopped)
						{
							bool flag = false;
							if (bleedingData.Particles != null)
							{
								for (int i = 0; i < bleedingData.Particles.Particles.Length; i++)
								{
									if (bleedingData.Particles.Particles[i].IsActive)
									{
										flag = true;
										break;
									}
								}
							}
							if (!flag)
							{
								bleedingData.State = CreatureBleedingState.Finished;
								this.m_toRemove.Add(creature);
							}
						}
						break;
					case CreatureBleedingState.Finished:
						this.m_toRemove.Add(creature);
						break;
				}
			}
			else if (this.ShouldStartBleeding(creature))
			{
				this.StartBleeding(creature, componentBody);
			}
		}

		public bool ShouldStartBleeding(ComponentCreature creature)
		{
			ComponentHealth health = creature.ComponentHealth;
			if (health == null)
			{
				return false;
			}

			// Verificar si la criatura está en la lista de exclusiones
			if (creature.Entity != null && creature.Entity.ValuesDictionary != null)
			{
				string creatureName = creature.Entity.ValuesDictionary.DatabaseObject?.Name;
				if (!string.IsNullOrEmpty(creatureName) && m_nonBleedingCreatures.Contains(creatureName))
				{
					return false;
				}
			}

			bool isPlayer = creature is ComponentPlayer;
			if (!isPlayer && health.IsInvulnerable)
			{
				return false;
			}

			if (health.Health > 0f && health.Health <= this.m_criticalHealthThreshold)
			{
				return true;
			}
			if (health.Health <= 0f && health.DeathTime != null && this.m_subsystemGameInfo.TotalElapsedGameTime - health.DeathTime.Value < 0.5)
			{
				return true;
			}
			return false;
		}

		public void StartBleeding(ComponentCreature creature, ComponentBody body)
		{
			// CORRECCIÓN: Posicionado en las piernas (25% de la altura total desde los pies)
			Vector3 position = body.Position + Vector3.UnitY * body.StanceBoxSize.Y * 0.25f + body.Matrix.Forward * 0.15f;
			Vector3 vector = -body.Matrix.Forward * 0.4f + -Vector3.UnitY * 0.3f;
			BloodParticleSystem bloodParticleSystem = new BloodParticleSystem(this.m_subsystemTerrain);
			bloodParticleSystem.Position = position;
			bloodParticleSystem.Direction = vector;
			bloodParticleSystem.IsStopped = false;
			SubsystemCreatureBleeding.BleedingData bleedingData = new SubsystemCreatureBleeding.BleedingData();
			bleedingData.Creature = creature;
			bleedingData.State = (creature.ComponentHealth.Health <= 0f) ? CreatureBleedingState.Dead : CreatureBleedingState.Dying;
			bleedingData.Particles = bloodParticleSystem;
			bleedingData.BleedPosition = position;
			bleedingData.BleedDirection = vector;
			this.m_subsystemParticles.AddParticleSystem(bloodParticleSystem, false);
			this.m_bleedingData.Add(creature, bleedingData);
		}

		public void CleanupBleeding(ComponentCreature creature)
		{
			SubsystemCreatureBleeding.BleedingData bleedingData;
			if (this.m_bleedingData.TryGetValue(creature, out bleedingData))
			{
				if (bleedingData.Particles != null && bleedingData.Particles.SubsystemParticles != null)
				{
					this.m_subsystemParticles.RemoveParticleSystem(bleedingData.Particles, false);
				}
				this.m_bleedingData.Remove(creature);
			}
		}

		public enum CreatureBleedingState
		{
			None,
			Dying,
			Dead,
			Fading,
			Finished
		}
	}
}
