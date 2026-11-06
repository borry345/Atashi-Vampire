using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace Survivor.Strategies.Pool
{
    /// Pool strategy C: one UnityEngine.Pool.ObjectPool per template, collectionCheck on, maxSize int.MaxValue (TAC-F9-4).
    public sealed class UnityObjectPoolStrategy : IPoolStrategy
    {
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _poolByTemplate =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private readonly PoolInstanceTracker _tracker;

        /// Creates an empty strategy; per-template pools are created on first use.
        public UnityObjectPoolStrategy()
        {
            _tracker = new PoolInstanceTracker(this);
        }

        /// Always UnityObjectPool.
        public PoolStrategyKind Kind => PoolStrategyKind.UnityObjectPool;

        /// Number of instances currently acquired.
        public int ActiveCount => _tracker.ActiveCount;

        /// Gets an instance of template from its ObjectPool and activates it at position.
        public GameObject Acquire(GameObject template, Vector2 position)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                PoolInstanceTracker.ValidateTemplate(template);
                return _tracker.MarkAcquired(template, GetPool(template).Get(), position);
            }
        }

        /// Deactivates instance and returns it to its ObjectPool; throws InvalidOperationException on double or foreign release.
        public void Release(GameObject instance)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                GameObject template = _tracker.MarkReleased(instance);
                ReturnToPool(GetPool(template), instance);
            }
        }

        /// Instantiates count additional inactive instances of template into its ObjectPool.
        public void Prewarm(GameObject template, int count)
        {
            PoolInstanceTracker.ValidatePrewarm(template, count);
            ObjectPool<GameObject> pool = GetPool(template);

            // ObjectPool has no prewarm API: take every pooled instance plus count new ones, then give them back.
            // Releasing in reverse restores the existing stack order with the new instances underneath.
            int total = pool.CountInactive + count;
            var taken = new List<GameObject>(total);
            for (int i = 0; i < total; i++)
            {
                taken.Add(pool.Get());
            }

            for (int i = taken.Count - 1; i >= 0; i--)
            {
                ReturnToPool(pool, taken[i]);
            }
        }

        /// Releases every acquired instance, in acquire order.
        public void ReleaseAll()
        {
            foreach (GameObject instance in _tracker.SnapshotActiveInAcquireOrder())
            {
                Release(instance);
            }
        }

        private ObjectPool<GameObject> GetPool(GameObject template)
        {
            if (!_poolByTemplate.TryGetValue(template, out ObjectPool<GameObject> pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () => PoolInstanceTracker.CreateInstance(template),
                    actionOnGet: null,
                    actionOnRelease: null,
                    actionOnDestroy: UnityEngine.Object.Destroy,
                    collectionCheck: true,
                    defaultCapacity: 10,
                    maxSize: int.MaxValue);
                _poolByTemplate.Add(template, pool);
            }

            return pool;
        }

        // The tracker already rejects double releases before this point; collectionCheck is a further line of defense.
        // Whatever ObjectPool throws is surfaced as InvalidOperationException, as the contract requires (U21).
        private static void ReturnToPool(ObjectPool<GameObject> pool, GameObject instance)
        {
            try
            {
                pool.Release(instance);
            }
            catch (Exception exception)
            {
                throw PoolInstanceTracker.Fail(
                    $"ObjectPool rejected '{instance.name}' (id {instance.GetInstanceID()}): {exception.Message}");
            }
        }
    }
}
