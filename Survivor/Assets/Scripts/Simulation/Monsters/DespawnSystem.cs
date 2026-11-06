using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Lifecycle order 20: despawns non-boss monsters strictly beyond the despawn distance D (R18) with OutOfRange, in ascending
    /// Id order. Not a kill: no drops, no kill count (R24). Gems and chests have no despawn rule. Calls EntityLifecycle directly.
    public sealed class DespawnSystem : IStepPhaseHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly MonsterRegistry _registry;
        private readonly GameDataRegistry _data;
        private readonly List<EntityHandle> _targets = new List<EntityHandle>();

        /// Creates the system; null arguments throw ArgumentNullException.
        public DespawnSystem(EntityLifecycle lifecycle, MonsterRegistry registry, GameDataRegistry data)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// D in use: the dev override when Rules.DespawnDistanceOverridden, else the current spawn table's DespawnDistance
        /// (read on every call so a hot reload applies from the next step). Returns false when neither is available.
        public bool TryGetDespawnDistance(SimulationRules rules, out float distance)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (rules.DespawnDistanceOverridden)
            {
                distance = rules.DespawnDistance;
                return true;
            }

            GameData data = _data.Current;
            if (data == null)
            {
                distance = 0f;
                return false;
            }

            distance = data.SpawnTable.DespawnDistance;
            return true;
        }

        /// Lifecycle: collects the ids first (the registry shrinks during Despawn), then despawns them in ascending Id order.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Lifecycle || !TryGetDespawnDistance(context.Rules, out float distance))
            {
                return;
            }

            IReadOnlyList<SimEntity> monsters = _registry.Monsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                SimEntity entity = monsters[i];
                if (entity is Monster monster && monster.IsBoss)
                {
                    continue;
                }

                if (SpawnGeometry.IsBeyondDespawn(entity.Position, context.PlayerPosition, distance))
                {
                    _targets.Add(entity.Id);
                }
            }

            try
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    _lifecycle.Despawn(_targets[i], DespawnReason.OutOfRange);
                }
            }
            finally
            {
                _targets.Clear();
            }
        }
    }
}
