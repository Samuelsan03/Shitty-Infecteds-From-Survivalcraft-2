using System;
using Engine;
using Engine.Graphics;
using Engine.Input;

namespace Game
{
	public class SniperScopeCamera : BasePerspectiveCamera
	{
		public override bool UsesMovementControls
		{
			get { return false; }
		}

		public override bool IsEntityControlEnabled
		{
			get { return true; }
		}

		private float m_zoomLevel;
		private float m_targetZoomLevel;

		private const float MinZoomLevel = 0.2f;
		private const float MaxZoomLevel = 1.0f;
		private const float ZoomSpeed = 1.5f;
		private const float BaseFOV = 80f;
		private const float ZoomThreshold = 0.95f;

		private float m_scopeVignetteAlpha;
		private float m_targetScopeVignetteAlpha;

		// Para bloquear posición
		private bool m_positionLocked = false;
		private Vector3 m_lockedPosition;

		public float ZoomLevel
		{
			get { return m_zoomLevel; }
		}

		public float ScopeVignetteAlpha
		{
			get { return m_scopeVignetteAlpha; }
		}

		public SniperScopeCamera(GameWidget gameWidget) : base(gameWidget)
		{
			m_zoomLevel = MaxZoomLevel;
			m_targetZoomLevel = MaxZoomLevel;
			m_scopeVignetteAlpha = 0f;
			m_targetScopeVignetteAlpha = 0f;
		}

		public override void Activate(Camera previousCamera)
		{
			base.SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
			m_zoomLevel = MaxZoomLevel;
			m_targetZoomLevel = MaxZoomLevel;
			m_scopeVignetteAlpha = 0f;
			m_targetScopeVignetteAlpha = 0f;
			m_positionLocked = false;
		}

		public override void Update(float dt)
		{
			if (base.GameWidget.Target == null)
			{
				return;
			}

			ComponentCreature target = base.GameWidget.Target;

			if (target != null)
			{
				Matrix matrix = Matrix.CreateFromQuaternion(target.ComponentCreatureModel.EyeRotation);
				matrix.Translation = target.ComponentCreatureModel.EyePosition;
				base.SetupPerspectiveCamera(matrix.Translation, matrix.Forward, matrix.Up);
			}

			if (Keyboard.IsKeyDown(Key.W))
			{
				m_targetZoomLevel -= ZoomSpeed * dt;
			}
			if (Keyboard.IsKeyDown(Key.S))
			{
				m_targetZoomLevel += ZoomSpeed * dt;
			}

			if (Keyboard.IsKeyDown(Key.UpArrow))
			{
				m_targetZoomLevel -= ZoomSpeed * dt;
			}
			if (Keyboard.IsKeyDown(Key.DownArrow))
			{
				m_targetZoomLevel += ZoomSpeed * dt;
			}

			m_targetZoomLevel = MathUtils.Clamp(m_targetZoomLevel, MinZoomLevel, MaxZoomLevel);
			m_zoomLevel = MathUtils.Lerp(m_zoomLevel, m_targetZoomLevel, 1f - MathF.Pow(0.001f, dt));

			// BLOQUEAR POSICIÓN AL HACER ZOOM
			if (m_zoomLevel < ZoomThreshold && target != null)
			{
				ComponentBody body = target.ComponentBody;
				if (body != null)
				{
					// Guardar posición cuando recién entra en zoom
					if (!m_positionLocked)
					{
						m_lockedPosition = body.Position;
						m_positionLocked = true;
					}
					// Forzar posición bloqueada (ignora movimiento horizontal)
					body.Position = new Vector3(m_lockedPosition.X, body.Position.Y, m_lockedPosition.Z);
					// Anular velocidad horizontal
					body.Velocity = new Vector3(0f, body.Velocity.Y, 0f);
				}
			}
			else
			{
				m_positionLocked = false;
			}

			if (m_zoomLevel < 0.5f)
			{
				m_targetScopeVignetteAlpha = MathUtils.Saturate((0.5f - m_zoomLevel) / 0.3f);
			}
			else
			{
				m_targetScopeVignetteAlpha = 0f;
			}
			m_scopeVignetteAlpha = MathUtils.Lerp(m_scopeVignetteAlpha, m_targetScopeVignetteAlpha, 1f - MathF.Pow(0.01f, dt));
		}

		public override Matrix CalculateBaseProjectionMatrix()
		{
			float fov = BaseFOV * SettingsManager.ViewAngle * m_zoomLevel;

			ViewWidget viewWidget = base.GameWidget.ViewWidget;
			float aspectRatio = viewWidget.ActualSize.X / viewWidget.ActualSize.Y;

			Matrix result = Matrix.CreatePerspectiveFieldOfView(MathUtils.DegToRad(fov), aspectRatio, 0.1f, 2048f);

			return result;
		}

		public float GetZoomMultiplier()
		{
			return 1f / m_zoomLevel;
		}
	}
}
