using System.Collections.Generic;

namespace Survivor.Data
{
    /// One time segment of the spawn table: [StartStep, EndStep) with a spawn rate and weighted monster entries.
    public sealed class SpawnSegmentDefinition
    {
        /// Segment start in seconds as written in the file (inclusive).
        public float StartSeconds { get; internal set; }

        /// Segment start in steps (R1 SecondsToSteps, 0 allowed).
        public int StartStep { get; internal set; }

        /// Segment end in seconds (exclusive), after clamping to 600.
        public float EndSeconds { get; internal set; }

        /// Segment end in steps (exclusive), after clamping to GameRules.BossSpawnStep.
        public int EndStep { get; internal set; }

        /// Monsters spawned per second (> 0).
        public float SpawnsPerSecond { get; internal set; }

        /// Health multiplier for non-boss monsters spawned in this segment (finite, > 0; 1.0 when omitted or invalid).
        public float HpMultiplier { get; internal set; } = 1f;

        /// Valid weighted entries in file order (non-boss monsters only).
        public IReadOnlyList<SpawnEntryDefinition> Entries { get; internal set; }
    }
}
