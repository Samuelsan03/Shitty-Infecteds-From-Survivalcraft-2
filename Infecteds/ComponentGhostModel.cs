using System;
using Engine;
using Engine.Animation;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentGhostModel : ComponentHumanModel
	{
		public float GhostOpacity
		{
			get
			{
				return m_ghostOpacity;
			}
			set
			{
				m_ghostOpacity = MathUtils.Clamp(value, 0f, 1f);
			}
		}

		public override void Animate()
		{
			base.Animate();

			base.Opacity = new float?((base.Opacity ?? 1f) * m_ghostOpacity);

			if (base.Opacity != null && base.Opacity.Value < 1f)
			{
				bool flag = this.m_componentCreature.ComponentBody.ImmersionFactor >= 1f;
				bool flag2 = this.m_subsystemSky.ViewUnderWaterDepth > 0f;
				this.RenderingMode = ((flag == flag2) ? ModelRenderingMode.TransparentAfterWater : ModelRenderingMode.TransparentBeforeWater);
			}
			else
			{
				this.RenderingMode = ModelRenderingMode.Solid;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_ghostOpacity = valuesDictionary.GetValue<float>("GhostOpacity", 0.5f);
		}

		private float m_ghostOpacity = 0.5f;
	}
}
