using UnityEngine;

namespace Survivor.Core
{
    /// Supplies inactive-to-active GameObjects; never touches gameplay state.
    public interface IPoolStrategy
    {
        /// Which pool strategy this is.
        PoolStrategyKind Kind { get; }

        /// Returns an instance of template, active at position.
        GameObject Acquire(GameObject template, Vector2 position);

        /// Returns an instance; throws InvalidOperationException on double release / foreign object.
        void Release(GameObject instance);

        /// Creates count inactive instances ahead of time; no-op for InstantiateDestroy.
        void Prewarm(GameObject template, int count);

        /// Run reset: every active instance released (InstantiateDestroy: destroyed).
        void ReleaseAll();

        /// Destroys every inactive instance this strategy still holds (CustomPool: per-template stacks, UnityObjectPool: ObjectPool.Clear, InstantiateDestroy: nothing to do); throws InvalidOperationException while ActiveCount > 0.
        /// Called by EntityLifecycle.ReplaceStrategies on the outgoing pool after DespawnAll, before the incoming pool is prewarmed.
        void Clear();

        /// Number of instances currently acquired.
        int ActiveCount { get; }
    }
}
