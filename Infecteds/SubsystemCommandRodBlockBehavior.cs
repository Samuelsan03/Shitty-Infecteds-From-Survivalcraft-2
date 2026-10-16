using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCommandRodBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get { return new int[] { CommandRodBlock.Index }; }
		}

		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemGameInfo m_subsystemGameInfo;

		// Campo privado: distancia mínima para señalar objetivo
		private readonly float m_minPointDistance = 5f;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			// Sólo jugadores
			ComponentPlayer player = componentMiner.ComponentPlayer;
			if (player == null) return false;

			ComponentCreature selfCreature = componentMiner.ComponentCreature;
			if (selfCreature == null || selfCreature.ComponentBody == null) return false;

			Vector3 start = ray.Position;
			Vector3 direction = Vector3.Normalize(ray.Direction);
			Vector3 end = start + direction * (m_minPointDistance + 1f);
			Vector3 eyePos = selfCreature.ComponentCreatureModel.EyePosition;

			// Raycast de cuerpos (criaturas) dentro de la distancia mínima de señalado
			BodyRaycastResult? bodyResult = m_subsystemBodies.Raycast(
				start, end, 0.35f,
				(ComponentBody body, float distance) =>
					Vector3.DistanceSquared(start + distance * direction, eyePos) <= m_minPointDistance * m_minPointDistance &&
					body.Entity != componentMiner.Entity &&
					!body.IsChildOfBody(selfCreature.ComponentBody) &&
					!selfCreature.ComponentBody.IsChildOfBody(body) &&
					Vector3.Dot(Vector3.Normalize(body.BoundingBox.Center() - start), direction) > 0.7f);

			if (bodyResult == null) return false;

			ComponentBody hitBody = bodyResult.Value.ComponentBody;
			if (hitBody == null || hitBody.Entity == null) return false;

			ComponentCreature targetCreature = hitBody.Entity.FindComponent<ComponentCreature>();
			if (targetCreature == null) return false;

			// No atacar a otros jugadores
			if (targetCreature.Entity.FindComponent<ComponentPlayer>() != null) return false;

			// No atacar criaturas muertas
			if (targetCreature.ComponentHealth == null || targetCreature.ComponentHealth.Health <= 0f) return false;

			// No atacar criaturas de la manada "player" (aliados)
			ComponentNewHerdBehavior targetHerd = targetCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (targetHerd != null && targetHerd.HerdName == "player") return false;

			// ====== SONIDO DE ATAQUE (sin mensajes GUI, sólo sonido) ======
			m_subsystemAudio.PlaySound("Audio/Attack", 1f, 0f, hitBody.Position, 5f, false);

			// ====== Llamar a las criaturas de la manada "player" cercanas ======
			Vector3 playerPos = selfCreature.ComponentBody.Position;
			float callRange = 40f;
			float callRangeSq = callRange * callRange;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == null || creature.Entity == null) continue;
				if (!creature.Entity.IsAddedToProject) continue;
				if (creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f) continue;
				if (creature == selfCreature) continue;

				ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herdBehavior == null) continue;
				if (herdBehavior.HerdName != "player") continue;

				float distSq = Vector3.DistanceSquared(creature.ComponentBody.Position, playerPos);
				if (distSq > callRangeSq) continue;

				ComponentNewChaseBehavior chaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chaseBehavior != null)
				{
					// Ataque persistente con rango y tiempo amplios
					chaseBehavior.Attack(targetCreature, 30f, 60f, true);
				}
			}

			componentMiner.Poke(false);
			return true;
		}
	}
}
