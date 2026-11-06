using UnityEngine;

namespace Survivor.Core
{
    /// Upgrade eligibility and heal rules shared by chests, level-up and dev tools (R22, R23).
    public static class ProgressionRules
    {
        /// R22: a weapon can be upgraded when level < levelCount and it is not evolved-only.
        public static bool IsWeaponUpgradable(int level, int levelCount, bool evolvedOnly)
        {
            return level < levelCount && !evolvedOnly;
        }

        /// R22: a passive can be upgraded when level < levelCount.
        public static bool IsPassiveUpgradable(int level, int levelCount)
        {
            return level < levelCount;
        }

        /// R23: min(maxHp, hp + maxHp * HealRatio); max HP itself never changes.
        public static float ApplyHeal(float hp, float maxHp)
        {
            return Mathf.Min(maxHp, hp + maxHp * GameRules.HealRatio);
        }
    }
}
