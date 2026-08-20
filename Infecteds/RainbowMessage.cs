using System;
using Engine;
using Engine.Media;

namespace Game
{
	public class RainbowMessage : MessageWidget.Message
	{
		private double m_creationTime;

		public RainbowMessage(string text) : base(text, Color.White, false, 1f)
		{
			m_creationTime = Time.FrameStartTime;
		}

		public override void Update()
		{
			// Fade idéntico al mensaje original (no blinking)
			float fade = MathUtils.Saturate(MathUtils.Min(
				3f * (float)(Time.FrameStartTime - StartTime),
				1f * (float)(StartTime + (double)Duration - Time.FrameStartTime)
			));

			// Color arcoíris que cambia con el tiempo
			float elapsed = (float)(Time.FrameStartTime - m_creationTime);
			float hue = (elapsed * 150f) % 360f;
			Vector3 rgb = Color.HsvToRgb(new Vector3(hue, 1f, 1f));

			// Aplicar fade al color arcoíris
			LabelWidget.Color = new Color(
				(byte)(rgb.X * 255f),
				(byte)(rgb.Y * 255f),
				(byte)(rgb.Z * 255f),
				(byte)(255f * fade)
			);
		}
	}
}
