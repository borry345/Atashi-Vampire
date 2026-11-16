using System;
using System.Collections.Generic;
using Survivor.Core;
using Unity.Profiling;

namespace Survivor.Dev
{
    /// Per-frame measurement collector (TAC-F10-1, TAC-F19-7): ProfilerRecorder on the seven R16 markers plus "GC Allocated
    /// In Frame", "Total Used Memory" and "Total Reserved Memory", the unscaled frame time and the host counts. Records s_N
    /// (first step with at least N live monsters) and keeps only frames that start at step s_N + MeasurementWarmupSteps or
    /// later (TAC-F19-7), so the kept steps are exactly the ones after the warmup up to StopAtStep.
    /// A recorder's LastValue at frame k describes frame k-1, so each frame is completed one Observe later and the host values
    /// of that earlier frame are kept until then.
    public sealed class StatsCollector : IDisposable
    {
        /// Number of R16 markers.
        public const int MarkerCount = 7;

        /// R16 marker names in the fixed column order.
        public static readonly IReadOnlyList<string> MarkerNames = new[]
        {
            "Survivor.Step", "Survivor.Pool", "Survivor.Targeting", "Survivor.SpatialIndex", "Survivor.Tick.Monsters",
            "Survivor.Tick.Projectiles", "Survivor.Collision",
        };

        /// CSV column prefixes of the markers ("step" -> step_msPerStep, step_ms), same order as MarkerNames.
        public static readonly IReadOnlyList<string> MarkerColumns = new[]
        {
            "step", "pool", "targeting", "spatialIndex", "tickMonsters", "tickProjectiles", "collision",
        };

        private const double NanosecondsPerMs = 1e6;

        private readonly List<MeasurementFrame> _frames = new List<MeasurementFrame>(4096);
        private readonly ProfilerRecorder[] _markers = new ProfilerRecorder[MarkerCount];
        private ProfilerRecorder _gc, _used, _reserved;
        private bool _recording;
        private int _n;
        private bool _hasPending;
        private bool _pendingInWindow;
        private int _pendingSteps, _pendingLive, _pendingGems;

        /// Target monster count of the session.
        public int MonsterCountN => _n;

        /// First step at which at least N monsters were alive (-1 before).
        public long NReachedStep { get; private set; } = -1;

        /// Clock step at the start of the first kept frame (-1 before).
        public long WindowStartStep { get; private set; } = -1;

        /// Frames kept so far (the measured window), oldest first.
        public IReadOnlyList<MeasurementFrame> Frames => _frames;

        /// Starts the recorders and clears the previous session; n must be positive.
        public void Begin(int n)
        {
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n), n, "Monster count N must be positive.");
            }

            StopRecorders();
            _frames.Clear();
            _n = n;
            NReachedStep = -1;
            WindowStartStep = -1;
            _hasPending = false;
            for (int i = 0; i < MarkerCount; i++)
            {
                _markers[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, MarkerNames[i]);
            }

            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _used = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _reserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _recording = true;
        }

        /// Once per Update: completes the previous frame (unscaledDeltaTime of this Update is its duration, the recorders'
        /// LastValue its totals), records s_N, then holds this frame's host values until the next call.
        public void Observe(long clockStep, int frameSteps, int liveMonsters, int activeGems, float unscaledDeltaTime)
        {
            if (!_recording)
            {
                return;
            }

            if (_hasPending && _pendingInWindow)
            {
                var markerMs = new double[MarkerCount];
                for (int i = 0; i < MarkerCount; i++)
                {
                    markerMs[i] = Read(_markers[i]) / NanosecondsPerMs;
                }

                _frames.Add(new MeasurementFrame(_frames.Count, unscaledDeltaTime * 1000d, _pendingSteps, Read(_gc), Read(_used),
                    Read(_reserved), _pendingLive, _pendingGems, markerMs));
            }

            if (NReachedStep < 0 && liveMonsters >= _n)
            {
                NReachedStep = clockStep;
            }

            long frameStartStep = clockStep - frameSteps;
            _hasPending = true;
            _pendingInWindow = NReachedStep >= 0 && frameStartStep >= NReachedStep + GameRules.MeasurementWarmupSteps;
            if (_pendingInWindow && WindowStartStep < 0)
            {
                WindowStartStep = frameStartStep;
            }

            _pendingSteps = frameSteps;
            _pendingLive = liveMonsters;
            _pendingGems = activeGems;
        }

        /// Aggregates the kept frames (MeasurementStats.FromFrames).
        public MeasurementStats Snapshot()
        {
            return MeasurementStats.FromFrames(_frames);
        }

        /// Stops the recorders; the kept frames stay readable.
        public void End()
        {
            StopRecorders();
            _hasPending = false;
        }

        /// Stops the recorders.
        public void Dispose()
        {
            StopRecorders();
        }

        // NaN when the counter is unavailable (written as NA, 5.15.3); batchmode may report valid recorders with 0.
        private static double Read(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : double.NaN;
        }

        private void StopRecorders()
        {
            if (!_recording)
            {
                return;
            }

            for (int i = 0; i < MarkerCount; i++)
            {
                _markers[i].Dispose();
            }

            _gc.Dispose();
            _used.Dispose();
            _reserved.Dispose();
            _recording = false;
        }
    }
}
