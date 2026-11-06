using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Pool
{
    /// Pool strategy A: per-template LIFO stack of inactive instances that grows without a cap (TAC-F9-2, Q13).
    public sealed class CustomPoolStrategy : IPoolStrategy
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> _inactiveByTemplate =
            new Dictionary<GameObject, Stack<GameObject>>();
        private readonly PoolInstanceTracker _tracker;

        /// Creates an empty pool.
        public CustomPoolStrategy()
        {
            _tracker = new PoolInstanceTracker(this);
        }

        /// Always CustomPool.
        public PoolStrategyKind Kind => PoolStrategyKind.CustomPool;

        /// Number of instances currently acquired.
        public int ActiveCount => _tracker.ActiveCount;

        /// Pops the most recently released instance of template (instantiating one if empty) and activates it at position.
        public GameObject Acquire(GameObject template, Vector2 position)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                PoolInstanceTracker.ValidateTemplate(template);
                Stack<GameObject> inactive = GetStack(template);
                GameObject instance = inactive.Count > 0 ? inactive.Pop() : PoolInstanceTracker.CreateInstance(template);
                return _tracker.MarkAcquired(template, instance, position);
            }
        }

        /// Deactivates instance and pushes it onto its template stack; throws InvalidOperationException on double or foreign release.
        public void Release(GameObject instance)
        {
            using (ProfilerMarkers.Pool.Auto())
            {
                GameObject template = _tracker.MarkReleased(instance);
                GetStack(template).Push(instance);
            }
        }

        /// Instantiates count additional inactive instances of template onto its stack.
        public void Prewarm(GameObject template, int count)
        {
            PoolInstanceTracker.ValidatePrewarm(template, count);
            Stack<GameObject> inactive = GetStack(template);
            for (int i = 0; i < count; i++)
            {
                inactive.Push(PoolInstanceTracker.CreateInstance(template));
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

        private Stack<GameObject> GetStack(GameObject template)
        {
            if (!_inactiveByTemplate.TryGetValue(template, out Stack<GameObject> inactive))
            {
                inactive = new Stack<GameObject>();
                _inactiveByTemplate.Add(template, inactive);
            }

            return inactive;
        }
    }
}
