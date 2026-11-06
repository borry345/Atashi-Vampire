using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Generic center-point hash over unbounded coordinates (payload int); owner of the R14 cell key functions.
    public sealed class SpatialHash
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly Stack<List<int>> _freeLists = new Stack<List<int>>();
        private int _count;
        private int _minCellX;
        private int _maxCellX;
        private int _minCellY;
        private int _maxCellY;

        /// Creates an empty hash; throws ArgumentOutOfRangeException unless cellSize is positive and finite.
        public SpatialHash(float cellSize)
        {
            if (!SpatialHashSettings.IsValidCellSize(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Spatial hash cell size must be a positive finite number.");
            }

            _cellSize = cellSize;
        }

        /// Cell edge length in world units.
        public float CellSize => _cellSize;

        /// Number of payloads inserted since the last Clear.
        public int Count => _count;

        /// R14: key of the cell containing p, i.e. KeyOf(FloorToInt(p.x / cellSize), FloorToInt(p.y / cellSize)).
        public static long CellOf(Vector2 p, float cellSize)
        {
            return KeyOf(CellCoordinate(p.x, cellSize), CellCoordinate(p.y, cellSize));
        }

        /// R14: ((long)cx << 32) | (uint)cy; cy keeps its two's complement bits in the low 32 bits.
        public static long KeyOf(int cx, int cy)
        {
            return ((long)cx << 32) | (uint)cy;
        }

        /// R14 cell coordinate along one axis: FloorToInt(value / cellSize) (never truncation toward zero).
        internal static int CellCoordinate(float value, float cellSize)
        {
            return Mathf.FloorToInt(value / cellSize);
        }

        /// Smallest occupied cell x; meaningful only when Count > 0.
        internal int MinCellX => _minCellX;

        /// Largest occupied cell x; meaningful only when Count > 0.
        internal int MaxCellX => _maxCellX;

        /// Smallest occupied cell y; meaningful only when Count > 0.
        internal int MinCellY => _minCellY;

        /// Largest occupied cell y; meaningful only when Count > 0.
        internal int MaxCellY => _maxCellY;

        /// Occupied cells keyed by R14 key; payload lists are in insertion order and must not be modified.
        internal Dictionary<long, List<int>> Cells => _cells;

        /// Payloads of cell (cx, cy) in insertion order; false when the cell is empty. The list must not be modified.
        internal bool TryGetCell(int cx, int cy, out List<int> payloads)
        {
            return _cells.TryGetValue(KeyOf(cx, cy), out payloads);
        }

        /// Removes every payload (cell lists are recycled).
        public void Clear()
        {
            foreach (List<int> list in _cells.Values)
            {
                list.Clear();
                _freeLists.Push(list);
            }

            _cells.Clear();
            _count = 0;
        }

        /// Adds payload to the cell that contains position (R14).
        public void Insert(int payload, Vector2 position)
        {
            int cx = CellCoordinate(position.x, _cellSize);
            int cy = CellCoordinate(position.y, _cellSize);
            long key = KeyOf(cx, cy);
            if (!_cells.TryGetValue(key, out List<int> list))
            {
                list = _freeLists.Count > 0 ? _freeLists.Pop() : new List<int>();
                _cells.Add(key, list);
            }

            list.Add(payload);
            if (_count == 0)
            {
                _minCellX = cx;
                _maxCellX = cx;
                _minCellY = cy;
                _maxCellY = cy;
            }
            else
            {
                _minCellX = Math.Min(_minCellX, cx);
                _maxCellX = Math.Max(_maxCellX, cx);
                _minCellY = Math.Min(_minCellY, cy);
                _maxCellY = Math.Max(_maxCellY, cy);
            }

            _count++;
        }

        /// Clears payloadsOut, then adds every payload in the cells overlapping the square [center - radius, center + radius]
        /// (a superset of the payloads within radius; callers filter by exact distance). Order: rows by cy ascending,
        /// cells by cx ascending, payloads in insertion order. Throws ArgumentNullException when payloadsOut is null.
        public void QueryCells(Vector2 center, float radius, List<int> payloadsOut)
        {
            if (payloadsOut == null)
            {
                throw new ArgumentNullException(nameof(payloadsOut));
            }

            payloadsOut.Clear();
            if (_count == 0 || !(radius >= 0f))
            {
                return;
            }

            // Clip to the occupied cell range so a huge radius never walks empty cells.
            int fromX = Math.Max(CellCoordinate(center.x - radius, _cellSize), _minCellX);
            int toX = Math.Min(CellCoordinate(center.x + radius, _cellSize), _maxCellX);
            int fromY = Math.Max(CellCoordinate(center.y - radius, _cellSize), _minCellY);
            int toY = Math.Min(CellCoordinate(center.y + radius, _cellSize), _maxCellY);
            for (long cy = fromY; cy <= toY; cy++)
            {
                for (long cx = fromX; cx <= toX; cx++)
                {
                    if (TryGetCell((int)cx, (int)cy, out List<int> list))
                    {
                        payloadsOut.AddRange(list);
                    }
                }
            }
        }
    }
}
