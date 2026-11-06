using System;

namespace Survivor.Core
{
    /// Passive bonus application (rule R13).
    public static class StatMath
    {
        /// Attack speed: max(1, round(baseSteps / (1 + bonus))) with MidpointRounding.AwayFromZero. Throws if bonus <= -1 or baseSteps < 0.
        public static int ApplyAttackSpeed(int baseSteps, float bonus)
        {
            if (baseSteps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseSteps), baseSteps, "baseSteps must be non-negative.");
            }

            if (!(bonus > -1f) || float.IsInfinity(bonus))
            {
                throw new ArgumentOutOfRangeException(nameof(bonus), bonus, "bonus must be finite and greater than -1.");
            }

            double steps = Math.Round(baseSteps / (1.0 + bonus), MidpointRounding.AwayFromZero);
            return Math.Max(1, (int)steps);
        }

        /// Move speed / pickup range: baseValue * (1 + bonus).
        public static float ApplyMultiplier(float baseValue, float bonus)
        {
            return baseValue * (1f + bonus);
        }
    }
}
