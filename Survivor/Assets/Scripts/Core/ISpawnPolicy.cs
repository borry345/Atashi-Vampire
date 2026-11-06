using System.Collections.Generic;

namespace Survivor.Core
{
    /// Decides which monsters to spawn each step; normal: TableSpawnPolicy (IMP-MONSTER), dev: DevSpawnPolicy.
    public interface ISpawnPolicy
    {
        /// Fills spawn requests for this step; MonsterSpawner applies cap and placement.
        void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut);

        /// True when the boss should spawn at this step.
        bool BossSpawnDue(long step);
    }
}
