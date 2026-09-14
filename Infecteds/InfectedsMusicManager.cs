using System;
using System.Collections.Generic;
using Engine;
using Engine.Audio;
using Engine.Media;

namespace Game
{
	public static class InfectedsMusicManager
	{
		public enum MusicType
		{
			Chase,
			BossChase,
			Death
		}

		// Duración por defecto del fade-out cuando la persecución termina
		// o cuando el zombi cambia de presa.
		public const float ChaseFadeOutDuration = 1.5f;

		private class MusicChannel
		{
			public StreamingSound Sound;
			public string Path;
			public float PlayTime;
			public float Duration;

			public bool FadingOut;
			public float FadeElapsed;
			public float FadeDuration;
			public float FadeStartVolume;
		}

		private static Dictionary<MusicType, MusicChannel> m_channels = new Dictionary<MusicType, MusicChannel>();
		private static bool m_initialized;
		private static double m_lastFadeTick = -1.0;

		/// <summary>Inicia un fade-out sobre el canal indicado. No hace nada si no hay sonido.</summary>
		public static void FadeOut(MusicType type, float duration)
		{
			MusicChannel channel = GetChannel(type);
			if (channel.Sound == null || channel.FadingOut) return;

			channel.FadingOut = true;
			channel.FadeElapsed = 0f;
			channel.FadeDuration = MathUtils.Max(0.01f, duration);
			channel.FadeStartVolume = channel.Sound.Volume;
		}

		/// <summary>
		/// Debe llamarse cada frame (por ejemplo desde AfterWidgetUpdate).
		/// Se protege contra múltiples llamadas por frame con un chequeo de tiempo.
		/// </summary>
		public static void UpdateFades()
		{
			// DEFENSIVO: si la música de muerte está desactivada, cortarla siempre.
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

			// CAMBIO: ya no cortamos en seco al terminar la persecución.
			// Aplicamos un fade-out suave (jugador perdido, presa alejada,
			// presa muerta...). El cambio de presa lo dispara el subsistema
			// llamando directamente a FadeOut, ya que aquí no sabemos de entidades.
			if (!isChasing)
			{
				MusicChannel channel = GetChannel(type);
				if (channel.Sound != null && !channel.FadingOut)
				{
					FadeOut(type, ChaseFadeOutDuration);
				}
				return;
			}

			bool loop;
			float volume;
			GetMusicSettings(type, out loop, out volume);

			MusicChannel ch = GetChannel(type);

			if (ch.Sound != null && ch.Sound.State == SoundState.Paused)
			{
				ch.Sound.Play();
			}

			// Si no hay sonido, cambió la ruta, se detuvo solo, o (sin bucle nativo) terminó su duración -> reiniciar.
			// IMPORTANTE: un fade en curso NO reinicia el sonido; se deja terminar y, cuando el
			// canal quede vacío, el siguiente frame lo vuelve a lanzar limpio (fade out → restart).
			if (ch.Sound == null || ch.Path != path || ch.Sound.State <= SoundState.Stopped || (!loop && ch.PlayTime >= ch.Duration))
			{
				Play(path, type);
			}
			else
			{
				ch.PlayTime += dt;
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

				channel.Duration = (float)((double)source.BytesCount / (double)source.ChannelsCount / 2.0 / (double)source.SamplingFrequency);
				channel.PlayTime = 0f;
				channel.Path = path;
				channel.FadingOut = false;
				channel.FadeElapsed = 0f;
				channel.FadeDuration = 0f;

				// Orden correcto: (source, volume, pitch, pan, isLooped, disposeSource, ...)
				channel.Sound = new StreamingSound(source, volume, 1f, 0f, loop, true, 1f);
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

		private static void GetMusicSettings(MusicType type, out bool loop, out float volume)
		{
			switch (type)
			{
				case MusicType.BossChase:
					loop = true;
					volume = MusicManager.Volume;
					break;
				case MusicType.Death:
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
