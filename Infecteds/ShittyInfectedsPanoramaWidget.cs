using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ShittyInfectedsPanoramaWidget : Widget
	{

		public static readonly List<string> WallpaperPaths = new List<string>
		{
			"Wallpapers/fight left 4 dead",
			"Wallpapers/left 4 dead two",
			"Wallpapers/tissous-zombie-pack-texture-minecraft",
			"Wallpapers/maksat-zombie-apocalypse-1",
			"Wallpapers/left 4 dead 1"
            // Agregar más fondos aquí
        };

		public PanoramaState CurrentState { get; private set; }
		public float TransitionDuration { get; set; }
		public float DisplayDuration { get; set; }

		private Texture2D m_currentTexture;
		private Texture2D m_nextTexture;
		private float m_transitionProgress;
		private float m_timeUntilChange;
		private Game.Random m_random;
		private int m_currentIndex;
		private double m_lastFrameTime;

		public ShittyInfectedsPanoramaWidget()
		{
			m_random = new Game.Random();
			CurrentState = PanoramaState.FadingFromBlack;
			m_transitionProgress = 0f;
			TransitionDuration = 0.5f;
			DisplayDuration = 5f;
			m_timeUntilChange = DisplayDuration;
			m_lastFrameTime = Time.FrameStartTime;

			LoadRandomWallpaper();
		}

		private void LoadRandomWallpaper()
		{
			if (WallpaperPaths.Count == 0)
				return;

			m_currentIndex = m_random.Int(WallpaperPaths.Count);
			m_currentTexture = ContentManager.Get<Texture2D>(WallpaperPaths[m_currentIndex]);
		}

		private void PrepareNextWallpaper()
		{
			if (WallpaperPaths.Count <= 1)
				return;

			int nextIndex;
			do
			{
				nextIndex = m_random.Int(WallpaperPaths.Count);
			} while (nextIndex == m_currentIndex);

			m_nextTexture = ContentManager.Get<Texture2D>(WallpaperPaths[nextIndex]);
			m_currentIndex = nextIndex;
		}

		private void StartTransition()
		{
			if (WallpaperPaths.Count <= 1)
				return;

			PrepareNextWallpaper();
			CurrentState = PanoramaState.FadingToBlack;
			m_transitionProgress = 0f;
		}

		public override void MeasureOverride(Vector2 parentAvailableSize)
		{
			base.IsDrawRequired = true;
		}

		private void UpdateTransition()
		{
			double currentTime = Time.FrameStartTime;
			float deltaTime = MathUtils.Min((float)(currentTime - m_lastFrameTime), 0.1f);
			m_lastFrameTime = currentTime;

			if (CurrentState == PanoramaState.DisplayingWallpaper)
			{
				m_timeUntilChange -= deltaTime;
				if (m_timeUntilChange <= 0f)
				{
					StartTransition();
				}
			}
			else if (CurrentState == PanoramaState.FadingToBlack)
			{
				m_transitionProgress += deltaTime / TransitionDuration;
				if (m_transitionProgress >= 1f)
				{
					m_transitionProgress = 1f;
					m_currentTexture = m_nextTexture;
					m_nextTexture = null;
					CurrentState = PanoramaState.FadingFromBlack;
					m_transitionProgress = 0f;
				}
			}
			else if (CurrentState == PanoramaState.FadingFromBlack)
			{
				m_transitionProgress += deltaTime / TransitionDuration;
				if (m_transitionProgress >= 1f)
				{
					m_transitionProgress = 1f;
					CurrentState = PanoramaState.DisplayingWallpaper;
					m_transitionProgress = 0f;
					m_timeUntilChange = DisplayDuration;
				}
			}
		}

		public override void Draw(Widget.DrawContext dc)
		{
			if (m_currentTexture == null)
				return;

			UpdateTransition();

			DrawFullscreenTexture(dc, m_currentTexture);

			if (CurrentState == PanoramaState.FadingToBlack)
			{
				DrawBlackOverlay(dc, m_currentTexture, m_transitionProgress);
			}
			else if (CurrentState == PanoramaState.FadingFromBlack)
			{
				DrawBlackOverlay(dc, m_currentTexture, 1f - m_transitionProgress);
			}
		}

		private void DrawBlackOverlay(Widget.DrawContext dc, Texture2D texture, float alpha)
		{
			if (alpha <= 0f || texture == null)
				return;

			// Se usa el mismo TexturedBatch para garantizar el orden de dibujo (encima de la imagen)
			TexturedBatch2D texturedBatch = dc.PrimitivesRenderer2D.TexturedBatch(
				texture,
				false,
				0,
				DepthStencilState.DepthWrite,
				null,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp);

			int count = texturedBatch.TriangleVertices.Count;
			Color color = new Color(0f, 0f, 0f, alpha);
			texturedBatch.QueueQuad(Vector2.Zero, base.ActualSize, 1f, Vector2.Zero, Vector2.One, color);
			texturedBatch.TransformTriangles(base.GlobalTransform, count, -1);
		}

		private void DrawFullscreenTexture(Widget.DrawContext dc, Texture2D texture)
		{
			if (texture == null)
				return;

			Vector2 screenPos = Vector2.Zero;
			Vector2 screenSize = base.ActualSize;

			Vector2 texCoord;
			Vector2 texCoord2;
			CalculateCoverCoordinates(texture, screenSize, out texCoord, out texCoord2);

			TexturedBatch2D texturedBatch = dc.PrimitivesRenderer2D.TexturedBatch(
				texture,
				false,
				0,
				DepthStencilState.DepthWrite,
				null,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp);

			int count = texturedBatch.TriangleVertices.Count;
			texturedBatch.QueueQuad(screenPos, screenSize, 1f, texCoord, texCoord2, base.GlobalColorTransform);
			texturedBatch.TransformTriangles(base.GlobalTransform, count, -1);
		}

		private void CalculateCoverCoordinates(Texture2D texture, Vector2 screenSize, out Vector2 texCoord, out Vector2 texCoord2)
		{
			float screenAspect = screenSize.X / screenSize.Y;
			float textureAspect = (float)texture.Width / (float)texture.Height;

			if (screenAspect > textureAspect)
			{
				float scale = textureAspect / screenAspect;
				float offset = (1f - scale) * 0.5f;
				texCoord = new Vector2(offset, 0f);
				texCoord2 = new Vector2(1f - offset, 1f);
			}
			else
			{
				float scale = screenAspect / textureAspect;
				float offset = (1f - scale) * 0.5f;
				texCoord = new Vector2(0f, offset);
				texCoord2 = new Vector2(1f, 1f - offset);
			}
		}

		public enum PanoramaState
		{
			DisplayingWallpaper,
			FadingToBlack,
			FadingFromBlack
		}
	}
}
