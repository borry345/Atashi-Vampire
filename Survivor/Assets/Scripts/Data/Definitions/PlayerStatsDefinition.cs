namespace Survivor.Data
{
    /// Player base stats from playerStats.json (single definition; any error blocks the run).
    public sealed class PlayerStatsDefinition
    {
        /// Maximum health (> 0).
        public float MaxHealth { get; internal set; }

        /// Move speed in units per second as written in the file.
        public float MoveSpeed { get; internal set; }

        /// Move speed in units per step (R3), before the P2 bonus.
        public float MoveSpeedPerStep { get; internal set; }

        /// Body radius in world units.
        public float Radius { get; internal set; }

        /// Magnet radius in world units, before the P3 bonus.
        public float PickupRadius { get; internal set; }

        /// Shared invulnerability time in seconds as written in the file.
        public float InvulnerabilitySeconds { get; internal set; }

        /// Shared invulnerability time in steps (R1 SecondsToSteps, 0 allowed; TAC-F11-8).
        public int InvulnerabilitySteps { get; internal set; }

        /// Starting weapon id (valid, not evolvedOnly).
        public string StartingWeaponId { get; internal set; }

        /// Experience curve (R21).
        public XpCurveDefinition XpCurve { get; internal set; }

        /// Gem and chest parameters.
        public PickupsDefinition Pickups { get; internal set; }
    }
}
