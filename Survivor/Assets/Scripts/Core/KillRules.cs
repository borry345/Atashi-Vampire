namespace Survivor.Core
{
    /// Definition of a kill (rule R24).
    public static class KillRules
    {
        /// True only for DespawnReason.Killed (bosses and summons included); out-of-range, reset, replace etc. are not kills.
        public static bool CountsAsKill(DespawnReason r)
        {
            return r == DespawnReason.Killed;
        }
    }
}
