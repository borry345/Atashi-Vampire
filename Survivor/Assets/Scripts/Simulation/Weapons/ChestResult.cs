namespace Survivor.Simulation
{
    /// One applied chest effect (SPEC F4, TAC-F4-9). Plain values; turning them into display text is the reader's job (5.15.5).
    /// Not a state hash input (5.16).
    public readonly struct ChestResult
    {
        /// 1, 2, 3 ... for the lifetime of the log object (never reset, not even by Clear); readers use it as the de-dup key.
        public readonly long Sequence;

        /// context.Step of the Resolve that applied the effect.
        public readonly long Step;

        /// Effect kind.
        public readonly ChestEffect Kind;

        /// Evolved: the result weapon id; WeaponUpgraded: the upgraded weapon id; PassiveUpgraded: the passive id; Healed: null.
        public readonly string TargetId;

        /// WeaponUpgraded / PassiveUpgraded: level before the upgrade; Evolved / Healed: 0.
        public readonly int FromLevel;

        /// WeaponUpgraded / PassiveUpgraded: level after the upgrade; Evolved: 1 (the new instance); Healed: 0.
        public readonly int ToLevel;

        /// Healed: hp after - hp before (>= 0, 0 at full health); otherwise 0.
        public readonly float HealAmount;

        /// Creates a result.
        public ChestResult(long sequence, long step, ChestEffect kind, string targetId, int fromLevel, int toLevel, float healAmount)
        {
            Sequence = sequence;
            Step = step;
            Kind = kind;
            TargetId = targetId;
            FromLevel = fromLevel;
            ToLevel = toLevel;
            HealAmount = healAmount;
        }
    }
}
