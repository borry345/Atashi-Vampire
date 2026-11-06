using Survivor.Core;

namespace Survivor.Data
{
    /// One weight rule of levelUpPool.json "rules[]".
    public sealed class LevelUpRuleDefinition
    {
        /// Target id that matches every candidate of the kind.
        public const string AnyTarget = "*";

        /// Candidate kind (WeaponNew / WeaponUpgrade / PassiveNew / PassiveUpgrade; never Heal).
        public UpgradeKind Kind { get; internal set; }

        /// Weapon/passive id, or AnyTarget.
        public string TargetId { get; internal set; }

        /// Weight (>= 0).
        public float Weight { get; internal set; }
    }
}
