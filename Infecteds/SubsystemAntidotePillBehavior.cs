using System;
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

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
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

			if (!hadFlu && !wasSick)
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

			// Mensaje arcoíris usando el sistema existente
			componentPlayer.ComponentGui.DisplaySmallMessage(
				new RainbowMessage("¡Antídoto consumido! Te has curado"),
				true
			);

			componentMiner.RemoveActiveTool(1);

			return true;
		}
	}
}
