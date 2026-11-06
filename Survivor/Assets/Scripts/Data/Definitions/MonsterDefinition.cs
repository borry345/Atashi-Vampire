using System.Collections.Generic;

namespace Survivor.Data
{
    /// One monster from monsters.json.
    public sealed class MonsterDefinition
    {
        /// Unique monster id (e.g. "M1").
        public string Id { get; internal set; }

        /// Korean display name.
        public string DisplayName { get; internal set; }

        /// Behavior type.
        public MonsterBehavior Behavior { get; internal set; }

        /// Visual key.
        public string AssetKey { get; internal set; }

        /// Maximum health (> 0).
        public float MaxHealth { get; internal set; }

        /// Move speed in units per second as written in the file.
        public float MoveSpeed { get; internal set; }

        /// Move speed in units per step (R3).
        public float MoveSpeedPerStep { get; internal set; }

        /// Body radius in world units.
        public float Radius { get; internal set; }

        /// Damage dealt by body contact (>= 0).
        public float ContactDamage { get; internal set; }

        /// True when the monster is "large" (R25: radius above the threshold or boss).
        public bool IsLarge { get; internal set; }

        /// Charge parameters; non-null only for Charger.
        public ChargeDefinition Charge { get; internal set; }

        /// Ranged attack parameters; non-null only for Ranged.
        public RangedDefinition Ranged { get; internal set; }

        /// Boss patterns in file order; empty for non-boss monsters.
        public IReadOnlyList<BossPatternDefinition> BossPatterns { get; internal set; }
    }
}
