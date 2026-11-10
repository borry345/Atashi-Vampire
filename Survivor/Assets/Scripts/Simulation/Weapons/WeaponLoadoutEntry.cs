namespace Survivor.Simulation
{
    /// One slot of a dev loadout (SetLoadout payload element): weapon id and 1-based level, or a null id for an empty slot.
    public readonly struct WeaponLoadoutEntry
    {
        /// Weapon definition id; null means "this slot is empty".
        public readonly string WeaponId;

        /// 1-based level; ignored when WeaponId is null.
        public readonly int Level;

        /// Creates an entry for weaponId at the 1-based level.
        public WeaponLoadoutEntry(string weaponId, int level)
        {
            WeaponId = weaponId;
            Level = level;
        }
    }
}
