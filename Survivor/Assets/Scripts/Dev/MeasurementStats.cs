using System;
using System.Collections.Generic;

namespace Survivor.Dev
{
    /// Body values of one measurement (5.15.3 body columns, except monsterCountLimit which needs the group and N). Every
    /// nullable value is written as NA when null. Built from the measured frames by FromFrames.
    public sealed class MeasurementStats
    {
        private const double BytesPerMB = 1024d * 1024d;

        /// Frames in the measured window.
        public long Frames;

        /// Steps run in those frames (R27 per-step denominator).
        public long Steps;

        /// Frames that ran the catch-up maximum of 3 steps (slow motion).
        public long SlowmoFrames;

        /// Frames / unscaled seconds.
        public double? AvgFps;

        /// 1000 / mean of the slowest 1% frame times.
        public double? OnePercentLowFps;

        /// Longest frame time in ms.
        public double? WorstFrameMs;

        /// GC bytes allocated per frame (session sum / frames).
        public double? GcAllocPerFrameBytes;

        /// Mean / largest live monster count.
        public double? MonsterCountAvg, MonsterCountMax;

        /// R27 per-step ms of each R16 marker (StatsCollector.MarkerNames order).
        public double?[] MarkerPerStepMs = new double?[StatsCollector.MarkerCount];

        /// R27 per-frame ms of each R16 marker.
        public double?[] MarkerPerFrameMs = new double?[StatsCollector.MarkerCount];

        /// Mean / largest Total Used Memory in MB.
        public double? MemUsedAvgMB, MemUsedMaxMB;

        /// Mean / largest Total Reserved Memory in MB.
        public double? MemReservedAvgMB, MemReservedMaxMB;

        /// Mean / largest active gem count.
        public double? ActiveGemsAvg, ActiveGemsMax;

        /// Aggregates frames (R27 sums over counts, 1% low, worst frame, averages and maxima); a NaN anywhere in a profiler
        /// channel makes that channel NA. Null throws.
        public static MeasurementStats FromFrames(IReadOnlyList<MeasurementFrame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            var stats = new MeasurementStats { Frames = frames.Count };
            var frameMs = new List<double>(frames.Count);
            double seconds = 0d, gc = 0d, used = 0d, usedMax = double.MinValue, reserved = 0d, reservedMax = double.MinValue;
            double live = 0d, liveMax = double.MinValue, gems = 0d, gemsMax = double.MinValue;
            var markerTotals = new double[StatsCollector.MarkerCount];
            for (int i = 0; i < frames.Count; i++)
            {
                MeasurementFrame f = frames[i];
                frameMs.Add(f.UnscaledDtMs);
                seconds += f.UnscaledDtMs / 1000d;
                stats.Steps += f.Steps;
                if (f.Steps >= Survivor.Core.StepMath.MaxCatchUpSteps)
                {
                    stats.SlowmoFrames++;
                }

                gc += f.GcBytes;
                used += f.MemUsedBytes;
                usedMax = Math.Max(usedMax, f.MemUsedBytes);
                reserved += f.MemReservedBytes;
                reservedMax = Math.Max(reservedMax, f.MemReservedBytes);
                live += f.LiveMonsters;
                liveMax = Math.Max(liveMax, f.LiveMonsters);
                gems += f.ActiveGems;
                gemsMax = Math.Max(gemsMax, f.ActiveGems);
                for (int m = 0; m < markerTotals.Length; m++)
                {
                    markerTotals[m] += f.MarkerMs != null && m < f.MarkerMs.Length ? f.MarkerMs[m] : double.NaN;
                }
            }

            stats.AvgFps = MeasurementMath.AverageFps(stats.Frames, seconds);
            stats.OnePercentLowFps = MeasurementMath.OnePercentLowFps(frameMs);
            stats.WorstFrameMs = MeasurementMath.WorstFrameMs(frameMs);
            stats.GcAllocPerFrameBytes = Finite(MeasurementMath.PerFrameMs(gc, stats.Frames));
            for (int m = 0; m < markerTotals.Length; m++)
            {
                stats.MarkerPerStepMs[m] = Finite(MeasurementMath.PerStepMs(markerTotals[m], stats.Steps));
                stats.MarkerPerFrameMs[m] = Finite(MeasurementMath.PerFrameMs(markerTotals[m], stats.Frames));
            }

            if (stats.Frames > 0)
            {
                stats.MonsterCountAvg = live / stats.Frames;
                stats.MonsterCountMax = liveMax;
                stats.ActiveGemsAvg = gems / stats.Frames;
                stats.ActiveGemsMax = gemsMax;
                stats.MemUsedAvgMB = Finite(used / stats.Frames / BytesPerMB);
                stats.MemUsedMaxMB = Finite(usedMax / BytesPerMB);
                stats.MemReservedAvgMB = Finite(reserved / stats.Frames / BytesPerMB);
                stats.MemReservedMaxMB = Finite(reservedMax / BytesPerMB);
            }

            return stats;
        }

        // NaN (a channel with an unavailable counter) becomes null, written as NA. Math.Max propagates NaN as well.
        private static double? Finite(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) ? value : null;
        }
    }
}
