namespace Survivor.Data
{
    /// Projectile fired by a ranged monster (monsters.json "ranged.projectile").
    public sealed class EnemyProjectileDefinition
    {
        /// Visual key.
        public string AssetKey { get; internal set; }

        /// Speed in units per second as written in the file.
        public float Speed { get; internal set; }

        /// Speed in units per step (R3).
        public float SpeedPerStep { get; internal set; }

        /// Collision radius in world units.
        public float Radius { get; internal set; }

        /// Damage dealt to the player.
        public float Damage { get; internal set; }

        /// Lifetime in seconds as written in the file.
        public float LifetimeSeconds { get; internal set; }

        /// Lifetime in steps, at least 1.
        public int LifetimeSteps { get; internal set; }
    }
}
