using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// BuildIndex order 0: keeps the live Monster-category entities in ascending Id order (via EntityLifecycle.Spawned/Despawning)
    /// and freezes them into the shared MonsterSnapshotBuffer every step.
    public sealed class MonsterRegistry : IStepPhaseHandler
    {
        private readonly MonsterSnapshotBuffer _snapshot;
        private readonly List<SimEntity> _monsters = new List<SimEntity>();
        private readonly List<EntityHandle> _ids = new List<EntityHandle>();

        /// Subscribes to lifecycle and fills snapshot each BuildIndex; null arguments throw ArgumentNullException.
        public MonsterRegistry(EntityLifecycle lifecycle, MonsterSnapshotBuffer snapshot)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }

            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            lifecycle.Spawned += OnSpawned;
            lifecycle.Despawning += OnDespawning;
        }

        /// Snapshot buffer this registry fills.
        public MonsterSnapshotBuffer Snapshot => _snapshot;

        /// Number of live monsters.
        public int Count => _monsters.Count;

        /// Live monsters in ascending Id order.
        public IReadOnlyList<SimEntity> Monsters => _monsters;

        /// BuildIndex: clears the snapshot and adds every monster in ascending Id (position, body radius, R25 size class).
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.BuildIndex)
            {
                return;
            }

            _snapshot.Clear();
            for (int i = 0; i < _monsters.Count; i++)
            {
                SimEntity monster = _monsters[i];
                CollisionBody body = monster.Body;
                // Body.IsLarge is set by the monster itself as MonsterSizeRules.IsLarge(radius, isBoss); the radius rule is re-applied
                // so a monster that never set it is still classified by R25.
                bool isLarge = body.IsLarge || MonsterSizeRules.IsLarge(body.Radius, false);
                _snapshot.Add(new MonsterSnapshot
                {
                    Id = monster.Id,
                    Position = monster.Position,
                    Radius = body.Radius,
                    IsLarge = isLarge,
                });
            }
        }

        private void OnSpawned(SimEntity entity)
        {
            if (entity.Category != EntityCategory.Monster)
            {
                return;
            }

            int index = _ids.BinarySearch(entity.Id);
            if (index >= 0)
            {
                throw new InvalidOperationException("MonsterRegistry: monster " + entity.Id + " is already registered.");
            }

            index = ~index;
            _ids.Insert(index, entity.Id);
            _monsters.Insert(index, entity);
        }

        private void OnDespawning(SimEntity entity)
        {
            if (entity.Category != EntityCategory.Monster)
            {
                return;
            }

            int index = _ids.BinarySearch(entity.Id);
            if (index >= 0)
            {
                _ids.RemoveAt(index);
                _monsters.RemoveAt(index);
            }
        }
    }
}
