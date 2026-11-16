namespace Survivor.Dev
{
    /// Raw values of one measured frame (5.15.3 frames file). Profiler-backed values are NaN when the counter is not
    /// available (written as NA); MarkerMs follows StatsCollector.MarkerNames (R16 order).
    public readonly struct MeasurementFrame
    {
        /// Creates a frame record; markerMs is kept as given (not copied).
        public MeasurementFrame(long frame, double unscaledDtMs, int steps, double gcBytes, double memUsedBytes,
                                double memReservedBytes, int liveMonsters, int activeGems, double[] markerMs)
        {
            Frame = frame;
            UnscaledDtMs = unscaledDtMs;
            Steps = steps;
            GcBytes = gcBytes;
            MemUsedBytes = memUsedBytes;
            MemReservedBytes = memReservedBytes;
            LiveMonsters = liveMonsters;
            ActiveGems = activeGems;
            MarkerMs = markerMs;
        }

        /// 0-based index of the frame within the measured window.
        public readonly long Frame;

        /// Unscaled frame time in ms.
        public readonly double UnscaledDtMs;

        /// Simulation steps run in the frame (IDevModeHost.LastFrameSteps).
        public readonly int Steps;

        /// "GC Allocated In Frame" in bytes (NaN = not available).
        public readonly double GcBytes;

        /// "Total Used Memory" in bytes (NaN = not available).
        public readonly double MemUsedBytes;

        /// "Total Reserved Memory" in bytes (NaN = not available).
        public readonly double MemReservedBytes;

        /// Live monsters, boss included (IDevModeHost.LiveMonsterCount).
        public readonly int LiveMonsters;

        /// Live experience gems (IDevModeHost.ActiveGemCount).
        public readonly int ActiveGems;

        /// Frame total of each R16 marker in ms (NaN = not available).
        public readonly double[] MarkerMs;
    }
}
