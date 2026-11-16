using System;
using System.Collections.Generic;

namespace Survivor.Dev
{
    /// R27 and the frame statistics of a measurement (TAC-F10-1): per-step / per-frame ms are session sums over session
    /// counts, never an average of per-frame ratios. Every function returns null (written as NA) when it has no denominator.
    public static class MeasurementMath
    {
        /// Average FPS under 55 marks the monster count limit (SPEC F10 item 4).
        public const double MonsterLimitFps = 55d;

        /// R27: session total ms / session steps; null when steps is 0 or less.
        public static double? PerStepMs(double totalMs, long steps)
        {
            return steps > 0 ? totalMs / steps : (double?)null;
        }

        /// R27: session total ms / session frames; null when frames is 0 or less.
        public static double? PerFrameMs(double totalMs, long frames)
        {
            return frames > 0 ? totalMs / frames : (double?)null;
        }

        /// Frames / unscaled seconds (5.15.3); null when there is no frame or no time.
        public static double? AverageFps(long frames, double totalSeconds)
        {
            return frames > 0 && totalSeconds > 0d ? frames / totalSeconds : (double?)null;
        }

        /// 1% low = 1000 / mean of the slowest ceil(1%) frame times in ms (at least one frame); null for no frames or a zero
        /// mean. frameMs is not modified. Null throws.
        public static double? OnePercentLowFps(IReadOnlyList<double> frameMs)
        {
            if (frameMs == null)
            {
                throw new ArgumentNullException(nameof(frameMs));
            }

            if (frameMs.Count == 0)
            {
                return null;
            }

            var sorted = new double[frameMs.Count];
            for (int i = 0; i < sorted.Length; i++)
            {
                sorted[i] = frameMs[i];
            }

            Array.Sort(sorted);
            int take = SlowestCount(sorted.Length);
            double sum = 0d;
            for (int i = sorted.Length - take; i < sorted.Length; i++)
            {
                sum += sorted[i];
            }

            double mean = sum / take;
            return mean > 0d ? 1000d / mean : (double?)null;
        }

        /// Number of frames in the slowest 1%: ceil(count / 100), at least 1 (0 for no frames).
        public static int SlowestCount(int count)
        {
            return count <= 0 ? 0 : Math.Max(1, (count + 99) / 100);
        }

        /// Longest frame time in ms; null for no frames. Null throws.
        public static double? WorstFrameMs(IReadOnlyList<double> frameMs)
        {
            if (frameMs == null)
            {
                throw new ArgumentNullException(nameof(frameMs));
            }

            if (frameMs.Count == 0)
            {
                return null;
            }

            double worst = frameMs[0];
            for (int i = 1; i < frameMs.Count; i++)
            {
                worst = Math.Max(worst, frameMs[i]);
            }

            return worst;
        }
    }
}
