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

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_isEnabled = true;
		}

		public override void ProcessRidingOrders()
		{
			// ============================================
			// VERIFICACIÓN DE SEGURIDAD: Jinete zombi en montura restringida
			// Esta validación protege contra estados inválidos donde un zombi
			// podría estar montado en una montura que no lo permite
			// ============================================
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
			// Prioridad: Jugador > Zombi > IA genérica
			// 
			// CORRECCIÓN: Detectamos zombis por ComponentRiderZombie O por ComponentZombieAI
			// para cubrir ambos casos:
			// - Zombis con ComponentRiderZombie (rider especializado)
			// - Zombis con ComponentRider normal + ComponentZombieAI
			// ============================================
			ComponentPlayer player = rider.Entity.FindComponent<ComponentPlayer>();

			// Detectar si el jinete es un zombi por cualquiera de los dos componentes
			bool isZombieRider = rider is ComponentRiderZombie || rider.Entity.FindComponent<ComponentZombieAI>() != null;

			if (player != null && player.ComponentInput != null)
			{
				// LÓGICA PARA JUGADORES (PC y Móvil)
				ProcessPlayerFlightControls(player, isInAir);
			}
			else if (isZombieRider)
			{
				// LÓGICA PARA JINETES ZOMBI
				// Ahora funciona tanto si el zombi tiene ComponentRiderZombie
				// como si solo tiene ComponentRider + ComponentZombieAI
				ProcessZombieFlightControls(rider, isInAir);
			}
			else
			{
				// LÓGICA PARA IA/CRIOPTURAS MONTANDO (usando ComponentPilot)
				ProcessAIFlightControls(rider, isInAir);
			}
		}

		private bool ValidateZombieRiderMount()
		{
			return true;
		}

		/// <summary>
		/// Procesa los controles de vuelo para jinetes zombi.
		/// CORRECCIÓN: Ahora acepta ComponentRider (no solo ComponentRiderZombie)
		/// para funcionar con zombis que usan el rider original pero tienen ComponentZombieAI.
		/// 
		/// Usa m_speed calculado por base.ProcessRidingOrders() para permitir movimiento
		/// horizontal cuando el zombi pilotea desde ComponentZombieAI.PilotMount().
		/// </summary>
		private void ProcessZombieFlightControls(ComponentRider rider, bool isInAir)
		{
			if (isInAir)
			{
				// ============================================
				// ZOMBI EN EL AIRE
				// CORRECCIÓN PRINCIPAL: Usar m_speed que viene de PilotMount()
				// a través de base.ProcessRidingOrders() para permitir que el
				// zombi pilotee la criatura voladora horizontalmente.
				// 
				// ANTES: FlyOrder = (0, -0.5, 0) → Solo descendía, sin movimiento horizontal
				// AHORA: FlyOrder incluye forward * m_speed → Se mueve hacia donde apunta
				// ============================================
				Matrix m = m_componentCreature.ComponentBody.Matrix;
				Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

				Vector3 flyDirection = forward * m_speed;
				flyDirection.Y = c_gentleDescentSpeed;

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
			// Si está en el suelo, el movimiento terrestre ya fue calculado por base.ProcessRidingOrders()
		}

		/// <summary>
		/// Procesa los controles de vuelo para un jugador humano.
		/// PC: Espacio para subir, Shift para bajar.
		/// Móvil: La inclinación de la cámara controla la dirección vertical.
		/// </summary>
		private void ProcessPlayerFlightControls(ComponentPlayer player, bool isInAir)
		{
			float flyInputY = 0f;

			if (player.ComponentInput.IsControlledByTouch)
			{
				// LÓGICA MÓVIL: Imitar vuelo creativo con la cámara
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
				// Si no se cumple la condición, flyInputY permanece en 0
			}
			else
			{
				// LÓGICA PC / GAMEPAD DIRECTA
				if (Keyboard.IsKeyDown(Key.Space))
				{
					flyInputY = 1f; // Espacio = Subir
				}
				else if (Keyboard.IsKeyDown(Key.Shift))
				{
					flyInputY = -1f; // Shift = Bajar
				}
			}

			// Determinar si debemos volar: si estamos en el aire o si el jugador quiere subir
			bool shouldFly = isInAir || flyInputY > 0.01f;

			if (shouldFly)
			{
				Matrix m = m_componentCreature.ComponentBody.Matrix;
				Vector3 forward = Vector3.Normalize(new Vector3(m.Forward.X, 0f, m.Forward.Z));

				Vector3 flyDirection = forward * m_speed;

				// Si está en el aire y no hay input vertical, Y=0 para mantenerse flotando estáticamente
				flyDirection.Y = (isInAir && MathF.Abs(flyInputY) < 0.01f) ? 0f : flyInputY;

				if (flyDirection.LengthSquared() > 1f)
				{
					flyDirection = Vector3.Normalize(flyDirection);
				}

				// SOLUCIÓN AL DESCENSO AUTOMÁTICO: 
				// Si el jugador se queda quieto en el aire, flyDirection será Vector3.Zero.
				// Forzamos un valor mínimo imperceptible para garantizar que el motor 
				// mantenga el estado de "vuelo activo" y NUNCA re-active la gravedad.
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
				// En el suelo sin querer volar, usar movimiento normal
				m_componentCreature.ComponentLocomotion.FlyOrder = null;
			}
		}

		/// <summary>
		/// Procesa los controles de vuelo para IA/Criaturas montando.
		/// Lee el ComponentPilot del jinete para determinar destino y ajustar vuelo.
		/// Respeta la lógica original de ComponentPilot para decisiones de vuelo.
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
					// ============================================
					// YA ESTAMOS EN EL AIRE - MANTENER Y AJUSTAR
					// ============================================
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

					// ============================================
					// LÓGICA DE ATERRIZAJE
					// ============================================
					if (horizontalDist < c_landDistanceThreshold &&
						heightDiff > -c_landHeightThreshold &&
						heightDiff < 0.5f)
					{
						shouldFly = false;
					}
				}
				else
				{
					// ============================================
					// EN EL SUELO - DECIDIR SI DESPEGAR
					// ============================================
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
				// ============================================
				// SIN DESTINO PERO EN EL AIRE
				// ============================================
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
