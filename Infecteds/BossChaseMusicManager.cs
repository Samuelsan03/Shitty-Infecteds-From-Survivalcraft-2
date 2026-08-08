using System;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class BossChaseMusicManager
	{
		private static StreamingSound m_sound;
		private static float m_playTime;
		private static float m_duration;

		public static void Update(bool isChasing, float dt)
		{
			// Si la criatura dejó de perseguir o murió, detenemos la música
			if (!isChasing)
			{
				Stop();
				return;
			}

			// Si no hay sonido, o el tiempo superó la duración (52s), o se detuvo solo
			if (m_sound == null || m_playTime >= m_duration || m_sound.State <= SoundState.Stopped)
			{
				Play();
			}
			else
			{
				// Sumamos tiempo solo si está sonando
				m_playTime += dt;
			}
		}

		public static void Play()
		{
			try
			{
				Stop();

				StreamingSource source = ContentManager.Get<StreamingSource>("Music/ChaseTheme/Tank Theme");
				source = source.Duplicate();

				// Calculamos la duración exacta del audio
				m_duration = (float)((double)source.BytesCount / (double)source.ChannelsCount / 2.0 / (double)source.SamplingFrequency);
				m_playTime = 0f;

				// Loop en false porque lo reiniciamos nosotros manualmente
				m_sound = new StreamingSound(source, MusicManager.Volume, 1f, 0f, false, false, 1f);
				m_sound.Play();
			}
			catch (Exception ex)
			{
				Log.Warning("Error playing boss chase music: " + ex.Message);
				m_sound = null;
			}
		}

		public static void Stop()
		{
			if (m_sound != null)
			{
				m_sound.Stop();
				m_sound.Dispose();
				m_sound = null;
				m_playTime = 0f;
			}
		}

		public static void Initialize()
		{
			// Por seguridad, si cierran el juego de golpe, limpiamos el audio
			Window.Closed += delegate
			{
				Stop();
			};
		}
	}
}
