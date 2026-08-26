using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class Teleport : Component, IUpdateable
    {
        public UpdateOrder UpdateOrder
        {
            get
            {
                return UpdateOrder.Default;
            }
        }

        private float m_teleportationRange;
        private float m_probabilityOfTeleporting;
        private float m_timeToTeleportAgain;
        private float m_timeMissingBeforeReappearing;

        private TeleportState m_currentState;
        private float m_teleportCooldownTimer;
        private float m_missingTimer;
        private Vector3 m_realTargetPosition;
        private ComponentCreature m_teleportTarget;
        private Random m_random;

        private SubsystemTime m_subsystemTime;
        private SubsystemAudio m_subsystemAudio;
        private SubsystemParticles m_subsystemParticles;
        private SubsystemTerrain m_subsystemTerrain;
        private ComponentCreature m_componentCreature;
        private ComponentBody m_componentBody;
        private ComponentNewChaseBehavior m_componentNewChaseBehavior;
        private ComponentRider m_componentRider;

        // NUEVO: Número máximo de intentos para encontrar posición segura
        private const int MaxTeleportAttempts = 20;
        // NUEVO: Margen de seguridad adicional para evitar appearing en bordes
        private const float SafeMargin = 0.15f;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);

            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentBody = m_componentCreature.ComponentBody;
            m_componentNewChaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>(true);
            m_componentRider = Entity.FindComponent<ComponentRider>(false);

            m_teleportationRange = valuesDictionary.GetValue<float>("TeleportationRange");
            m_probabilityOfTeleporting = valuesDictionary.GetValue<float>("ProbabilityOfTeleporting");
            m_timeToTeleportAgain = valuesDictionary.GetValue<float>("TimeToTeleportAgain");
            m_timeMissingBeforeReappearing = valuesDictionary.GetValue<float>("TimeMissingBeforeReappearing");

            m_random = new Random();
            m_currentState = TeleportState.Idle;
            m_teleportCooldownTimer = 0f;
        }

        public void Update(float dt)
        {
            if (m_teleportCooldownTimer > 0f)
            {
                m_teleportCooldownTimer -= dt;
            }

            switch (m_currentState)
            {
                case TeleportState.Idle:
                    HandleIdleState();
                    break;
                case TeleportState.Disappearing:
                    HandleDisappearingState(dt);
                    break;
                case TeleportState.Appearing:
                    HandleAppearingState();
                    break;
            }
        }

        private void HandleIdleState()
        {
            if (m_componentCreature.ComponentHealth == null || m_componentCreature.ComponentHealth.Health <= 0f)
                return;

            if (m_teleportCooldownTimer > 0f)
                return;

            if (m_componentRider != null && m_componentRider.Mount != null)
                return;

            ComponentCreature chaseTarget = m_componentNewChaseBehavior.Target;

            if (chaseTarget == null)
                return;

            if (chaseTarget.ComponentHealth == null || chaseTarget.ComponentHealth.Health <= 0f)
                return;

            float distanceToTarget = Vector3.Distance(m_componentBody.Position, chaseTarget.ComponentBody.Position);

            if (distanceToTarget <= m_teleportationRange)
                return;

            if (m_random.Float(0f, 1f) < m_probabilityOfTeleporting)
            {
                m_teleportTarget = chaseTarget;
                StartDisappearing(chaseTarget.ComponentBody.Position);
            }
        }

        private void StartDisappearing(Vector3 targetPosition)
        {
            m_currentState = TeleportState.Disappearing;

            Vector3 particlePosition = m_componentBody.Position + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
            float size = m_componentBody.BoxSize.X;

            m_subsystemParticles.AddParticleSystem(new TeleportParticleSystem(m_subsystemTerrain, particlePosition, size), false);
            m_subsystemAudio.PlaySound("Audio/teleport 1", 1f, 0f, particlePosition, 4f, true);

            // NUEVO: Calcular posición segura para aparecer
            m_realTargetPosition = FindSafeTeleportPosition(targetPosition);

            // Esconder en el cielo mientras espera
            Vector3 hiddenPosition = m_realTargetPosition;
            hiddenPosition.Y += 500f;

            m_componentBody.IsGravityEnabled = false;
            m_componentBody.TerrainCollidable = false;
            m_componentBody.BodyCollidable = false;

            m_componentBody.Position = hiddenPosition;
            m_componentBody.Velocity = Vector3.Zero;

            m_missingTimer = m_timeMissingBeforeReappearing;
        }

        /// <summary>
        /// NUEVO: Encuentra una posición segura para teletransportarse
        /// Verifica que no haya bloques sólidos, que haya espacio suficiente
        /// y que la criatura pueda respirar (aire o agua según su tipo)
        /// </summary>
        private Vector3 FindSafeTeleportPosition(Vector3 targetPosition)
        {
            float creatureHeight = m_componentBody.BoxSize.Y;
            float creatureWidth = m_componentBody.BoxSize.X;
            float creatureDepth = m_componentBody.BoxSize.Z;
            
            // Determinar tipo de respiración
            bool breathesAir = true;
            bool breathesWater = false;
            if (m_componentCreature.ComponentHealth != null)
            {
                breathesAir = m_componentCreature.ComponentHealth.BreathingMode == BreathingMode.Air;
                breathesWater = m_componentCreature.ComponentHealth.BreathingMode == BreathingMode.Water;
            }

            // Intentar encontrar una posición segura
            for (int attempt = 0; attempt < MaxTeleportAttempts; attempt++)
            {
                // Generar dirección y distancia aleatoria
                Vector2 randomDirection = m_random.Vector2();
                float randomDistance = m_random.Float(2f, 5f);

                Vector3 candidateBase = new Vector3(
                    targetPosition.X + randomDirection.X * randomDistance,
                    targetPosition.Y,
                    targetPosition.Z + randomDirection.Y * randomDistance
                );

                int cellX = Terrain.ToCell(candidateBase.X);
                int cellZ = Terrain.ToCell(candidateBase.Z);

                // Encontrar la altura del terreno
                int topBlockY = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(cellX, cellZ);

                // Probar diferentes alturas sobre el suelo
                for (int heightOffset = 0; heightOffset <= 4; heightOffset++)
                {
                    Vector3 candidatePos = new Vector3(
                        candidateBase.X,
                        topBlockY + 1f + heightOffset,
                        candidateBase.Z
                    );

                    if (IsPositionSafeForTeleport(candidatePos, creatureWidth, creatureDepth, creatureHeight, breathesAir, breathesWater))
                    {
                        return candidatePos;
                    }
                }

                // Para criaturas acuáticas, también buscar bajo el agua
                if (breathesWater)
                {
                    Vector3 waterPos = FindSafeWaterPosition(targetPosition, creatureWidth, creatureDepth, creatureHeight);
                    if (waterPos.Y > 0)
                    {
                        return waterPos;
                    }
                }
            }

            // Fallback: aparecer sobre el terreno en la posición del target
            int fallbackX = Terrain.ToCell(targetPosition.X);
            int fallbackZ = Terrain.ToCell(targetPosition.Z);
            int fallbackY = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(fallbackX, fallbackZ);
            return new Vector3(targetPosition.X, fallbackY + 3f, targetPosition.Z);
        }

        /// <summary>
        /// NUEVO: Verifica si una posición es segura para teletransportarse
        /// </summary>
        private bool IsPositionSafeForTeleport(Vector3 position, float width, float depth, float height, bool needsAir, bool needsWater)
        {
            int centerX = Terrain.ToCell(position.X);
            int centerZ = Terrain.ToCell(position.Z);
            int bottomY = Terrain.ToCell(position.Y);
            int topY = Terrain.ToCell(position.Y + height);

            // Verificar límites del mundo
            if (bottomY < 1 || topY >= 255)
                return false;

            // Verificar que hay suelo sólido debajo
            int groundCellContents = m_subsystemTerrain.Terrain.GetCellContents(centerX, bottomY - 1, centerZ);
            Block groundBlock = BlocksManager.Blocks[groundCellContents];
            if (!groundBlock.IsCollidable_(0))
                return false;

            // Calcular rango de celdas a verificar (basado en el tamaño de la criatura)
            int halfWidthX = MathUtils.Max(0, (int)Math.Ceiling(width / 2f) - 1);
            int halfWidthZ = MathUtils.Max(0, (int)Math.Ceiling(depth / 2f) - 1);

            bool hasClearSpace = true;
            bool headIsInCorrectMedium = true;

            for (int dx = -halfWidthX; dx <= halfWidthX && hasClearSpace; dx++)
            {
                for (int dz = -halfWidthZ; dz <= halfWidthZ && hasClearSpace; dz++)
                {
                    int checkX = centerX + dx;
                    int checkZ = centerZ + dz;

                    for (int y = bottomY; y <= topY; y++)
                    {
                        int cellContents = m_subsystemTerrain.Terrain.GetCellContents(checkX, y, checkZ);
                        Block block = BlocksManager.Blocks[cellContents];

                        // Verificar que no haya bloques sólidos
                        if (block.IsCollidable_(0))
                        {
                            hasClearSpace = false;
                            break;
                        }
                    }
                }
            }

            if (!hasClearSpace)
                return false;

            // Verificar que la cabeza esté en el medio correcto (aire o agua)
            int headY = Terrain.ToCell(position.Y + height * 0.75f);
            if (headY >= 0 && headY < 256)
            {
                int headCellContents = m_subsystemTerrain.Terrain.GetCellContents(centerX, headY, centerZ);
                Block headBlock = BlocksManager.Blocks[headCellContents];
                bool headInFluid = headBlock is FluidBlock;
                bool headInWater = headBlock is WaterBlock;

                if (needsAir && headInFluid)
                {
                    headIsInCorrectMedium = false;
                }
                if (needsWater && !headInWater)
                {
                    headIsInCorrectMedium = false;
                }
            }

            // Verificar que no haya techo muy bajo (espacio para la cabeza)
            int aboveHeadY = topY + 1;
            if (aboveHeadY < 256)
            {
                int aboveCellContents = m_subsystemTerrain.Terrain.GetCellContents(centerX, aboveHeadY, centerZ);
                Block aboveBlock = BlocksManager.Blocks[aboveCellContents];
                // Si hay un bloque sólido justo encima, el espacio es muy cerrado
                if (aboveBlock.IsCollidable_(0) && needsAir)
                {
                    return false;
                }
            }

            return headIsInCorrectMedium;
        }

        /// <summary>
        /// NUEVO: Busca una posición segura bajo el agua para criaturas acuáticas
        /// </summary>
        private Vector3 FindSafeWaterPosition(Vector3 targetPosition, float width, float depth, float height)
        {
            int cellX = Terrain.ToCell(targetPosition.X);
            int cellZ = Terrain.ToCell(targetPosition.Z);

            // Buscar agua desde la superficie hacia abajo
            int surfaceY = m_subsystemTerrain.Terrain.CalculateTopmostCellHeight(cellX, cellZ);

            for (int y = surfaceY; y >= 0; y--)
            {
                int cellContents = m_subsystemTerrain.Terrain.GetCellContents(cellX, y, cellZ);
                Block block = BlocksManager.Blocks[cellContents];

                if (block is WaterBlock)
                {
                    // Encontramos agua, verificar si es un espacio seguro
                    Vector3 waterPos = new Vector3(targetPosition.X, y + 0.5f, targetPosition.Z);
                    
                    // Para criaturas acuáticas, verificar solo que no haya bloques sólidos
                    int bottomY = Terrain.ToCell(waterPos.Y);
                    int topY = Terrain.ToCell(waterPos.Y + height);
                    
                    bool hasClearSpace = true;
                    int halfWidth = MathUtils.Max(0, (int)Math.Ceiling(width / 2f) - 1);
                    
                    for (int dx = -halfWidth; dx <= halfWidth && hasClearSpace; dx++)
                    {
                        for (int dz = -halfWidth; dz <= halfWidth && hasClearSpace; dz++)
                        {
                            for (int checkY = bottomY; checkY <= topY; checkY++)
                            {
                                int checkContents = m_subsystemTerrain.Terrain.GetCellContents(cellX + dx, checkY, cellZ + dz);
                                if (BlocksManager.Blocks[checkContents].IsCollidable_(0))
                                {
                                    hasClearSpace = false;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (hasClearSpace)
                    {
                        return waterPos;
                    }
                }
            }

            return Vector3.Zero; // No se encontró posición segura
        }

        private void HandleDisappearingState(float dt)
        {
            // Prevención de bugs: si se monta mientras desaparece, cancelar
            if (m_componentRider != null && m_componentRider.Mount != null)
            {
                m_componentBody.Position = m_realTargetPosition;
                m_componentBody.Velocity = Vector3.Zero;
                m_componentBody.IsGravityEnabled = true;
                m_componentBody.TerrainCollidable = true;
                m_componentBody.BodyCollidable = true;
                m_currentState = TeleportState.Idle;
                return;
            }

            m_missingTimer -= dt;
            if (m_missingTimer <= 0f)
            {
                m_currentState = TeleportState.Appearing;
            }
        }

        private void HandleAppearingState()
        {
            m_componentBody.Position = m_realTargetPosition;
            m_componentBody.Velocity = Vector3.Zero;

            m_componentBody.IsGravityEnabled = true;
            m_componentBody.TerrainCollidable = true;
            m_componentBody.BodyCollidable = true;

            Vector3 appearParticlePosition = m_realTargetPosition + new Vector3(0f, m_componentBody.StanceBoxSize.Y / 2f, 0f);
            float size = m_componentBody.BoxSize.X;

            m_subsystemParticles.AddParticleSystem(new TeleportParticleSystem(m_subsystemTerrain, appearParticlePosition, size), false);
            m_subsystemAudio.PlaySound("Audio/teleport 2", 1f, 0f, appearParticlePosition, 4f, true);

            // Re-enganchar el chase
            if (m_teleportTarget != null && m_teleportTarget.ComponentHealth != null && m_teleportTarget.ComponentHealth.Health > 0f)
            {
                m_componentNewChaseBehavior.Attack(m_teleportTarget, m_teleportationRange * 2f, 10f, false);
            }

            m_currentState = TeleportState.Idle;
            m_teleportCooldownTimer = m_timeToTeleportAgain;
            m_teleportTarget = null;
        }

        public enum TeleportState
        {
            Idle,
            Disappearing,
            Appearing
        }
    }
}
