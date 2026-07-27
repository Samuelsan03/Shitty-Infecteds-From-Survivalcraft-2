using System;
using Engine;
using Engine.Input; // ¡Necesario para usar Keyboard.IsKeyDown!
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente de comportamiento de montura mejorado.
	/// Soluciona conflictos de gravedad y saltos bruscos en criaturas voladoras.
	/// PC: Espacio directo para subir, Shift directo para bajar.
	/// Movil: Sube y baja segun la inclinacion de la camara.
	/// </summary>
	public class ComponentSteedBehaviorImproved : ComponentSteedBehavior
	{
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_isEnabled = true;
		}

		public override void ProcessRidingOrders()
		{
			bool canFly = m_componentCreature.ComponentLocomotion.FlySpeed > 0f;
			bool isInAir = m_componentCreature.ComponentBody.StandingOnValue == null;

			// Llamamos a la base para calcular la velocidad (m_speed) y el giro (m_turnSpeed)
			base.ProcessRidingOrders();

			// Si la criatura no puede volar, salimos (se comportará como un caballo normal)
			if (!canFly) return;

			float flyInputY = 0f;
			ComponentRider rider = m_componentMount.Rider;

			if (rider != null)
			{
				ComponentPlayer player = rider.Entity.FindComponent<ComponentPlayer>();
				if (player != null && player.ComponentInput != null)
				{
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

							// Bloquear el salto normal terrestre en móvil
							m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
							return;
						}
						else
						{
							flyInputY = 0f;
						}
					}
					else
					{
						// LÓGICA PC / GAMEPAD DIRECTA
						// Usamos Engine.Input.Keyboard para evitar que el motor bloquee las teclas
						if (Keyboard.IsKeyDown(Key.Space))
						{
							flyInputY = 1f; // Espacio = Subir
						}
						else if (Keyboard.IsKeyDown(Key.Shift))
						{
							flyInputY = -1f; // Shift = Bajar
						}
					}
				}
			}

			// Lógica común para PC/Gamepad, o móvil cuando no avanza
			if (isInAir || MathF.Abs(flyInputY) > 0.01f)
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

				// Aplicamos el FlyOrder (esto desactiva la gravedad dentro de ComponentLocomotion)
				m_componentCreature.ComponentLocomotion.FlyOrder = flyDirection;

				// Anulamos el WalkOrder mientras volamos
				m_componentCreature.ComponentLocomotion.WalkOrder = null;

				// ¡SOLUCIÓN CLAVE!: Forzar JumpOrder a 0. 
				// Esto evita que el motor haga el "impulso de salto brusco" del suelo y cancele el vuelo fluido.
				m_componentCreature.ComponentLocomotion.JumpOrder = 0f;
			}
			else
			{
				// En el suelo y sin intención de volar: restauramos null para usar la gravedad normal
				m_componentCreature.ComponentLocomotion.FlyOrder = null;
			}
		}
	}
}
