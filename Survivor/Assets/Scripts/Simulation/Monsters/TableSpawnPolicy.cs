using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Normal-play spawn policy: the spawn table segment containing the step, SpawnScheduler counts and weighted species
    /// draws on the Spawn stream (R9). The table is read from the data registry on every call (5c.3 #10).
    public sealed class TableSpawnPolicy : ISpawnPolicy
    {
        private readonly GameDataRegistry _data;
        private readonly List<float> _weights = new List<float>();

        /// Creates the policy over the data registry; null throws ArgumentNullException.
        public TableSpawnPolicy(GameDataRegistry data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// Appends this step's monster ids in draw order. liveMonsterCount is ignored (MonsterSpawner applies the cap).
        /// Steps outside every segment (including after the last one) and a missing data set add nothing.
        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
            if (spawnRng == null)
            {
                throw new ArgumentNullException(nameof(spawnRng));
            }

            if (monsterIdsOut == null)
            {
                throw new ArgumentNullException(nameof(monsterIdsOut));
            }

            GameData data = _data.Current;
            if (data == null)
            {
                return;
            }

            SpawnSegmentDefinition segment = FindSegment(data.SpawnTable.Segments, context.Step);
            if (segment == null)
            {
                return;
            }

            int count = SpawnScheduler.SpawnsDueAtStep(segment.SpawnsPerSecond, context.Step - segment.StartStep);
            if (count == 0)
            {
                return;
            }

            IReadOnlyList<SpawnEntryDefinition> entries = segment.Entries;
            _weights.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                _weights.Add(entries[i].Weight);
            }

            for (int i = 0; i < count; i++)
            {
                int index = WeightedSampler.PickIndex(_weights, spawnRng);
                if (index < 0)
                {
                    return;
                }

                monsterIdsOut.Add(entries[index].MonsterId);
            }
        }

        /// step >= GameRules.BossSpawnStep; a second boss is prevented by MonsterSpawner's per-run flag.
        public bool BossSpawnDue(long step)
        {
            return step >= GameRules.BossSpawnStep;
        }

        private static SpawnSegmentDefinition FindSegment(IReadOnlyList<SpawnSegmentDefinition> segments, long step)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                SpawnSegmentDefinition segment = segments[i];
                if (step >= segment.StartStep && step < segment.EndStep)
                {
                    return segment;
                }
            }

            return null;
        }
    }
}
