using System;
using System.Collections.Generic;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InfectedsMusicManager
	{
		// Tipos de música que administra el manager
		public enum MusicType
		{
			// Persecución normal: bucle continuo, volumen al 80%
			Chase,
			// Persecución de jefe: sin bucle nativo (reinicio manual), volumen de MusicManager
			BossChase,
			// Música de muerte del jugador: bucle continuo hasta reaparecer
			Death
		}

		// Canal independiente por tipo (permite que dos temas suenen a la vez, como antes)
		private class MusicChannel
		{
			public StreamingSound Sound;
			public string Path;
			public float PlayTime;
			public float Duration;

			// Estado de fade-out
			public bool FadingOut;
			public float FadeElapsed;
			public float FadeDuration;
			public float FadeStartVolume;
		}

		private static Dictionary<MusicType, MusicChannel> m_channels = new Dictionary<MusicType, MusicChannel>();
		private static bool m_initialized;
		private static double m_lastFadeTick = -1.0;
		/// Inicia un fade-out sobre el canal indicado. No hace nada si no hay sonido.
		public static void FadeOut(MusicType type, float duration)
		{
			MusicChannel channel = GetChannel(type);
			if (channel.Sound == null || channel.FadingOut) return;

			channel.FadingOut = true;
			channel.FadeElapsed = 0f;
			channel.FadeDuration = MathUtils.Max(0.01f, duration);
			channel.FadeStartVolume = channel.Sound.Volume;
		}

		/// Debe llamarse cada frame (por ejemplo desde AfterWidgetUpdate).
		/// Se protege contra múltiples llamadas por frame con un chequeo de tiempo.
		public static void UpdateFades()
		{
			// DEFENSIVO: si la música de muerte está desactivada, cortarla siempre.
			// Esto cubre el caso en el que el Stop del toggle no llegó a ejecutarse
			// (por ejemplo si se reactivó el sonido por otro camino) o si el ajuste
			// se cambió mientras el canal Death seguía sonando.
			if (!ShittyInfectedsSettings.EnableDeathMusic)
			{
				MusicChannel deathChannel;
				if (m_channels.TryGetValue(MusicType.Death, out deathChannel) && deathChannel.Sound != null)
				{
					Stop(MusicType.Death);
				}
			}

			double now = Time.FrameStartTime;
			if (m_lastFadeTick < 0.0)
			{
				m_lastFadeTick = now;
				return;
			}
			float dt = (float)(now - m_lastFadeTick);
			m_lastFadeTick = now;
			if (dt <= 0f) return;

			List<MusicType> toStop = null;
			foreach (KeyValuePair<MusicType, MusicChannel> pair in m_channels)
			{
				MusicChannel channel = pair.Value;
				if (!channel.FadingOut || channel.Sound == null) continue;

				channel.FadeElapsed += dt;
				float t = MathUtils.Clamp(channel.FadeElapsed / channel.FadeDuration, 0f, 1f);
				try
				{
					channel.Sound.Volume = channel.FadeStartVolume * (1f - t);
				}
				catch
				{
				}

				if (t >= 1f)
				{
					if (toStop == null) toStop = new List<MusicType>();
					toStop.Add(pair.Key);
				}
			}
			if (toStop != null)
			{
				foreach (MusicType k in toStop) Stop(k);
			}
		}

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
				channel.FadingOut = false;
				channel.FadeElapsed = 0f;
				channel.FadeDuration = 0f;

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
				channel.FadingOut = false;
				channel.FadeElapsed = 0f;
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
					loop = false;
					volume = MusicManager.Volume;
					break;
				case MusicType.Death:
					// Bucle continuo mientras el jugador esté muerto
					loop = true;
					volume = MusicManager.Volume;
					break;
				case MusicType.Chase:
				default:
					loop = true;
					volume = SettingsManager.MusicVolume * 0.8f;
					break;
			}
		}
	}
}
