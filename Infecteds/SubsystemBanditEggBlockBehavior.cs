using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBanditEggBlockBehavior : SubsystemBlockBehavior
	{
		// ╔══════════════════════════════════════════════════════════╗
		// ║  LISTA DE CRIATURAS QUE PUDE SPAWNEAR EL HUEVO DE BANDIDO ║
		// ║  Para agregar más criaturas, simplemente añade entradas    ║
		// ║  al array de abajo. Ejemplo:                              ║
		// ║                                                            ║
		// ║    "Bandit1",                                              ║
		// ║    "Bandit2",                                              ║
		// ║    "Bandit3",   // ← nueva criatura                        ║
		// ║    "BanditBoss" // ← otra más                              ║
		// ║                                                            ║
		// ╚══════════════════════════════════════════════════════════╝
		public static readonly string[] m_creatureTemplates = new string[]
		{
			"Bandit1",
			"Bandit2",
			"Bandit3"
            // ─────── Espacio para agregar más criaturas aquí ───────
            // "Bandit4",
            // "Bandit5",
        };

		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();
			}
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// Verificar que el proyectil sea del huevo de bandido
			int blockType = Terrain.ExtractContents(worldItem.Value);
			if (blockType != BanditEggBlock.Index)
			{
				return true;
			}

			int num = Terrain.ExtractData(worldItem.Value);
			bool isCooked = BanditEggBlock.GetIsCooked(num);
			bool isLaid = BanditEggBlock.GetIsLaid(num);

			if (!isCooked && (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative
				|| this.m_random.Float(0f, 1f) <= (isLaid ? 0.15f : 1f)))
			{
				try
				{
					// Seleccionar criatura aleatoria de la lista
					int index = this.m_random.Int(0, m_creatureTemplates.Length - 1);
					string templateName = m_creatureTemplates[index];

					Entity entity = DatabaseManager.CreateEntity(base.Project, templateName, true);
					entity.FindComponent<ComponentBody>(true).Position = worldItem.Position;
					entity.FindComponent<ComponentBody>(true).Rotation = Quaternion.CreateFromAxisAngle(
						Vector3.UnitY,
						this.m_random.Float(0f, 6.2831855f)
					);
					entity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0.25f;
					base.Project.AddEntity(entity);
				}
				catch (Exception value)
				{
					Log.Error(string.Format(
						"Spawning bandit from egg (index: {0}) error: {1}",
						num >> 4 & 4095,
						value
					));

					// Mostrar mensaje en el GUI del jugador que lanzó el huevo
					Projectile projectile = worldItem as Projectile;
					if (projectile != null)
					{
						ComponentCreature owner = projectile.Owner;
						ComponentGui componentGui = (owner != null) ? owner.Entity.FindComponent<ComponentGui>() : null;
						if (componentGui != null)
						{
							componentGui.DisplaySmallMessage(
								"Failed to spawn bandit, check game log",
								Color.White,
								true,
								false
							);
						}
					}
				}
			}
			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_eggBlock = (BanditEggBlock)BlocksManager.Blocks[BanditEggBlock.Index];
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public BanditEggBlock m_eggBlock;
		public Random m_random = new Random();
		public const string fName = "SubsystemBanditEggBlockBehavior";
	}
}
