using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Stateless spawn schedule (TAC-F3-2): nothing accumulates, so skipped spawns can never be released in a burst later.
    public static class SpawnScheduler
    {
        /// Spawns due at step k of a segment: floor((k+1) * rate / 60) - floor(k * rate / 60); any 60 consecutive steps of an
        /// integer rate give exactly rate spawns. Throws ArgumentOutOfRangeException for k < 0 or a negative / NaN / infinite rate.
        public static int SpawnsDueAtStep(float rate, long k)
        {
            if (k < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), k, "k must be non-negative.");
            }

            if (!(rate >= 0f) || float.IsInfinity(rate))
            {
                throw new ArgumentOutOfRangeException(nameof(rate), rate, "rate must be a finite, non-negative number.");
            }

            // Double keeps (k + 1) * rate exact for every k of a run; IEEE division of exact values is deterministic.
            double stepsPerSecond = StepMath.StepsPerSecond;
            return (int)(Math.Floor((k + 1) * (double)rate / stepsPerSecond) - Math.Floor(k * (double)rate / stepsPerSecond));
        }
    }
}
