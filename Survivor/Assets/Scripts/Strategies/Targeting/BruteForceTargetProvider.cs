using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Target provider A: scans every snapshot entry (baseline for measurement, not for shipping).
    public sealed class BruteForceTargetProvider : ITargetProvider
    {
        private MonsterSnapshotBuffer _snapshot;

        /// TargetProviderKind.BruteForce.
        public TargetProviderKind Kind => TargetProviderKind.BruteForce;

        /// Keeps the frozen snapshot of this step for TryFindNearest; null forgets it.
        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
            _snapshot = snapshot;
        }

        /// Nearest monster to point by squared center distance, ties by smaller Id (R6); false when there is no monster
        /// or no snapshot yet. Never throws.
        public bool TryFindNearest(Vector2 point, out TargetHit hit)
        {
            using (ProfilerMarkers.Targeting.Auto())
            {
                hit = default;
                MonsterSnapshotBuffer snapshot = _snapshot;
                if (snapshot == null || snapshot.Count == 0)
                {
                    return false;
                }

                bool found = false;
                float bestD2 = 0f;
                EntityHandle bestId = default;
                Vector2 bestPosition = default;
                int count = snapshot.Count;
                for (int i = 0; i < count; i++)
                {
                    ref readonly MonsterSnapshot entry = ref snapshot[i];
                    float d2 = (entry.Position - point).sqrMagnitude;
                    if (!found || TargetOrdering.IsBetter(d2, entry.Id, bestD2, bestId))
                    {
                        found = true;
                        bestD2 = d2;
                        bestId = entry.Id;
                        bestPosition = entry.Position;
                    }
                }

                hit = new TargetHit(bestId, bestPosition, bestD2);
                return true;
            }
        }
    }
}
