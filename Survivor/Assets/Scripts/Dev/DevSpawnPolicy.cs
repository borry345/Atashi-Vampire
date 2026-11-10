using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Dev
{
    /// Dev spawn policy (5.15, SPEC F19 enemy control), bound at the next run start through IDevModeHost.SpawnPolicyOverride.
    /// Settings are fixed per instance (DevController builds a new one per change) so a bound run never changes under it:
    /// table spawns unless stopped, optionally renamed to one species, then the "hold N" top-up in the same step. Never spawns
    /// the boss on its own (BossSpawnDue is always false). "Spawn N now" is a SpawnMonstersNow command, not this policy.
    public sealed class DevSpawnPolicy : ISpawnPolicy
    {
        private readonly ISpawnPolicy _table;

        /// Creates the policy. table supplies the normal spawns (required unless stopSpawns); onlySpeciesId (null = as the
        /// table says) renames every table spawn; holdCount (0 = off, negative throws) tops live non-boss monsters up to N with
        /// holdSpeciesId (required when holdCount > 0).
        public DevSpawnPolicy(ISpawnPolicy table, bool stopSpawns, string onlySpeciesId, int holdCount, string holdSpeciesId)
        {
            if (holdCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(holdCount), holdCount, "Hold count must be zero (off) or positive.");
            }

            if (!stopSpawns && table == null)
            {
                throw new ArgumentNullException(nameof(table), "Table spawns need the table policy.");
            }

            if (holdCount > 0 && string.IsNullOrEmpty(holdSpeciesId))
            {
                throw new ArgumentException("Hold mode needs a monster id.", nameof(holdSpeciesId));
            }

            _table = table;
            StopSpawns = stopSpawns;
            OnlySpeciesId = string.IsNullOrEmpty(onlySpeciesId) ? null : onlySpeciesId;
            HoldCount = holdCount;
            HoldSpeciesId = holdCount > 0 ? holdSpeciesId : null;
        }

        /// True when the table spawns nothing.
        public bool StopSpawns { get; }

        /// Species every table spawn is replaced with; null keeps the table's species.
        public string OnlySpeciesId { get; }

        /// Live non-boss monsters kept at least this many (0 = off).
        public int HoldCount { get; }

        /// Species used for the hold top-up; null when hold is off.
        public string HoldSpeciesId { get; }

        /// Table spawns (unless stopped, renamed when OnlySpeciesId is set), then HoldCount - live - already requested
        /// copies of HoldSpeciesId. liveMonsterCount is the spawner's live non-boss count; the cap is still MonsterSpawner's.
        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
            if (monsterIdsOut == null)
            {
                throw new ArgumentNullException(nameof(monsterIdsOut));
            }

            int before = monsterIdsOut.Count;
            if (!StopSpawns)
            {
                _table.CollectSpawns(context, liveMonsterCount, spawnRng, monsterIdsOut);
                if (OnlySpeciesId != null)
                {
                    for (int i = before; i < monsterIdsOut.Count; i++)
                    {
                        monsterIdsOut[i] = OnlySpeciesId;
                    }
                }
            }

            if (HoldCount > 0)
            {
                int deficit = HoldCount - liveMonsterCount - (monsterIdsOut.Count - before);
                for (int i = 0; i < deficit; i++)
                {
                    monsterIdsOut.Add(HoldSpeciesId);
                }
            }
        }

        /// Always false: the boss comes only from a dev SpawnBossNow command.
        public bool BossSpawnDue(long step)
        {
            return false;
        }
    }
}
