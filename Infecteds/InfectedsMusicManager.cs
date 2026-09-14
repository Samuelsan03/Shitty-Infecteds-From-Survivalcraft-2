using System;
using System.Collections.Generic;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InfectedsMusicManager
	{
		// Tipos de música que administra el manager (por ahora solo persecución)
		public enum MusicType
		{
			// Persecución normal: bucle continuo, volumen al 80%
			Chase,
			// Persecución de jefe: sin bucle nativo (reinicio manual), volumen de MusicManager
			BossChase
		}

		// Canal independiente por tipo (permite que dos temas suenen a la vez, como antes)
		private class MusicChannel
		{
			public StreamingSound Sound;
			public string Path;
			public float PlayTime;
			public float Duration;
		}

		private static Dictionary<MusicType, MusicChannel> m_channels = new Dictionary<MusicType, MusicChannel>();
		private static bool m_initialized;

		public static bool IsPlaying(MusicType type)
		{
			return GetChannel(type).Sound != null && GetChannel(type).Sound.State == SoundState.Playing;
		}

		public static void Update(bool isChasing, float dt, string path, MusicType type)
		{
			// SEGURIDAD: si salimos del mundo (menú, pausa, etc.), cortamos todo al instante
			if (ScreensManager.CurrentScreen == null || !(ScreensManager.CurrentScreen is GameScreen))
			{
				StopAll();
				return;
			}

			// Si la criatura dejó de perseguir (o la opción está desactivada), detenemos su música
			if (!isChasing)
			{
				Stop(type);
				return;
			}

			bool loop;
			float volume;
			GetMusicSettings(type, out loop, out volume);

			MusicChannel channel = GetChannel(type);

			// Si el sonido se pausó (ej. minimizar ventana) y seguimos persiguiendo, lo reanudamos
			if (channel.Sound != null && channel.Sound.State == SoundState.Paused)
			{
				channel.Sound.Play();
			}

			// Si no hay sonido, cambió la ruta, se detuvo solo, o (sin bucle nativo) terminó su duración -> reiniciar
			if (channel.Sound == null || channel.Path != path || channel.Sound.State <= SoundState.Stopped || (!loop && channel.PlayTime >= channel.Duration))
			{
				Play(path, type);
			}
			else
			{
				// Sumamos tiempo solo si está sonando
				channel.PlayTime += dt;
			}
		}

		public static void Play(string path, MusicType type)
		{
			if (string.IsNullOrEmpty(path))
			{
				Stop(type);
				return;
			}
			try
			{
				Stop(type);

				StreamingSource source = ContentManager.Get<StreamingSource>(path);
				source = source.Duplicate();

				bool loop;
				float volume;
				GetMusicSettings(type, out loop, out volume);

				MusicChannel channel = GetChannel(type);

				// Duración exacta del audio (PCM 16 bits)
				channel.Duration = (float)((double)source.BytesCount / (double)source.ChannelsCount / 2.0 / (double)source.SamplingFrequency);
				channel.PlayTime = 0f;
				channel.Path = path;

				channel.Sound = new StreamingSound(source, volume, 1f, 0f, false, loop, 1f);
				channel.Sound.Play();
			}
			catch (Exception ex)
			{
				Log.Warning("Error playing infecteds music \"" + path + "\": " + ex.Message);
				MusicChannel channel = GetChannel(type);
				channel.Sound = null;
				channel.Path = null;
				channel.PlayTime = 0f;
			}
		}

		public static void Stop(MusicType type)
		{
			MusicChannel channel = GetChannel(type);
			if (channel.Sound != null)
			{
				channel.Sound.Stop();
				channel.Sound.Dispose();
				channel.Sound = null;
				channel.Path = null;
				channel.PlayTime = 0f;
			}
		}

		public static void StopAll()
		{
			foreach (KeyValuePair<MusicType, MusicChannel> pair in m_channels)
			{
				Stop(pair.Key);
			}
		}

		public static void Initialize()
		{
			if (m_initialized) return;
			m_initialized = true;

			// Por seguridad, si cierran el juego de golpe, limpiamos el audio
			Window.Closed += delegate
			{
				try
				{
					StopAll();
				}
				catch
				{
				}
			};
		}

		public static void Dispose()
		{
			StopAll();
		}

		private static MusicChannel GetChannel(MusicType type)
		{
			MusicChannel channel;
			if (!m_channels.TryGetValue(type, out channel))
			{
				channel = new MusicChannel();
				m_channels.Add(type, channel);
			}
			return channel;
		}

		// Aquí vive la lógica original de cada manager antiguo (bucle y volumen por tipo)
		private static void GetMusicSettings(MusicType type, out bool loop, out float volume)
		{
			switch (type)
			{
				case MusicType.BossChase:
					// Original de BossChaseMusicManager: loop en false (reinicio manual), volumen de MusicManager
					loop = false;
					volume = MusicManager.Volume;
					break;
				case MusicType.Chase:
				default:
					// Original de ChaseMusicManager: en bucle (true), volumen directo al 80%
					loop = true;
					volume = SettingsManager.MusicVolume * 0.8f;
					break;
			}
		}
	}
}
