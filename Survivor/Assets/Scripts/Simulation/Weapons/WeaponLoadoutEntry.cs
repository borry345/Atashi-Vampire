namespace Survivor.Simulation
{
    /// One weapon of a dev loadout (SetLoadout payload element): weapon id and 1-based level.
    public readonly struct WeaponLoadoutEntry
    {
        /// Weapon definition id.
        public readonly string WeaponId;

        /// 1-based weapon level.
        public readonly int Level;

        /// Creates an entry for weaponId at the 1-based level.
        public WeaponLoadoutEntry(string weaponId, int level)
        {
            WeaponId = weaponId;
            Level = level;
        }
    }
}
