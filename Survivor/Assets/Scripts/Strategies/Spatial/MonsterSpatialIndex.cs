using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Shared monster spatial hash; rebuilt at most once per step, reused by SpatialHashTargetProvider and SelfCollisionStrategy.
    /// Registered as the BuildIndex order 10 phase handler; large monsters (MonsterSnapshot.IsLarge, R25) are kept out of the hash.
    public sealed class MonsterSpatialIndex : IStepPhaseHandler
    {
        private readonly MonsterSnapshotBuffer _snapshot;
        private readonly SpatialHash _hash;
        private readonly List<int> _largeIndices = new List<int>();
        private readonly Dictionary<long, int> _cellCounts = new Dictionary<long, int>();
        private bool _cellCountsDirty;
        private float _maxHashedRadius;

        /// Creates an empty index over snapshot (the buffer MonsterRegistry fills each step). Throws ArgumentOutOfRangeException
        /// unless cellSize is positive and finite, ArgumentNullException when snapshot is null.
        public MonsterSpatialIndex(float cellSize, MonsterSnapshotBuffer snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _hash = new SpatialHash(cellSize);
        }

        /// Whether BuildIndex rebuilds the index; set by the composition root / target provider switch (R19, strategy choice).
        public bool Required { get; set; }

        /// Cell edge length in world units.
        public float CellSize => _hash.CellSize;

        /// Monster count per occupied cell (R14 key), for dev visualization only; large monsters are not counted.
        public IReadOnlyDictionary<long, int> CellCounts
        {
            get
            {
                if (_cellCountsDirty)
                {
                    _cellCounts.Clear();
                    foreach (KeyValuePair<long, List<int>> cell in _hash.Cells)
                    {
                        _cellCounts.Add(cell.Key, cell.Value.Count);
                    }

                    _cellCountsDirty = false;
                }

                return _cellCounts;
            }
        }

        /// Snapshot buffer this index reads (internal: lets SpatialHashTargetProvider verify it was handed the same buffer).
        internal MonsterSnapshotBuffer Snapshot => _snapshot;

        /// Snapshot indices of the large monsters at the last rebuild, ascending.
        internal List<int> LargeIndices => _largeIndices;

        /// True when at least one non-large monster is hashed.
        internal bool HasHashedEntries => _hash.Count > 0;

        /// Rebuilds from the snapshot in BuildIndex when Required; any other phase does nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.BuildIndex || !Required)
            {
                return;
            }

            using (ProfilerMarkers.SpatialIndex.Auto())
            {
                Rebuild();
            }
        }

        /// Clears indicesOut, then adds the snapshot index of every hashed monster whose circle can overlap the query circle
        /// (cells within radius + MaxHashedRadius; a superset, callers test exact overlap) plus every large monster.
        /// Result is ascending (= ascending Id). Throws ArgumentNullException when snapshotIndicesOut is null.
        public void QueryCircle(Vector2 center, float radius, List<int> snapshotIndicesOut)
        {
            if (snapshotIndicesOut == null)
            {
                throw new ArgumentNullException(nameof(snapshotIndicesOut));
            }

            _hash.QueryCells(center, radius + _maxHashedRadius, snapshotIndicesOut);
            snapshotIndicesOut.AddRange(_largeIndices);
            snapshotIndicesOut.Sort();
        }

        /// Clears indicesOut, then adds the snapshot indices of hashed monsters in the cells at Chebyshev cell distance exactly
        /// ring from point's cell (ring 0 = point's own cell). Large monsters are never included. Throws ArgumentNullException
        /// when snapshotIndicesOut is null, ArgumentOutOfRangeException when ring is negative.
        public void ForEachRing(Vector2 point, int ring, List<int> snapshotIndicesOut)
        {
            if (snapshotIndicesOut == null)
            {
                throw new ArgumentNullException(nameof(snapshotIndicesOut));
            }

            if (ring < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ring), ring, "Ring must be zero or positive.");
            }

            snapshotIndicesOut.Clear();
            if (_hash.Count == 0)
            {
                return;
            }

            long cx0 = SpatialHash.CellCoordinate(point.x, CellSize);
            long cy0 = SpatialHash.CellCoordinate(point.y, CellSize);
            long left = cx0 - ring;
            long right = cx0 + ring;
            long bottom = cy0 - ring;
            long top = cy0 + ring;
            long fromX = Math.Max(left, _hash.MinCellX);
            long toX = Math.Min(right, _hash.MaxCellX);
            long fromY = Math.Max(bottom, _hash.MinCellY);
            long toY = Math.Min(top, _hash.MaxCellY);
            for (long cy = fromY; cy <= toY; cy++)
            {
                if (cy == bottom || cy == top)
                {
                    for (long cx = fromX; cx <= toX; cx++)
                    {
                        AddCell(cx, cy, snapshotIndicesOut);
                    }
                }
                else
                {
                    if (left >= _hash.MinCellX)
                    {
                        AddCell(left, cy, snapshotIndicesOut);
                    }

                    if (right <= _hash.MaxCellX)
                    {
                        AddCell(right, cy, snapshotIndicesOut);
                    }
                }
            }
        }

        /// Removes every entry; run reset, called by App right after RunSession.Reset and before the clock opens (TAC-F8-2).
        public void Clear()
        {
            _hash.Clear();
            _largeIndices.Clear();
            _maxHashedRadius = 0f;
            _cellCountsDirty = true;
        }

        /// Ring of the first ring that can contain a hashed monster (Chebyshev cell distance from point's cell to the occupied range).
        internal int FirstOccupiedRing(Vector2 point)
        {
            long cx0 = SpatialHash.CellCoordinate(point.x, CellSize);
            long cy0 = SpatialHash.CellCoordinate(point.y, CellSize);
            long dx = Math.Max(0L, Math.Max(_hash.MinCellX - cx0, cx0 - _hash.MaxCellX));
            long dy = Math.Max(0L, Math.Max(_hash.MinCellY - cy0, cy0 - _hash.MaxCellY));
            return (int)Math.Min(int.MaxValue, Math.Max(dx, dy));
        }

        /// True when rings 0..ring around point's cell cover every occupied cell, i.e. the search left the occupied range.
        internal bool RingCoversOccupied(Vector2 point, int ring)
        {
            long cx0 = SpatialHash.CellCoordinate(point.x, CellSize);
            long cy0 = SpatialHash.CellCoordinate(point.y, CellSize);
            return cx0 - ring <= _hash.MinCellX && cx0 + ring >= _hash.MaxCellX
                && cy0 - ring <= _hash.MinCellY && cy0 + ring >= _hash.MaxCellY;
        }

        private void Rebuild()
        {
            Clear();
            int count = _snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                ref readonly MonsterSnapshot entry = ref _snapshot[i];
                if (entry.IsLarge)
                {
                    _largeIndices.Add(i);
                    continue;
                }

                _hash.Insert(i, entry.Position);
            }

            _maxHashedRadius = _snapshot.MaxHashedRadius;
        }

        private void AddCell(long cx, long cy, List<int> snapshotIndicesOut)
        {
            if (_hash.TryGetCell((int)cx, (int)cy, out List<int> payloads))
            {
                snapshotIndicesOut.AddRange(payloads);
            }
        }
    }
}
