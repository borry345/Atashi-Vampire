namespace Survivor.Core
{
    /// Kind of a level-up / chest option; the value order is the canonical candidate order (rule R12).
    public enum UpgradeKind { WeaponUpgrade = 0, WeaponNew = 1, PassiveUpgrade = 2, PassiveNew = 3, Heal = 4 }

    /// One level-up option: what kind, which weapon/passive id, and whether it is a filler entry.
    public readonly struct UpgradeOption
    {
        /// Kind of the option.
        public readonly UpgradeKind Kind;

        /// Weapon or passive id the option targets.
        public readonly string TargetId;

        /// True for an entry that only fills an empty slot in the choice list.
        public readonly bool IsFiller;

        /// Creates an option.
        public UpgradeOption(UpgradeKind kind, string targetId, bool isFiller = false)
        {
            Kind = kind;
            TargetId = targetId;
            IsFiller = isFiller;
        }
    }
}
