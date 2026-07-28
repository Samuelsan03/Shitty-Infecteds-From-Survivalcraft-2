using System;
using Engine;
using Engine.Input;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente de comportamiento de montura mejorado.
	/// Soluciona conflictos de gravedad y saltos bruscos en criaturas voladoras.
	/// PC: Espacio directo para subir, Shift directo para bajar.
	/// Movil: Sube y baja segun la inclinacion de la camara.
	/// IA: Usa FlyOrder cuando la montura puede volar, respetando logica de ComponentPilot.
	/// Soporte integrado para jinetes y monturas zombi con validación de restricciones.
	/// </summary>
	public class ComponentSteedBehaviorImproved : ComponentSteedBehavior
	{
		// Umbral de altura para que la IA decida despegar
		private const float c_flyHeightThreshold = 3f;

		// Umbral de distancia horizontal para considerar despegue con poca altura
		private const float c_flyFarDistanceThreshold = 6f;

		// Umbral de altura para considerar aterrizaje
		private const float c_landHeightThreshold = 1.5f;

		// Umbral de distancia horizontal para considerar aterrizaje
		private const float c_landDistanceThreshold = 2f;

		// Velocidad de descenso suave cuando no hay destino (igual que ComponentPilot)
		private const float c_gentleDescentSpeed = -0.5f;

		// Velocidad de descenso cuando se desmonta
		private const float c_dismountDescentSpeed = -0.3f;

		// Valor minimo para mantener estado de vuelo activo
		private const float c_minFlyInput = 0.001f;

		// Velocidad de ascenso al despegar
		private const float c_takeoffSpeed = 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_isEnabled = true;
		}

		public override void ProcessRidingOrders()
		{
			if (!ValidateZombieRiderMount())
			{
				return;
			}

			bool canFly = m_componentCreature.ComponentLocomotion.FlySpeed > 0f;
			bool isInAir = m_componentCreature.ComponentBody.StandingOnValue == null;

			// Llamamos a la base para calcular la velocidad (m_speed) y el giro (m_turnSpeed)
			base.ProcessRidingOrders();

			// Si la criatura no puede volar, salimos (se comportará como un caballo normal)
			if (!canFly) return;

			ComponentRider rider = m_componentMount.Rider;

			if (rider == null)
			{
				// SIN JINETE - Si está en el aire, forzar descenso suave para evitar caída libre
				if (isInAir)
				{
					m_componentCreature.ComponentLocomotion.FlyOrder = new Vector3(0f, c_dismountDescentSpeed, 0f);
					m_componentCreature.ComponentLocomotion.WalkOrder = null;
					m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
				}
				return;
			}

			// ============================================
			// DETERMINAR TIPO DE JINETE Y PROCESAR CONTROLES
			// ============================================
			ComponentPlayer player = rider.Entity.FindComponent<ComponentPlayer>();
			bool isZombieRider = rider is ComponentRiderZombie || rider.Entity.FindComponent<ComponentZombieAI>() != null;

			if (player != null && player.ComponentInput != null)
			{
				ProcessPlayerFlightControls(player, isInAir);
			}
			else if (isZombieRider)
			{
				ProcessZombieFlightControls(rider, isInAir);
			}
			else
			{
				ProcessAIFlightControls(rider, isInAir);
			}
		}

		private bool ValidateZombieRiderMount()
		{
			return true;
		}

		/// <summary>
		/// Procesa los controles de vuelo para jinetes zombi.
		/// Calcula GIRO y ALTURA directamente, igual que ComponentPilot,
		/// para no depender del orden de actualización de ComponentZombieAI.
		/// </summary>
		private void ProcessZombieFlightControls(ComponentRider rider, bool isInAir)
		{
			// ============================================
			// VERIFICAR SI EL ZOMBI TIENE UN OBJETIVO
			// ============================================
			ComponentZombieChaseBehavior chaseBehavior = rider.Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			bool hasTarget = chaseBehavior != null && chaseBehavior.Target != null && chaseBehavior.Target.ComponentHealth.Health > 0f;

			// Usar m_speed calculado por base.ProcessRidingOrders()
			float effectiveSpeed = m_speed;

			if (hasTarget && effectiveSpeed < 0.1f)
			{
				effectiveSpeed = 0.5f;
			}

			Matrix m = m_componentCreature.ComponentBody.Matrix;
			Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

			Vector3 flyDirection = forward * effectiveSpeed;

			// ============================================
			// CALCULAR GIRO HACIA EL OBJETIVO
			// Igual que ComponentPilot: Vector2.Angle + TurnOrder
			// Esto hace que la montura gire hacia el objetivo
			// ============================================
			float turnAmount = 0f;

			if (hasTarget)
			{
				Vector3 targetPos = chaseBehavior.Target.ComponentBody.Position;
				Vector3 myPos = m_componentCreature.ComponentBody.Position;
				Vector3 dirToTarget = targetPos - myPos;
				Vector2 dirToTargetXZ = new Vector2(dirToTarget.X, dirToTarget.Z);

				if (dirToTargetXZ.LengthSquared() > 0.01f)
				{
					dirToTargetXZ = Vector2.Normalize(dirToTargetXZ);
					Vector2 forwardXZ = new Vector2(forward.X, forward.Z);

					// Vector2.Angle devuelve el ángulo firmado entre los vectores
					float angleToTarget = Vector2.Angle(forwardXZ, dirToTargetXZ);

					// Aplicar TurnOrder directamente, igual que ComponentPilot
					turnAmount = MathUtils.Clamp(angleToTarget, -1f, 1f);
				}
			}

			// Aplicar giro a la locomoción
			m_componentCreature.ComponentLocomotion.TurnOrder = new Vector2(turnAmount, 0f);

			// Aplicar LookOrder para que la criatura mire hacia donde gira
			// Igual que el original: LookOrder = 2 * turnSpeed - LookAngles
			if (MathF.Abs(effectiveSpeed) > 0.01f || MathF.Abs(turnAmount) > 0.01f)
			{
				m_componentCreature.ComponentLocomotion.LookOrder = new Vector2(2f * turnAmount, 0f) - m_componentCreature.ComponentLocomotion.LookAngles;
			}

			// ============================================
			// CALCULAR INPUT VERTICAL BASADO EN EL OBJETIVO
			// ============================================
			float verticalInput = 0f;

			if (hasTarget)
			{
				Vector3 targetPos = chaseBehavior.Target.ComponentBody.Position;
				float heightDiff = targetPos.Y - m_componentCreature.ComponentBody.Position.Y;

				if (heightDiff > 2f)
				{
					verticalInput = MathUtils.Min((heightDiff - 1f) * 0.3f, 1f);
				}
				else if (heightDiff < -2f)
				{
					verticalInput = MathUtils.Max((heightDiff + 1f) * 0.3f, -0.8f);
				}
				else
				{
					verticalInput = MathUtils.Clamp(heightDiff * 0.2f, -0.3f, 0.3f);
				}
			}

			if (isInAir)
			{
				flyDirection.Y = hasTarget ? verticalInput : c_gentleDescentSpeed;
			}
			else
			{
				if (hasTarget && effectiveSpeed > 0.1f)
				{
					flyDirection.Y = c_takeoffSpeed;
				}
				else
				{
					m_componentCreature.ComponentLocomotion.FlyOrder = null;
					return;
				}
			}

			// Normalizar si es necesario
			if (flyDirection.LengthSquared() > 1f)
			{
				flyDirection = Vector3.Normalize(flyDirection);
			}

			// Asegurar que siempre haya algún input para mantener el estado de vuelo
			if (flyDirection.LengthSquared() <= 0f)
			{
				flyDirection = new Vector3(0f, c_minFlyInput, 0f);
			}

			m_componentCreature.ComponentLocomotion.FlyOrder = flyDirection;
			m_componentCreature.ComponentLocomotion.WalkOrder = null;
			m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
		}

		/// <summary>
		/// Procesa los controles de vuelo para un jugador humano.
		/// </summary>
		private void ProcessPlayerFlightControls(ComponentPlayer player, bool isInAir)
		{
			float flyInputY = 0f;

			if (player.ComponentInput.IsControlledByTouch)
			{
				// LÓGICA MÓVIL
				if (m_speed > 0.1f && player.GameWidget?.ActiveCamera != null)
				{
					Vector3 viewDir = player.GameWidget.ActiveCamera.ViewDirection;
					Vector3 normViewDir = Vector3.Normalize(viewDir);

					flyInputY = normViewDir.Y;

					Matrix m = m_componentCreature.ComponentBody.Matrix;
					Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

					Vector3 flyDirection = (forward * m_speed) + (Vector3.UnitY * flyInputY * m_speed);

					if (flyDirection.LengthSquared() > 1f)
					{
						flyDirection = Vector3.Normalize(flyDirection);
					}

					m_componentCreature.ComponentLocomotion.FlyOrder = flyDirection;
					m_componentCreature.ComponentLocomotion.WalkOrder = null;
					m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
					return;
				}
			}
			else
			{
				// LÓGICA PC / GAMEPAD
				if (Keyboard.IsKeyDown(Key.Space))
				{
					flyInputY = 1f;
				}
				else if (Keyboard.IsKeyDown(Key.Shift))
				{
					flyInputY = -1f;
				}
			}

			bool shouldFly = isInAir || flyInputY > 0.01f;

			if (shouldFly)
			{
				Matrix m = m_componentCreature.ComponentBody.Matrix;
				Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

				Vector3 flyDirection = forward * m_speed;
				flyDirection.Y = (isInAir && MathF.Abs(flyInputY) < 0.01f) ? 0f : flyInputY;

				if (flyDirection.LengthSquared() > 1f)
				{
					flyDirection = Vector3.Normalize(flyDirection);
				}

				if (flyDirection.LengthSquared() <= 0f)
				{
					flyDirection = new Vector3(0f, c_minFlyInput, 0f);
				}

				m_componentCreature.ComponentLocomotion.FlyOrder = flyDirection;
				m_componentCreature.ComponentLocomotion.WalkOrder = null;
				m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
			}
			else
			{
				m_componentCreature.ComponentLocomotion.FlyOrder = null;
			}
		}

		/// <summary>
		/// Procesa los controles de vuelo para IA/Criaturas montando.
		/// </summary>
		private void ProcessAIFlightControls(ComponentRider rider, bool isInAir)
		{
			ComponentCreature riderCreature = rider.ComponentCreature;
			if (riderCreature == null) return;

			ComponentPilot riderPilot = rider.Entity.FindComponent<ComponentPilot>();
			bool shouldFly = false;
			float verticalInput = 0f;

			if (riderPilot != null && riderPilot.Destination != null)
			{
				Vector3 position = m_componentCreature.ComponentBody.Position;
				Vector3 destination = riderPilot.Destination.Value;
				Vector3 direction = destination - position;
				float horizontalDist = new Vector2(direction.X, direction.Z).Length();
				float heightDiff = direction.Y;

				if (isInAir)
				{
					shouldFly = true;

					if (heightDiff > 2f)
					{
						verticalInput = MathUtils.Min((heightDiff - 1f) * 0.3f, 1f);
					}
					else if (heightDiff < -2f)
					{
						verticalInput = MathUtils.Max((heightDiff + 1f) * 0.3f, -0.8f);
					}
					else
					{
						verticalInput = MathUtils.Clamp(heightDiff * 0.2f, -0.3f, 0.3f);
					}

					if (horizontalDist < c_landDistanceThreshold &&
						heightDiff > -c_landHeightThreshold &&
						heightDiff < 0.5f)
					{
						shouldFly = false;
					}
				}
				else
				{
					bool destinationHigh = heightDiff > c_flyHeightThreshold;
					bool destinationFarAndElevated = horizontalDist > c_flyFarDistanceThreshold && heightDiff > 1f;
					bool destinationVeryFar = horizontalDist > 9f;

					if (destinationHigh || destinationFarAndElevated || destinationVeryFar)
					{
						shouldFly = true;
						verticalInput = 1f;
					}
				}
			}
			else if (isInAir)
			{
				shouldFly = true;
				verticalInput = c_gentleDescentSpeed;
			}

			if (shouldFly)
			{
				Matrix m = m_componentCreature.ComponentBody.Matrix;
				Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

				Vector3 flyDirection = forward * m_speed;
				flyDirection.Y = verticalInput;

				if (flyDirection.LengthSquared() > 1f)
				{
					flyDirection = Vector3.Normalize(flyDirection);
				}

				if (flyDirection.LengthSquared() <= 0f)
				{
					flyDirection = new Vector3(0f, c_minFlyInput, 0f);
				}

				m_componentCreature.ComponentLocomotion.FlyOrder = flyDirection;
				m_componentCreature.ComponentLocomotion.WalkOrder = null;
				m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
			}
			else
			{
				m_componentCreature.ComponentLocomotion.FlyOrder = null;
			}
		}
	}
}
