namespace Survivor.Data
{
    /// Charger (M2) parameters from monsters.json "charge".
    public sealed class ChargeDefinition
    {
        /// Distance to the player that triggers a charge, in world units.
        public float TriggerDistance { get; internal set; }

        /// Charge speed in units per second as written in the file.
        public float ChargeSpeed { get; internal set; }

        /// Charge speed in units per step (R3).
        public float ChargeSpeedPerStep { get; internal set; }

        /// Charge duration in seconds as written in the file.
        public float DurationSeconds { get; internal set; }

        /// Charge duration in steps, at least 1.
        public int DurationSteps { get; internal set; }

        /// Cooldown after a charge in seconds as written in the file.
        public float CooldownSeconds { get; internal set; }

        /// Cooldown after a charge in steps, at least 1.
        public int CooldownSteps { get; internal set; }
    }
}
