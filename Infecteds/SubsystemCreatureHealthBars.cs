using System;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCreatureHealthBars : Subsystem, IDrawable
	{
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private PrimitivesRenderer3D m_primitivesRenderer = new PrimitivesRenderer3D();

		public int[] DrawOrders => new int[] { 202 };

		public void Draw(Camera camera, int drawOrder)
		{
			if (drawOrder != DrawOrders[0]) return;

			// NUEVA LÍNEA: Cancelar el dibujado si está desactivado en los ajustes
			if (!ShittyInfectedsSettings.ShowCreatureHealthBars) return;

			var batch = m_primitivesRenderer.FlatBatch(0, DepthStencilState.None, RasterizerState.CullNoneScissor, BlendState.AlphaBlend);
			var fontBatch = m_primitivesRenderer.FontBatch(LabelWidget.BitmapFont, 0, DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.LinearClamp);

			foreach (var creature in m_subsystemCreatureSpawn.Creatures)
			{
				var health = creature.ComponentHealth;
				if (health == null) continue;

				var body = creature.ComponentBody;
				if (body == null) continue;

				CreatureHealthState state = GetHealthState(health.Health);
				Color color = GetColor(state);

				Vector3 positionWorld = new Vector3(
					body.Position.X,
					body.Position.Y + body.BoxSize.Y + 0.15f,
					body.Position.Z
				);

				Vector3 positionView = Vector3.Transform(positionWorld, camera.ViewMatrix);

				float maxWidth = 0.6f;
				float currentWidth = maxWidth * MathUtils.Saturate(health.Health);
				float halfMaxWidth = maxWidth / 2f;
				float thickness = 0.03f;

				Color backgroundColor = new Color(0, 0, 0, 220);
				Vector3 bg_p1 = positionView + new Vector3(-halfMaxWidth, -thickness, 0f);
				Vector3 bg_p2 = positionView + new Vector3(halfMaxWidth, -thickness, 0f);
				Vector3 bg_p3 = positionView + new Vector3(halfMaxWidth, thickness, 0f);
				Vector3 bg_p4 = positionView + new Vector3(-halfMaxWidth, thickness, 0f);
				batch.QueueQuad(bg_p1, bg_p2, bg_p3, bg_p4, backgroundColor);

				float healthEndX = -halfMaxWidth + currentWidth;

				Vector3 hp_p1 = positionView + new Vector3(-halfMaxWidth, -thickness, 0.001f);
				Vector3 hp_p2 = positionView + new Vector3(healthEndX, -thickness, 0.001f);
				Vector3 hp_p3 = positionView + new Vector3(healthEndX, thickness, 0.001f);
				Vector3 hp_p4 = positionView + new Vector3(-halfMaxWidth, thickness, 0.001f);
				batch.QueueQuad(hp_p1, hp_p2, hp_p3, hp_p4, color);

				float textScale = 0.003f;

				string creatureName = !string.IsNullOrEmpty(creature.DisplayName) ? creature.DisplayName : "NPC";
				int healthValue = (int)Math.Round(health.Health * health.AttackResilience);

				string format = LanguageControl.Get("SubsystemCreatureHealthBars", 1);
				string healthText = string.Format(format, creatureName, healthValue);

				Vector3 textPositionView = positionView + new Vector3(0f, thickness + 0.005f, 0.002f);

				float fontScaleCorrection = LabelWidget.BitmapFont.Scale;
				Vector3 right = new Vector3(textScale / fontScaleCorrection, 0f, 0f);
				Vector3 down = new Vector3(0f, -textScale / fontScaleCorrection, 0f);

				Color textColor = color;
				Color shadowColor = new Color(0, 0, 0, 200);

				Vector3 shadowOffset = new Vector3(0.001f, -0.001f, 0f);
				fontBatch.QueueText(healthText, textPositionView + shadowOffset, right, down, shadowColor, TextAnchor.HorizontalCenter | TextAnchor.Bottom);

				fontBatch.QueueText(healthText, textPositionView, right, down, textColor, TextAnchor.HorizontalCenter | TextAnchor.Bottom);
			}

			m_primitivesRenderer.Flush(camera.ProjectionMatrix, true, int.MaxValue);
		}

		private CreatureHealthState GetHealthState(float health)
		{
			if (health <= 0f) return CreatureHealthState.Dead;
			if (health <= 0.25f) return CreatureHealthState.LowHealth;
			if (health <= 0.5f) return CreatureHealthState.Half;
			return CreatureHealthState.Alive;
		}

		private Color GetColor(CreatureHealthState state)
		{
			switch (state)
			{
				case CreatureHealthState.Alive: return new Color(50, 255, 50);
				case CreatureHealthState.Half: return new Color(255, 255, 50);
				case CreatureHealthState.LowHealth: return new Color(255, 50, 50);
				case CreatureHealthState.Dead: return new Color(120, 20, 20);
				default: return Color.White;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
		}

		public enum CreatureHealthState
		{
			Alive,
			Half,
			LowHealth,
			Dead
		}
	}
}
