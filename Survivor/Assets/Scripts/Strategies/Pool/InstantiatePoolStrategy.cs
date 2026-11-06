using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Pool
{
    /// Pool strategy B (baseline, no pooling): Instantiate on every acquire, deactivate then Destroy on every release (TAC-F9-3).
    public sealed class InstantiatePoolStrategy : IPoolStrategy
    {
        private readonly PoolInstanceTracker _tracker;

        /// Creates the strategy.
        public InstantiatePoolStrategy()
        {
            _tracker = new PoolInstanceTracker(this);
        }

        /// Always InstantiateDestroy.
        public PoolStrategyKind Kind => PoolStrategyKind.InstantiateDestroy;

        /// Number of instances currently acquired.
        public int ActiveCount => _tracker.ActiveCount;

        /// Instantiates a new instance of template and activates it at position.
        public GameObject Acquire(GameObject template, Vector2 position)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                PoolInstanceTracker.ValidateTemplate(template);
                return _tracker.MarkAcquired(template, PoolInstanceTracker.CreateInstance(template), position);
            }
        }

        /// Deactivates instance, then destroys it; throws InvalidOperationException on double or foreign release.
        public void Release(GameObject instance)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                // Deactivate first: Destroy only takes effect at the end of the frame (U22),
                // so later steps in the same frame must already see the instance as gone.
                _tracker.MarkReleased(instance);
                Object.Destroy(instance);
            }
        }

        /// Validates the arguments like the other strategies, then does nothing: no inactive instances are kept.
        public void Prewarm(GameObject template, int count)
        {
            PoolInstanceTracker.ValidatePrewarm(template, count);
        }

        /// Destroys every acquired instance, in acquire order.
        public void ReleaseAll()
        {
            foreach (GameObject instance in _tracker.SnapshotActiveInAcquireOrder())
            {
                Release(instance);
            }
        }
    }
}
