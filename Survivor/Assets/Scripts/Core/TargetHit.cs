using UnityEngine;

namespace Survivor.Core
{
    /// Result of a nearest-target query.
    public readonly struct TargetHit
    {
        /// Id of the chosen monster.
        public readonly EntityHandle Id;

        /// Snapshot position of the chosen monster.
        public readonly Vector2 Position;

        /// Squared distance from the query point to Position.
        public readonly float DistanceSquared;

        /// Creates a hit.
        public TargetHit(EntityHandle id, Vector2 position, float distanceSquared)
        {
            Id = id;
            Position = position;
            DistanceSquared = distanceSquared;
        }
    }
}
