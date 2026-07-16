using System;
using Engine;
using Engine.Graphics;
using Engine.Input;

namespace Game
{
	public class FreeCamera : BasePerspectiveCamera
	{
		public override bool UsesMovementControls
		{
			get
			{
				return true;
			}
		}

		public override bool IsEntityControlEnabled
		{
			get
			{
				return true;
			}
		}

		public FreeCamera(GameWidget gameWidget) : base(gameWidget)
		{
		}

		public override void Activate(Camera previousCamera)
		{
			this.m_position = previousCamera.ViewPosition;
			this.m_direction = previousCamera.ViewDirection;
			base.SetupPerspectiveCamera(this.m_position, this.m_direction, Vector3.UnitY);
		}

		public override void Update(float dt)
		{
			dt = MathUtils.Min(dt, 0.1f);
			Vector3 vector = Vector3.Zero;
			Vector2 vector2 = Vector2.Zero;
			ComponentPlayer componentPlayer = base.GameWidget.PlayerData.ComponentPlayer;
			ComponentInput componentInput = (componentPlayer != null) ? componentPlayer.ComponentInput : null;
			if (componentInput != null)
			{
				vector = componentInput.PlayerInput.CameraMove * new Vector3(1f, 0f, 1f);
				vector2 = componentInput.PlayerInput.CameraLook;
			}
			bool flag = Keyboard.IsKeyDown(Key.Shift);
			bool flag2 = Keyboard.IsKeyDown(Key.Control);
			Vector3 direction = this.m_direction;
			Vector3 unitY = Vector3.UnitY;
			Vector3 vector3 = Vector3.Normalize(Vector3.Cross(direction, unitY));
			float num = 8f;
			if (flag)
			{
				num *= 10f;
			}
			if (flag2)
			{
				num /= 10f;
			}
			Vector3 vector4 = Vector3.Zero;
			vector4 += num * vector.X * vector3;
			vector4 += num * vector.Y * unitY;
			vector4 += num * vector.Z * direction;
			this.m_position += vector4 * dt;
			this.m_direction = Vector3.Transform(this.m_direction, Matrix.CreateFromAxisAngle(unitY, -4f * vector2.X * dt));
			this.m_direction = Vector3.Transform(this.m_direction, Matrix.CreateFromAxisAngle(vector3, 4f * vector2.Y * dt));
			base.SetupPerspectiveCamera(this.m_position, this.m_direction, Vector3.UnitY);
			Vector2 v = this.ViewportSize / 2f;
			FlatBatch2D flatBatch2D = this.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, null);
			int count = flatBatch2D.LineVertices.Count;
			flatBatch2D.QueueLine(v - new Vector2(5f, 0f), v + new Vector2(5f, 0f), 0f, Color.White);
			flatBatch2D.QueueLine(v - new Vector2(0f, 5f), v + new Vector2(0f, 5f), 0f, Color.White);
			flatBatch2D.TransformLines(this.ViewportMatrix, count, -1);
			this.PrimitivesRenderer2D.Flush(true, int.MaxValue);
		}

		public static string AmbientParameters = string.Empty;

		public static string PlantParameters = string.Empty;

		public Vector3 m_position;

		public Vector3 m_direction;

		public PrimitivesRenderer2D PrimitivesRenderer2D = new PrimitivesRenderer2D();
	}
}
