using UnityEngine;

namespace Survivor.Core
{
    /// Drives IUpdatable ticks either centrally or through per-object self tickers (F17).
    public interface ITickStrategy
    {
        /// Which tick strategy this is.
        TickStrategyKind Kind { get; }

        /// Adds an updatable; per-object: binds and enables host's self ticker.
        void Register(IUpdatable updatable, GameObject host);

        /// Removes an updatable; safe during RunGroup.
        void Unregister(IUpdatable updatable);

        /// Ticks every registered updatable of the group; per-object: no-op.
        void RunGroup(TickGroup group, in StepContext context);

        /// Number of registered updatables in the group.
        int RegisteredCount(TickGroup group);

        /// Removes every registration (run reset).
        void Clear();
    }
}
