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

		// MÉTODO RESTAURADO para evitar el error de referencia
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
				// Ahora también muestra el nombre de la criatura específica aquí
				player.ComponentGui.DisplaySmallMessage(
					new RainbowMessage(string.Format("¡Has curado a {0} de la enfermedad!", creature.DisplayName)),
					true
				);
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
			{
				return false;
			}

			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			ComponentFlu componentFlu = componentPlayer.ComponentFlu;
			ComponentSickness componentSickness = componentPlayer.ComponentSickness;

			bool hadFlu = componentFlu.HasFlu;
			bool wasSick = componentSickness.IsSick;

			// Obtener la lista de nombres de criaturas curadas
			List<string> curedCreatureNames = CureNearbyCreatures(componentPlayer);

			if (!hadFlu && !wasSick && curedCreatureNames.Count == 0)
			{
				componentPlayer.ComponentGui.DisplaySmallMessage("No estás enfermo", Color.White, true, true);
				return false;
			}

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

			m_subsystemAudio.PlaySound("Audio/consumo antidoto", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, false);

			if (hadFlu || wasSick)
			{
				componentPlayer.ComponentGui.DisplaySmallMessage(
					new RainbowMessage("¡Antídoto consumido! Te has curado"),
					true
				);
			}

			// Mostrar mensaje con los nombres de todas las criaturas curadas
			if (curedCreatureNames.Count > 0)
			{
				string creatureNames = string.Join(", ", curedCreatureNames);
				componentPlayer.ComponentGui.DisplaySmallMessage(
					new RainbowMessage(string.Format("¡Has curado a {0} de la enfermedad!", creatureNames)),
					true
				);
			}

			componentMiner.RemoveActiveTool(1);

			return true;
		}

		/// <summary>
		/// Cura a las criaturas cercanas y devuelve una lista con los nombres de las criaturas curadas
		/// </summary>
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
					// Agregar el DisplayName de la criatura a la lista
					curedCreatureNames.Add(componentCreature.DisplayName);
				}
			}

			return curedCreatureNames;
		}
	}
}
