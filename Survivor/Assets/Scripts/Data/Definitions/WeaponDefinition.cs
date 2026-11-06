using System.Collections.Generic;

namespace Survivor.Data
{
    /// One weapon from weapons.json; Levels[i] holds the absolute values of level i+1.
    public sealed class WeaponDefinition
    {
        /// Unique weapon id (e.g. "W1", "E1").
        public string Id { get; internal set; }

        /// Korean display name.
        public string DisplayName { get; internal set; }

        /// Behavior type.
        public WeaponBehavior Behavior { get; internal set; }

        /// Visual key of the projectile / orb / zone.
        public string AssetKey { get; internal set; }

        /// Icon key for the choice UI; equals AssetKey when the file omits it.
        public string IconKey { get; internal set; }

        /// True when the weapon can only be obtained by evolution (exactly one level).
        public bool EvolvedOnly { get; internal set; }

        /// Per-level values: 5 for base weapons, 1 for evolved weapons.
        public IReadOnlyList<WeaponLevelDefinition> Levels { get; internal set; }
    }
}
