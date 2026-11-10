using System.Collections.Generic;

namespace Survivor.Data
{
    /// The spawn table from spawnTable.json.
    public sealed class SpawnTableDefinition
    {
        /// Despawn distance from the player in world units (> spawn radius).
        public float DespawnDistance { get; internal set; }

        /// Boss monster id, or null when the file's bossMonsterId is invalid (D6: warning only, the boss never appears).
        public string BossMonsterId { get; internal set; }

        /// Valid segments in ascending, non-overlapping time order.
        public IReadOnlyList<SpawnSegmentDefinition> Segments { get; internal set; }

        /// R31: health multiplier at a step. The segment containing the step, otherwise the latest segment that
        /// started at or before it (gaps, after the last segment), otherwise 1.0. Not rounded; no allocation.
        public float HpMultiplierAt(long step)
        {
            float multiplier = 1f;
            IReadOnlyList<SpawnSegmentDefinition> segments = Segments;
            if (segments == null)
            {
                return multiplier;
            }

            // Segments are ascending and non-overlapping (binder), so the last one with StartStep <= step covers both cases.
            for (int i = 0; i < segments.Count; i++)
            {
                SpawnSegmentDefinition segment = segments[i];
                if (segment.StartStep > step)
                {
                    break;
                }

                multiplier = segment.HpMultiplier;
            }

            return multiplier;
        }
    }
}
