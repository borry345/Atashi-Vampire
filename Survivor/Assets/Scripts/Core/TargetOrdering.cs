namespace Survivor.Core
{
    /// Nearest-target tie-break shared by every target provider (rule R6).
    public static class TargetOrdering
    {
        /// True when (d2, id) beats (bestD2, bestId): smaller squared distance, then smaller EntityHandle (Index, then Generation).
        public static bool IsBetter(float d2, EntityHandle id, float bestD2, EntityHandle bestId)
        {
            if (d2 < bestD2)
            {
                return true;
            }

            return d2 == bestD2 && id.CompareTo(bestId) < 0;
        }
    }
}
