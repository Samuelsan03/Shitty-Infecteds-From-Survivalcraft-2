using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDefensiveRunAway : ComponentBehavior, IUpdateable, INoiseListener, IComponentEscapeBehavior
	{
		public float LowHealthToEscape { get; set; }

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return 0f; }
		}

		public virtual void RunAwayFrom(ComponentBody componentBody)
		{
			// No huye de los atacantes.
		}

		public virtual void Update(float dt)
		{
			// Sin lógica de actualización.
		}

		public virtual void HearNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
			// Ignora los ruidos.
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape", 0f);
		}

		public virtual Vector3 FindSafePlace()
		{
			return Vector3.Zero;
		}

		public virtual float ScoreSafePlace(
			Vector3 currentPosition,
			Vector3 safePosition,
			Vector3? herdPosition,
			Vector3? noiseSourcePosition,
			int contents)
		{
			return 0f;
		}
	}
}
