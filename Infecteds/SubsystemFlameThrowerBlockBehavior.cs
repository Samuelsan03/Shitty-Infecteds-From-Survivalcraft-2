using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFlameThrowerBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new int[] { FlameThrowerBlock.Index };

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			if (componentPlayer.ComponentGui.ModalPanelWidget == null)
			{
				componentPlayer.ComponentGui.ModalPanelWidget = new FlameThrowerWidget(inventory, slotIndex);
			}
			else
			{
				componentPlayer.ComponentGui.ModalPanelWidget = null;
			}
			return true;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlotIndex = inventory.ActiveSlotIndex;
			if (activeSlotIndex < 0) return false;

			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int slotCount = inventory.GetSlotCount(activeSlotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);

			if (contents != FlameThrowerBlock.Index || slotCount <= 0)
				return false;

			int newData = data;
			bool changed = false;

			// Inicializar tiempos de apuntado
			if (!m_aimStartTimes.ContainsKey(componentMiner))
			{
				m_aimStartTimes[componentMiner] = m_subsystemTime.GameTime;
				// Reiniciar contador de disparos para esta sesión
				m_shotsFired[componentMiner] = 0;
			}
			double aimStartTime = m_aimStartTimes[componentMiner];
			float aimDuration = (float)(m_subsystemTime.GameTime - aimStartTime);

			// Temblor al apuntar (igual que el mosquete)
			float num5 = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) * new Vector3
			{
				X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
				Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
				Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
			};
			aim.Direction = Vector3.Normalize(aim.Direction + v);

			switch (state)
			{
				case AimState.InProgress:
					// Verificar munición al inicio del apuntado
					int ammo = FlameThrowerBlock.GetAmmoCount(newData);
					if (ammo <= 0)
					{
						// Mostrar mensaje una sola vez al intentar apuntar sin munición
						if (!m_emptyMessageShown.ContainsKey(componentMiner) || !m_emptyMessageShown[componentMiner])
						{
							componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("FlameThrower is empty", Color.White, true, false);
							m_emptyMessageShown[componentMiner] = true;
						}
						// No activar el switch ni disparar
						break;
					}

					// Activar el switch si no está activado y ha pasado suficiente tiempo
					if (aimDuration > 0.5f && !FlameThrowerBlock.GetSwitchState(newData))
					{
						newData = FlameThrowerBlock.SetSwitchState(newData, true);
						m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
						changed = true;
					}

					// Disparo continuo mientras el switch está activado
					if (FlameThrowerBlock.GetSwitchState(newData))
					{
						if (!m_lastFireTimes.ContainsKey(componentMiner))
						{
							m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
						}
						double lastFireTime = m_lastFireTimes[componentMiner];
						float fireInterval = 0.2f; // 5 disparos/segundo
						if (m_subsystemTime.GameTime - lastFireTime >= fireInterval)
						{
							// Disparar sin consumir munición
							if (TryFire(componentMiner, aim))
							{
								m_lastFireTimes[componentMiner] = m_subsystemTime.GameTime;
								// Incrementar contador de disparos de esta sesión
								m_shotsFired[componentMiner] = m_shotsFired[componentMiner] + 1;
							}
						}
					}

					// Actualizar postura y vista (igual que el mosquete)
					ComponentFirstPersonModel firstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
					if (firstPersonModel != null)
					{
						ComponentPlayer player = componentMiner.ComponentPlayer;
						if (player != null)
							player.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);

						firstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
						firstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
					}

					componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;

				case AimState.Cancelled:
				case AimState.Completed:
					// Al soltar, desactivar el switch y consumir munición si se disparó al menos una vez
					int shots = m_shotsFired.ContainsKey(componentMiner) ? m_shotsFired[componentMiner] : 0;
					int currentAmmo = FlameThrowerBlock.GetAmmoCount(newData);
					if (FlameThrowerBlock.GetSwitchState(newData))
					{
						// Sonido de release (como el mosquete)
						if (state == AimState.Completed)
						{
							m_subsystemAudio.PlaySound("Audio/HammerRelease", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
						}
						else // Cancelled
						{
							m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
						}

						// Consumir una bala si se disparó y hay munición
						if (shots > 0 && currentAmmo > 0)
						{
							currentAmmo--;
							newData = FlameThrowerBlock.SetAmmoCount(newData, currentAmmo);
							if (currentAmmo == 0)
							{
								newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Empty);
							}
							changed = true;
						}

						// Desactivar switch
						newData = FlameThrowerBlock.SetSwitchState(newData, false);
						changed = true;
					}

					// Limpiar datos de la sesión
					m_aimStartTimes.Remove(componentMiner);
					m_lastFireTimes.Remove(componentMiner);
					m_shotsFired.Remove(componentMiner);
					m_emptyMessageShown.Remove(componentMiner);
					break;
			}

			// Actualizar el inventario si hubo cambios
			if (changed && newData != data)
			{
				int newValue = Terrain.MakeBlockValue(contents, 0, newData);
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}

			return false;
		}

		private bool TryFire(ComponentMiner componentMiner, Ray3 aim)
		{
			// Disparar sin consumir munición (solo crear el proyectil)
			Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 fireDir = Vector3.Normalize(aim.Direction);
			Vector3 right = Vector3.Normalize(Vector3.Cross(fireDir, Vector3.UnitY));
			Vector3 up = Vector3.Normalize(Vector3.Cross(fireDir, right));

			int bulletValue = Terrain.MakeBlockValue(m_flameBulletBlockIndex, 0, 0);
			float speed = 120f;
			Vector3 spread = new Vector3(0.06f, 0.06f, 0f);

			Vector3 randomOffset = m_random.Float(-spread.X, spread.X) * right
								 + m_random.Float(-spread.Y, spread.Y) * up
								 + m_random.Float(-spread.Z, spread.Z) * fireDir;
			Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + speed * (fireDir + randomOffset);

			Projectile projectile = m_subsystemProjectiles.FireProjectile(
				bulletValue,
				eyePos,
				velocity,
				Vector3.Zero,
				componentMiner.ComponentCreature);

			if (projectile != null)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				projectile.AttackPower = 0f;
			}

			m_subsystemAudio.PlaySound("Audio/Fire", 1f, m_random.Float(-0.1f, 0.1f), eyePos, 10f, true);
			m_subsystemNoise.MakeNoise(eyePos, 1f, 40f);

			// Dañar la herramienta (1 de durabilidad por disparo, pero no consume munición)
			componentMiner.DamageActiveTool(1);

			return true;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotValue = inventory.GetSlotValue(slotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			if (contents != FlameThrowerBlock.Index)
				return 0;

			int data = Terrain.ExtractData(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);

			if (loadState == FlameThrowerBlock.LoadState.Empty && Terrain.ExtractContents(value) == m_flameBulletBlockIndex)
				return 1;

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			if (processCount == 1)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int data = Terrain.ExtractData(slotValue);
				FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);

				if (loadState == FlameThrowerBlock.LoadState.Empty && Terrain.ExtractContents(value) == m_flameBulletBlockIndex)
				{
					int newData = FlameThrowerBlock.SetLoadState(data, FlameThrowerBlock.LoadState.Loaded);
					newData = FlameThrowerBlock.SetAmmoCount(newData, 15);

					inventory.RemoveSlotItems(slotIndex, 1);
					inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, newData), 1);

					processedValue = 0;
					processedCount = 0;
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_flameBulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
			base.Load(valuesDictionary);
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();

		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, double> m_lastFireTimes = new Dictionary<ComponentMiner, double>();
		private Dictionary<ComponentMiner, int> m_shotsFired = new Dictionary<ComponentMiner, int>();
		private Dictionary<ComponentMiner, bool> m_emptyMessageShown = new Dictionary<ComponentMiner, bool>();
		private int m_flameBulletBlockIndex;
	}
}