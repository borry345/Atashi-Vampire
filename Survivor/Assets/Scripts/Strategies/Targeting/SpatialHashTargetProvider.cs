using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Target provider B: ring search over MonsterSpatialIndex, then a full scan of large monsters (TAC-F16-2, R25).
    /// Returns exactly what BruteForceTargetProvider returns (TAC-F16-1); the index must be Required so BuildIndex rebuilds it.
    public sealed class SpatialHashTargetProvider : ITargetProvider
    {
        // Slack on the ring lower bound so float rounding in cell assignment (FloorToInt(x / cellSize)) and in the
        // squared distance can never stop the search before an equal-or-closer monster in the next ring (needed for bit equality with A).
        private const float RingBoundSlack = 1e-5f;

        private readonly MonsterSpatialIndex _index;
        private readonly List<int> _ringIndices = new List<int>();
        private MonsterSnapshotBuffer _snapshot;

        /// Creates a provider over index; throws ArgumentNullException when index is null.
        public SpatialHashTargetProvider(MonsterSpatialIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// TargetProviderKind.SpatialHash.
        public TargetProviderKind Kind => TargetProviderKind.SpatialHash;

        /// Keeps the frozen snapshot of this step; null forgets it. Throws ArgumentException when it is not the buffer the index reads.
        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
            if (snapshot != null && !ReferenceEquals(snapshot, _index.Snapshot))
            {
                throw new ArgumentException("SpatialHashTargetProvider: snapshot must be the buffer the MonsterSpatialIndex was built over.", nameof(snapshot));
            }

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

                var best = new Best();
                if (_index.HasHashedEntries)
                {
                    SearchRings(snapshot, point, ref best);
                }

                List<int> large = _index.LargeIndices;
                for (int i = 0; i < large.Count; i++)
                {
                    best.Consider(snapshot, large[i], point);
                }

                if (!best.Found)
                {
                    return false;
                }

                hit = new TargetHit(best.Id, best.Position, best.DistanceSquared);
                return true;
            }
        }

        private void SearchRings(MonsterSnapshotBuffer snapshot, Vector2 point, ref Best best)
        {
            float cellSize = _index.CellSize;
            float slack = RingBoundSlack * (Mathf.Abs(point.x) + Mathf.Abs(point.y));
            for (int ring = _index.FirstOccupiedRing(point); ; ring++)
            {
                _index.ForEachRing(point, ring, _ringIndices);
                for (int i = 0; i < _ringIndices.Count; i++)
                {
                    best.Consider(snapshot, _ringIndices[i], point);
                }

                if (_index.RingCoversOccupied(point, ring) || ring == int.MaxValue)
                {
                    return;
                }

                // TAC-F16-2: every unvisited hashed monster lies outside rings 0..ring, so more than ring * cellSize from point
                // in exact arithmetic. The slack and the strict '<' make the stop safe under float rounding too, so an
                // equidistant monster with a smaller Id (R6) in the next ring is never skipped.
                if (best.Found && ring > 0)
                {
                    float bound = ring * cellSize - (slack + RingBoundSlack * (ring + 1) * cellSize);
                    if (bound > 0f && best.DistanceSquared < bound * bound)
                    {
                        return;
                    }
                }
            }
        }

        private struct Best
        {
            public bool Found;
            public float DistanceSquared;
            public EntityHandle Id;
            public Vector2 Position;

            public void Consider(MonsterSnapshotBuffer snapshot, int index, Vector2 point)
            {
                ref readonly MonsterSnapshot entry = ref snapshot[index];
                float d2 = (entry.Position - point).sqrMagnitude;
                if (!Found || TargetOrdering.IsBetter(d2, entry.Id, DistanceSquared, Id))
                {
                    Found = true;
                    DistanceSquared = d2;
                    Id = entry.Id;
                    Position = entry.Position;
                }
            }
        }
    }
}
