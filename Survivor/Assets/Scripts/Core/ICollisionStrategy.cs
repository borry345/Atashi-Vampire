using System.Collections.Generic;

namespace Survivor.Core
{
    /// Polling collision detection; both strategies must emit the same sorted event set.
    public interface ICollisionStrategy
    {
        /// Which collision strategy this is.
        CollisionStrategyKind Kind { get; }

        /// Adds a body; it participates from the next CollectEvents.
        void Register(CollisionBody body);

        /// Removes a body and drops its overlaps silently (no Exit).
        void Unregister(CollisionBody body);

        /// Tells the strategy that body.Radius changed.
        void NotifyRadiusChanged(CollisionBody body);

        /// Enter/Exit events of this step, sorted ascending; the list is reused until the next call.
        IReadOnlyList<CollisionEvent> CollectEvents(long step);

        /// Removes every body and overlap (run reset).
        void Clear();
    }
}
