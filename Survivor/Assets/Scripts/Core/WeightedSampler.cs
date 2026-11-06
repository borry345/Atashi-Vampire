using System;
using System.Collections.Generic;

namespace Survivor.Core
{
    /// Weighted draws over candidates kept in canonical order (rule R9).
    public static class WeightedSampler
    {
        /// Draws one index with replacement: u = NextFloat01() * total, first index whose running sum > u; weights <= 0 are skipped.
        /// Returns -1 without consuming the generator when no weight is positive.
        public static int PickIndex(IReadOnlyList<float> weights, DeterministicRng rng)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            return PickExcluding(weights, null, rng);
        }

        /// Draws up to count distinct indices into indicesOut (cleared first, draw order kept); each drawn index is removed before the next draw.
        /// Stops early when no positive weight remains. Throws ArgumentOutOfRangeException if count < 0.
        public static void SampleWithoutReplacement(IReadOnlyList<float> weights, int count, DeterministicRng rng, List<int> indicesOut)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (indicesOut == null)
            {
                throw new ArgumentNullException(nameof(indicesOut));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be non-negative.");
            }

            indicesOut.Clear();
            if (count == 0)
            {
                return;
            }

            var removed = new bool[weights.Count];
            while (indicesOut.Count < count)
            {
                int index = PickExcluding(weights, removed, rng);
                if (index < 0)
                {
                    return;
                }

                removed[index] = true;
                indicesOut.Add(index);
            }
        }

        private static int PickExcluding(IReadOnlyList<float> weights, bool[] removed, DeterministicRng rng)
        {
            float total = 0f;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (IsEligible(weights, removed, i))
                {
                    total += weights[i];
                    lastPositive = i;
                }
            }

            if (lastPositive < 0)
            {
                return -1;
            }

            float u = rng.NextFloat01() * total;
            float cumulative = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (!IsEligible(weights, removed, i))
                {
                    continue;
                }

                cumulative += weights[i];
                if (cumulative > u)
                {
                    return i;
                }
            }

            // Float rounding can leave u at the very top of the range; the last positive candidate owns it.
            return lastPositive;
        }

        private static bool IsEligible(IReadOnlyList<float> weights, bool[] removed, int i)
        {
            return weights[i] > 0f && (removed == null || !removed[i]);
        }
    }
}
