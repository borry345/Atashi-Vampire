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
    }
}
