using System;
using Survivor.Core;

namespace Survivor.Dev
{
    /// Everything one measurement file holds (5.15.3): the header values and the aggregated body (Stats). Filled by
    /// MeasurementSession from the run; tests fill it in memory.
    public sealed class MeasurementRecord
    {
        /// Session end time (UTC); also names the files.
        public DateTime TimestampUtc;

        /// Preset label and group.
        public string Label = string.Empty, Group = string.Empty;

        /// "Development" or "Release", and Application.unityVersion.
        public string BuildType = string.Empty, UnityVersion = string.Empty;

        /// PC specs.
        public string Cpu = string.Empty, Gpu = string.Empty, Os = string.Empty;

        /// System memory in MB.
        public int RamMB;

        /// Measurement environment (TAC-F19-9).
        public int VSyncCount, TargetFrameRate;

        /// "Mono" or "IL2CPP".
        public string ScriptingBackend = string.Empty;

        /// GarbageCollector.isIncremental.
        public bool IncrementalGC;

        /// Strategies of the measured run (pool, target, tick, collision, ccdMode, cellSize).
        public StrategySelection Selection;

        /// Monster count N and whether the 400 cap was lifted.
        public int MonsterCountN;

        /// True when the monster cap was lifted (N > 400).
        public bool MonsterCapDisabled;

        /// D used and its R26 formula text.
        public float DespawnDistance;

        /// "45" or "45*sqrt(N/400)".
        public string DespawnFormula = string.Empty;

        /// Master seed of the run.
        public ulong Seed;

        /// Segment HP multipliers ("1.0;1.5;2.2;3.0", MeasurementCsvExporter.FormatHpMultipliers).
        public string HpMultipliers = string.Empty;

        /// Measured steps requested after the warmup.
        public long DurationSteps;

        /// s_N, or -1 when N was never reached (written as NA).
        public long NReachedStep = -1;

        /// Determinism and fixed conditions.
        public bool PlayerInvulnerable, SpawnStopHoldN, LevelUpEnabled;

        /// "W1:5;W2:5;W3:5;W4:5".
        public string WeaponLoadout = string.Empty;

        /// Chest drop chance during the run.
        public float ChestDropChance;

        /// Auto path settings.
        public float AutoPathRadius, AutoPathSpeed;

        /// Time scale at the end of the session.
        public float TimeScale = 1f;

        /// Exclusion conditions at the end of the session.
        public bool OverlayHidden, VisualizersOff, CollisionLogOff;

        /// False for an invalidated session, with the cause in InvalidReason ("" when valid).
        public bool Valid;

        /// Cause of an invalid session ("" when valid).
        public string InvalidReason = string.Empty;

        /// Aggregated body values.
        public MeasurementStats Stats = new MeasurementStats();
    }
}
