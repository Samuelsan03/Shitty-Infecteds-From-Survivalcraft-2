using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntidotePillBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { BlocksManager.GetBlockIndex<AntidotePillBlock>() };

		private SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;

		private const float CureRadius = 5f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
		}

		public void CureCreatureWithMessage(ComponentPlayer player, ComponentCreature creature)
		{
			if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f)
				return;

			bool creatureCured = false;

			ComponentCreatureFlu creatureFlu = creature.Entity.FindComponent<ComponentCreatureFlu>();
			if (creatureFlu != null && creatureFlu.HasFlu)
			{
				creatureFlu.Cure();
				creatureCured = true;
			}

			ComponentInfectedWithPoison creaturePoison = creature.Entity.FindComponent<ComponentInfectedWithPoison>();
			if (creaturePoison != null && creaturePoison.IsInfected)
			{
				creaturePoison.Cure();
				creatureCured = true;
			}

			if (creatureCured)
			{
				string message = string.Format(LanguageControl.Get("SubsystemAntidotePillBehavior", "2"), creature.DisplayName);
				// false = NO reproducir sonido por defecto del mensaje
				player.ComponentGui.DisplaySmallMessage(new RainbowMessage(message), false);
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			bool isCreative = m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
			bool playerCured = false;

			// Solo intentar curar al jugador si NO está en modo creativo
			if (!isCreative)
			{
				ComponentFlu componentFlu = componentPlayer.ComponentFlu;
				ComponentSickness componentSickness = componentPlayer.ComponentSickness;

				bool hadFlu = componentFlu.HasFlu;
				bool wasSick = componentSickness.IsSick;

				if (hadFlu || wasSick)
				{
					if (hadFlu)
					{
						componentFlu.m_fluDuration = 0f;
						componentFlu.m_fluOnset = 0f;
						componentFlu.m_coughDuration = 0f;
						componentFlu.m_sneezeDuration = 0f;
						componentFlu.m_blackoutDuration = 0f;
						componentFlu.m_blackoutFactor = 0f;
					}

					if (wasSick)
					{
						componentSickness.m_sicknessDuration = 0f;
						componentSickness.m_greenoutDuration = 0f;
						componentSickness.m_greenoutFactor = 0f;
						componentSickness.m_pukeParticleSystem = null;
					}

					// Tu sonido personalizado
					m_subsystemAudio.PlaySound("Audio/consumo antidoto", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, false);
					// false = NO reproducir sonido por defecto "Audio/UI/Message"
					componentPlayer.ComponentGui.DisplaySmallMessage(
						new RainbowMessage(LanguageControl.Get("SubsystemAntidotePillBehavior", "1")),
						false
					);
					playerCured = true;
				}
			}

			// Siempre intentar curar criaturas (funciona en creativo y no creativo)
			List<string> curedCreatureNames = CureNearbyCreatures(componentPlayer);

			// Si no se curó nada (ni jugador en supervivencia, ni criaturas), bloquear sin mensaje
			if (!playerCured && curedCreatureNames.Count == 0)
			{
				return false;
			}

			// Mostrar mensaje de criaturas curadas si hay alguna
			if (curedCreatureNames.Count > 0)
			{
				string creatureNames = string.Join(", ", curedCreatureNames);
				string message = string.Format(LanguageControl.Get("SubsystemAntidotePillBehavior", "2"), creatureNames);

				// Tu sonido personalizado (solo si no se reprodujo ya)
				if (!playerCured)
				{
					m_subsystemAudio.PlaySound("Audio/consumo antidoto", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, false);
				}

				// false = NO reproducir sonido por defecto "Audio/UI/Message"
				componentPlayer.ComponentGui.DisplaySmallMessage(
					new RainbowMessage(message),
					false
				);
			}

			componentMiner.RemoveActiveTool(1);

			return true;
		}

		private List<string> CureNearbyCreatures(ComponentPlayer componentPlayer)
		{
			List<string> curedCreatureNames = new List<string>();
			Vector3 playerPosition = componentPlayer.ComponentBody.Position;
			float cureRadiusSquared = CureRadius * CureRadius;

			foreach (ComponentCreature componentCreature in m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.Entity == componentPlayer.Entity)
					continue;

				if (componentCreature.ComponentBody == null)
					continue;

				float distanceSquared = Vector3.DistanceSquared(playerPosition, componentCreature.ComponentBody.Position);
				if (distanceSquared > cureRadiusSquared)
					continue;

				if (componentCreature.ComponentHealth != null && componentCreature.ComponentHealth.Health <= 0f)
					continue;

				bool creatureCured = false;

				ComponentCreatureFlu creatureFlu = componentCreature.Entity.FindComponent<ComponentCreatureFlu>();
				if (creatureFlu != null && creatureFlu.HasFlu)
				{
					creatureFlu.Cure();
					creatureCured = true;
				}

				ComponentInfectedWithPoison creaturePoison = componentCreature.Entity.FindComponent<ComponentInfectedWithPoison>();
				if (creaturePoison != null && creaturePoison.IsInfected)
				{
					creaturePoison.Cure();
					creatureCured = true;
				}

				if (creatureCured)
				{
					curedCreatureNames.Add(componentCreature.DisplayName);
				}
			}

			return curedCreatureNames;
		}
	}
}
