using UnityEngine;

namespace Survivor.Core
{
    /// Nearest-monster query for weapons only (monsters use StepContext.PlayerPosition); all implementations must return identical results.
    public interface ITargetProvider
    {
        /// Which target provider this is.
        TargetProviderKind Kind { get; }

        /// Receives the frozen monster snapshot; called once per step in BuildIndex.
        void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot);

        /// Nearest monster to point by R6; false when no monster exists. Never throws.
        bool TryFindNearest(Vector2 point, out TargetHit hit);
    }
}
