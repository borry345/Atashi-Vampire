using System;
using System.Collections.Generic;

namespace Survivor.Data
{
    /// Experience curve: Thresholds[L-1] is the experience needed for level L -> L+1; beyond the table each level adds GrowthAfterTable (R21).
    public sealed class XpCurveDefinition
    {
        /// Creates a curve; thresholds must be non-empty with every value >= 1 and growthAfterTable >= 0 (ArgumentException otherwise).
        public XpCurveDefinition(IReadOnlyList<int> thresholds, int growthAfterTable)
        {
            if (thresholds == null)
            {
                throw new ArgumentNullException(nameof(thresholds));
            }

            if (thresholds.Count == 0)
            {
                throw new ArgumentException("At least one threshold is required.", nameof(thresholds));
            }

            var copy = new int[thresholds.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                if (thresholds[i] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(thresholds), thresholds[i], "Every threshold must be at least 1.");
                }

                copy[i] = thresholds[i];
            }

            if (growthAfterTable < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(growthAfterTable), growthAfterTable, "growthAfterTable must be non-negative.");
            }

            Thresholds = copy;
            GrowthAfterTable = growthAfterTable;
        }

        /// Required experience per level, starting at level 1.
        public IReadOnlyList<int> Thresholds { get; }

        /// Extra experience added per level past the end of the table.
        public int GrowthAfterTable { get; }
    }
}
