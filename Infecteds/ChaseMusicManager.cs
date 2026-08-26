using System;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class ChaseMusicManager
	{
		private static StreamingSound m_sound;

		public static bool IsPlaying
		{
			get { return m_sound != null && m_sound.State == SoundState.Playing; }
		}

		public static void PlayChaseMusic()
		{
			if (IsPlaying) return;

			try
			{
				StopMusic();

				StreamingSource streamingSource = ContentManager.Get<StreamingSource>("Music/ChaseTheme/Hotel Insanity Chase Theme");
				streamingSource = streamingSource.Duplicate();

				// Volumen directo al 80% y en bucle (true)
				m_sound = new StreamingSound(streamingSource, SettingsManager.MusicVolume * 0.8f, 1f, 0f, false, true, 1f);
				m_sound.Play();
			}
			catch
			{
				Log.Warning("Error reproduciendo Music/ChaseTheme/Hotel Insanity Chase Theme");
			}
		}

		public static void StopMusic()
		{
			if (m_sound != null)
			{
				m_sound.Stop();
				m_sound.Dispose();
				m_sound = null;
			}
		}

		public static void Update()
		{
			// SEGURIDAD: Si salimos del mundo (pantalla de menú, pausa, etc), la música se corta instantáneamente.
			if (ScreensManager.CurrentScreen == null || !(ScreensManager.CurrentScreen is GameScreen))
			{
				if (m_sound != null)
				{
					StopMusic();
				}
				return;
			}

			// Si por alguna razón el sonido se pausó (ej. minimizar ventana) y volvemos, lo reanudamos
			if (m_sound != null && m_sound.State == SoundState.Paused)
			{
				m_sound.Play();
			}
		}

		public static void Dispose()
		{
			StopMusic();
		}
	}
}