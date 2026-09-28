using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentBanditRunAwayBehavior : ComponentBehavior, IUpdateable, INoiseListener, IComponentEscapeBehavior
	{
		public float LowHealthToEscape { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				// El bandido nunca considera necesario huir.
				return 0f;
			}
		}

		public virtual void RunAwayFrom(ComponentBody componentBody)
		{
			// Intencionalmente vacío.
			// El bandido no huye de atacantes.
		}

		public virtual void Update(float dt)
		{
			// No existe máquina de estados de huida.
			// Se ignoran daño, ruido y salud baja.
		}

		public virtual void HearNoise(
			ComponentBody sourceBody,
			Vector3 sourcePosition,
			float loudness)
		{
			// Intencionalmente vacío.
			// El bandido no huye de los ruidos.
		}

		public override void Load(
			ValuesDictionary valuesDictionary,
			IdToEntityMap idToEntityMap)
		{
			LowHealthToEscape = valuesDictionary.GetValue<float>(
				"LowHealthToEscape",
				0f);

			// No registramos ComponentHealth.Injured.
			// Por lo tanto, recibir daño no activa ninguna huida.
		}

		public virtual Vector3 FindSafePlace()
		{
			// El bandido no busca lugares seguros.
			return Vector3.Zero;
		}

		public virtual float ScoreSafePlace(
			Vector3 currentPosition,
			Vector3 safePosition,
			Vector3? herdPosition,
			Vector3? noiseSourcePosition,
			int contents)
		{
			// No se utiliza porque el bandido nunca huye.
			return 0f;
		}
	}
}
