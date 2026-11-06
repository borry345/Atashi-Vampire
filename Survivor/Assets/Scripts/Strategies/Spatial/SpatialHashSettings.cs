using System;
using Survivor.Core;

namespace Survivor.Strategies
{
    /// Tunable spatial hash parameters (TAC-F16-4); App may change CellSize between runs (dev mode), recorded as cellSize.
    public sealed class SpatialHashSettings
    {
        private float _cellSize = StrategySelection.DefaultSpatialHashCellSize;

        /// Cell edge length in world units; defaults to StrategySelection.DefaultSpatialHashCellSize. Throws ArgumentOutOfRangeException unless positive and finite.
        public float CellSize
        {
            get => _cellSize;
            set
            {
                if (!IsValidCellSize(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Spatial hash cell size must be a positive finite number.");
                }

                _cellSize = value;
            }
        }

        /// True when cellSize is a positive finite number (NaN and infinities are rejected).
        public static bool IsValidCellSize(float cellSize)
        {
            return cellSize > 0f && !float.IsInfinity(cellSize);
        }
    }
}
