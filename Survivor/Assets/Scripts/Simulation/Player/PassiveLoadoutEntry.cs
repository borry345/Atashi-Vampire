namespace Survivor.Simulation
{
    /// One slot of a dev passive loadout (SetPassiveLoadout payload element): passive id and 1-based level, or a null id for an
    /// empty slot.
    public readonly struct PassiveLoadoutEntry
    {
        /// Passive definition id; null means "this slot is empty".
        public readonly string PassiveId;

        /// 1-based level; ignored when PassiveId is null.
        public readonly int Level;

        /// Creates an entry for passiveId at the 1-based level.
        public PassiveLoadoutEntry(string passiveId, int level)
        {
            PassiveId = passiveId;
            Level = level;
        }
    }
}
