using System;

namespace Survivor.Data
{
    /// Experience curve rule R21 (Data owns it; ExperienceSystem subtracts and carries over).
    public static class XpCurve
    {
        /// Experience needed to go from level to level+1: Thresholds[level-1], or last + GrowthAfterTable * (levels past the table). Saturates at int.MaxValue; throws for level < 1 or null def.
        public static int RequiredForNext(int level, XpCurveDefinition def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be at least 1.");
            }

            int count = def.Thresholds.Count;
            if (level <= count)
            {
                return def.Thresholds[level - 1];
            }

            long required = def.Thresholds[count - 1] + (long)def.GrowthAfterTable * (level - count);
            return required > int.MaxValue ? int.MaxValue : (int)required;
        }
    }
}
