using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Single owner of acquire/id/init/register and the exact reverse on despawn (5.10, R11). Lifecycle phase or run reset only.
    public sealed class EntityLifecycle
    {
        private static readonly Comparison<SimEntity> ById = (a, b) => a.Id.CompareTo(b.Id);

        private readonly EntityHandleAllocator _ids;
        private readonly IPhysicsBodyRegistry _physicsBodies;
        private readonly PrefabCatalog _prefabs;
        private readonly SimulationClock _clock;
        private readonly List<SimEntity> _active = new List<SimEntity>();
        private readonly List<SimEntity> _byIndex = new List<SimEntity> { null };
        private readonly List<SimEntity> _scratch = new List<SimEntity>();
        private IPoolStrategy _pool;
        private ITickStrategy _ticks;
        private ICollisionStrategy _collisions;
        private bool _runResetInProgress;

        /// Creates the lifecycle over the run's strategies; every argument is required (ArgumentNullException).
        public EntityLifecycle(IPoolStrategy pool, EntityHandleAllocator ids, ITickStrategy ticks,
                               ICollisionStrategy collisions, IPhysicsBodyRegistry physicsBodies,
                               PrefabCatalog prefabs, SimulationClock clock)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
            _collisions = collisions ?? throw new ArgumentNullException(nameof(collisions));
            _physicsBodies = physicsBodies ?? throw new ArgumentNullException(nameof(physicsBodies));
            _prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// Raised last in Spawn (registries such as MonsterRegistry / PickupRegistry add the entity here).
        public event Action<SimEntity> Spawned;

        /// Raised first in Despawn, the mirror of Spawned (registries remove the entity here).
        public event Action<SimEntity> Despawning;

        /// Raised after every strategy unregistered the entity and before OnReleased / id free, so Id is still readable (TAC-0-10).
        public event Action<SimEntity, DespawnReason> Despawned;

        /// Pool strategy of the current run.
        public IPoolStrategy Pool => _pool;

        /// Tick strategy of the current run (drivers read it every step).
        public ITickStrategy Ticks => _ticks;

        /// Collision strategy of the current run.
        public ICollisionStrategy Collisions => _collisions;

        /// Number of live entities.
        public int ActiveCount => _active.Count;

        /// Live entities in no particular order (presentation only; simulation code iterates in ascending Id).
        public IReadOnlyList<SimEntity> ActiveEntities => _active;

        /// Spawns from the category template; see the other overload.
        public T Spawn<T>(EntityCategory category, Vector2 position, long lifetimeSteps, Action<T> initialize) where T : SimEntity
        {
            return Spawn(category, null, position, lifetimeSteps, initialize);
        }

        /// Spawns an entity at position from the category template (or the decorated clone of prefabOverride). lifetimeSteps == 0 means
        /// no lifetime (ExpireStep = long.MaxValue), > 0 gives ExpireStep = SpawnStep + lifetimeSteps, < 0 throws ArgumentOutOfRangeException.
        /// Order: pool.Acquire -> ids.Allocate -> Bind -> initialize -> ticks.Register (IUpdatable) -> physicsBodies.Register
        /// -> collisions.Register -> Spawned. Throws InvalidOperationException outside the Lifecycle phase / run reset (TAC-0-8).
        public T Spawn<T>(EntityCategory category, GameObject prefabOverride, Vector2 position, long lifetimeSteps, Action<T> initialize)
            where T : SimEntity
        {
            ThrowUnlessMutationAllowed(nameof(Spawn));
            if (lifetimeSteps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSteps), lifetimeSteps, "Lifetime must be 0 (none) or positive.");
            }

            GameObject template = _prefabs.GetTemplate(category, prefabOverride);
            if (!(template.GetComponent<SimEntity>() is T))
            {
                throw new InvalidOperationException("EntityLifecycle.Spawn: the " + category + " template does not carry " + typeof(T).Name + ".");
            }

            GameObject instance = _pool.Acquire(template, position);
            T entity = instance.GetComponent<T>();
            EntityHandle id = _ids.Allocate();
            long spawnStep = _clock.Step;
            long expireStep = lifetimeSteps == 0 ? long.MaxValue : spawnStep + lifetimeSteps;
            entity.Bind(id, spawnStep, expireStep);
            entity.PlaceAt(position);
            initialize?.Invoke(entity);
            if (entity is IUpdatable updatable)
            {
                _ticks.Register(updatable, instance);
            }

            _physicsBodies.Register(entity.Body);
            _collisions.Register(entity.Body);
            AddActive(entity);
            Spawned?.Invoke(entity);
            return entity;
        }

        /// Despawns id in the exact reverse of Spawn: Despawning -> collisions.Unregister -> physicsBodies.Unregister
        /// -> ticks.Unregister -> Despawned -> OnReleased -> ids.Free -> pool.Release. Returns false when id is not live (no exception).
        /// Throws InvalidOperationException outside the Lifecycle phase / run reset.
        public bool Despawn(EntityHandle id, DespawnReason reason)
        {
            ThrowUnlessMutationAllowed(nameof(Despawn));
            if (!_ids.IsLive(id) || !TryGet(id, out SimEntity entity))
            {
                return false;
            }

            Despawning?.Invoke(entity);
            _collisions.Unregister(entity.Body);
            _physicsBodies.Unregister(entity.Body);
            if (entity is IUpdatable updatable)
            {
                _ticks.Unregister(updatable);
            }

            Despawned?.Invoke(entity, reason);
            entity.Release();
            RemoveActive(entity, id);
            _ids.Free(id);
            _pool.Release(entity.gameObject);
            return true;
        }

        /// Live entity with id; false when id is not live or not an entity of this lifecycle.
        public bool TryGet(EntityHandle id, out SimEntity entity)
        {
            if (id.IsValid && id.Index < _byIndex.Count)
            {
                SimEntity candidate = _byIndex[id.Index];
                if (candidate != null && candidate.Id == id)
                {
                    entity = candidate;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        /// Run reset: despawns every live entity in ascending Id order (allowed outside the Lifecycle phase while it runs).
        public void DespawnAll(DespawnReason reason)
        {
            if (_clock.StepActive)
            {
                throw new InvalidOperationException("EntityLifecycle.DespawnAll is a run-boundary operation and cannot run inside a step.");
            }

            _scratch.Clear();
            _scratch.AddRange(_active);
            _scratch.Sort(ById);
            _runResetInProgress = true;
            try
            {
                for (int i = 0; i < _scratch.Count; i++)
                {
                    Despawn(_scratch[i].Id, reason);
                }
            }
            finally
            {
                _runResetInProgress = false;
                _scratch.Clear();
            }
        }

        /// Run boundary only, after DespawnAll: swaps the strategies of the next run. When the pool object changes, the outgoing
        /// pool's Clear() destroys its inactive instances (prewarming the new pool is the composition root's job, PoolPrewarmCounts).
        /// Throws InvalidOperationException while entities are live or a step is active, ArgumentNullException for null strategies.
        public void ReplaceStrategies(IPoolStrategy pool, ITickStrategy ticks, ICollisionStrategy collisions)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (ticks == null)
            {
                throw new ArgumentNullException(nameof(ticks));
            }

            if (collisions == null)
            {
                throw new ArgumentNullException(nameof(collisions));
            }

            if (_clock.StepActive || _active.Count > 0)
            {
                throw new InvalidOperationException("EntityLifecycle.ReplaceStrategies requires a run boundary after DespawnAll ("
                    + _active.Count + " entities still live).");
            }

            if (!ReferenceEquals(pool, _pool))
            {
                _pool.Clear();
            }

            bool changed = pool.Kind != _pool.Kind || ticks.Kind != _ticks.Kind || collisions.Kind != _collisions.Kind;
            _pool = pool;
            _ticks = ticks;
            _collisions = collisions;
            if (changed)
            {
                LoopLog.Info("Strategies replaced: pool=" + pool.Kind + ", tick=" + ticks.Kind + ", collision=" + collisions.Kind + ".");
            }
        }

        private void ThrowUnlessMutationAllowed(string operation)
        {
            bool inLifecycle = _clock.StepActive && _clock.CurrentPhase == SimulationPhase.Lifecycle;
            if (!inLifecycle && !_runResetInProgress)
            {
                throw new InvalidOperationException("EntityLifecycle." + operation + " is only allowed in the Lifecycle phase or during a run reset"
                    + " (phase " + _clock.CurrentPhase + ", step active " + _clock.StepActive + ").");
            }
        }

        private void AddActive(SimEntity entity)
        {
            int index = entity.Id.Index;
            while (_byIndex.Count <= index)
            {
                _byIndex.Add(null);
            }

            _byIndex[index] = entity;
            entity.ActiveSlot = _active.Count;
            _active.Add(entity);
        }

        private void RemoveActive(SimEntity entity, EntityHandle id)
        {
            _byIndex[id.Index] = null;
            int slot = entity.ActiveSlot;
            int last = _active.Count - 1;
            if (slot != last)
            {
                SimEntity moved = _active[last];
                _active[slot] = moved;
                moved.ActiveSlot = slot;
            }

            _active.RemoveAt(last);
            entity.ActiveSlot = -1;
        }
    }
}
