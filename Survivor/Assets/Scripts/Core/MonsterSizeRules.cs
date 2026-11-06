namespace Survivor.Core
{
    /// Large-monster classification (rule R25).
    public static class MonsterSizeRules
    {
        /// True when radius > LargeMonsterRadiusThreshold or the monster is the boss (bosses are always large).
        public static bool IsLarge(float radius, bool isBoss)
        {
            return radius > GameRules.LargeMonsterRadiusThreshold || isBoss;
        }
    }
}
