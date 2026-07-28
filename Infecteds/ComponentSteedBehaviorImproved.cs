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
			// ============================================
			ComponentPlayer player = rider.Entity.FindComponent<ComponentPlayer>();

			if (player != null && player.ComponentInput != null)
			{
				// LÓGICA PARA JUGADORES (PC y Móvil)
				ProcessPlayerFlightControls(player, isInAir);
			}
			else if (rider is ComponentRiderZombie)
			{
				// LÓGICA PARA JINETES ZOMBI
				// Los zombis montados usan lógica de IA adaptada
				ProcessZombieFlightControls((ComponentRiderZombie)rider, isInAir);
			}
			else
			{
				// LÓGICA PARA IA/CRIOPTURAS MONTANDO
				ProcessAIFlightControls(rider, isInAir);
			}
		}

		/// <summary>
		/// Valida que un jinete zombi pueda estar montado en la montura actual.
		/// Si la montura es un ComponentMountZombie con CanRiderBeMounted = false,
		/// fuerza el desmonte inmediato del zombi usando la API original del juego.
		/// </summary>
		/// <returns>True si el estado es válido, false si se forzó el desmonte.</returns>
		private bool ValidateZombieRiderMount()
		{
			if (m_componentMount?.Rider == null) return true;

			// Verificar si el jinete es un zombi
			ComponentRiderZombie riderZombie = m_componentMount.Rider as ComponentRiderZombie;
			if (riderZombie == null) return true;

			// Verificar si la montura tiene restricciones para zombis
			ComponentMountZombie mountZombie = m_componentMount as ComponentMountZombie;
			if (mountZombie == null) return true;

			// Si la montura no permite jinetes zombi, forzar desmonte inmediato
			if (!mountZombie.CanRiderBeMounted)
			{
				ComponentBody riderBody = riderZombie.ComponentCreature.ComponentBody;

				// Forzar desmonte inmediato sin animación (tal como lo hace el código base en Update)
				if (riderBody.ParentBody != null)
				{
					riderBody.Velocity = riderBody.ParentBody.Velocity;
					riderBody.ParentBody = null;
				}

				// Reseteamos los estados de animación internos del jinete por seguridad
				riderZombie.m_isAnimating = false;
				riderZombie.m_isDismounting = false;

				return false;
			}

			return true;
		}

		/// <summary>
		/// Procesa los controles de vuelo para jinetes zombi.
		/// Los zombis no tienen ComponentPilot, por lo que usan una lógica
		/// simplificada basada en el comportamiento de la criatura montada.
		/// </summary>
		private void ProcessZombieFlightControls(ComponentRiderZombie riderZombie, bool isInAir)
		{
			if (isInAir)
			{
				// ============================================
				// ZOMBI EN EL AIRE
				// Los zombis no tienen control de vuelo sofisticado.
				// Descienden suavemente hacia el suelo, igual que
				// el comportamiento original de ComponentPilot sin destino.
				// ============================================
				m_componentCreature.ComponentLocomotion.FlyOrder = new Vector3(0f, c_gentleDescentSpeed, 0f);
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
						// Destino significativamente más arriba - subir
						// Usamos (heightDiff - 1f) para empezar a subir antes de llegar al destino
						verticalInput = MathUtils.Min((heightDiff - 1f) * 0.3f, 1f);
					}
					else if (heightDiff < -2f)
					{
						// Destino significativamente más abajo - bajar
						// Limitamos la velocidad de bajada para seguridad
						verticalInput = MathUtils.Max((heightDiff + 1f) * 0.3f, -0.8f);
					}
					else
					{
						// Cerca de la altura objetivo - ajuste fino
						verticalInput = MathUtils.Clamp(heightDiff * 0.2f, -0.3f, 0.3f);
					}

					// ============================================
					// LÓGICA DE ATERRIZAJE
					// Si estamos cerca del destino horizontalmente y a la altura correcta,
					// dejar de volar para que la gravedad nos baje al suelo suavemente
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
					// Lógica similar a ComponentPilot: volar si el destino está lejos o arriba
					// ============================================

					// Condición 1: Destino muy arriba
					bool destinationHigh = heightDiff > c_flyHeightThreshold;

					// Condición 2: Destino lejos con algo de altura (como ComponentPilot: num > 9f)
					bool destinationFarAndElevated = horizontalDist > c_flyFarDistanceThreshold && heightDiff > 1f;

					// Condición 3: Destino lejano y hay obstáculos (simulado por distancia grande)
					bool destinationVeryFar = horizontalDist > 9f;

					if (destinationHigh || destinationFarAndElevated || destinationVeryFar)
					{
						shouldFly = true;
						verticalInput = 1f; // Subir al despegar
					}
				}
			}
			else if (isInAir)
			{
				// ============================================
				// SIN DESTINO PERO EN EL AIRE
				// Descender suavemente, igual que ComponentPilot original:
				// m_flyOrder = new Vector3?(new Vector3(0f, -0.5f, 0f));
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

				// Normalizar si excede magnitud 1
				if (flyDirection.LengthSquared() > 1f)
				{
					flyDirection = Vector3.Normalize(flyDirection);
				}

				// SOLUCIÓN AL DESCENSO AUTOMÁTICO:
				// Asegurar que siempre haya algún input para mantener el estado de "vuelo activo"
				// y NUNCA re-active la gravedad inesperadamente
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
				// No volar - usar movimiento normal terrestre
				// WalkOrder y TurnOrder ya fueron establecidos por base.ProcessRidingOrders()
				m_componentCreature.ComponentLocomotion.FlyOrder = null;
			}
		}
	}
}
