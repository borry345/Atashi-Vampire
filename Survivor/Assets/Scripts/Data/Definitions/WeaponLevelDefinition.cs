namespace Survivor.Data
{
    /// Absolute values of one weapon level. Fields that do not apply to the behavior are 0 (or the schema default).
    /// Seconds fields keep the original file value for dumps; consumers read only the *Steps / *PerStep values (R1, R3).
    public sealed class WeaponLevelDefinition
    {
        /// Damage per hit (> 0).
        public float Damage { get; internal set; }

        /// Fire / spawn interval in seconds as written in the file (projectile, homing, zone).
        public float CooldownSeconds { get; internal set; }

        /// Fire / spawn interval in steps, at least 1 (R1 SecondsToIntervalSteps).
        public int CooldownSteps { get; internal set; }

        /// Projectiles per volley (default 1).
        public int ProjectileCount { get; internal set; }

        /// Spread between projectiles in degrees (default 0).
        public float SpreadDegrees { get; internal set; }

        /// Projectile speed in units per second as written in the file.
        public float ProjectileSpeed { get; internal set; }

        /// Projectile speed in units per step (R3).
        public float ProjectileSpeedPerStep { get; internal set; }

        /// Projectile collision radius in world units.
        public float ProjectileRadius { get; internal set; }

        /// Projectile lifetime in seconds as written in the file.
        public float LifetimeSeconds { get; internal set; }

        /// Projectile lifetime in steps, at least 1.
        public int LifetimeSteps { get; internal set; }

        /// Extra monsters a projectile may pass through (default 0).
        public int PierceCount { get; internal set; }

        /// Number of orbs (orbit).
        public int OrbCount { get; internal set; }

        /// Orb collision radius in world units (orbit).
        public float OrbRadius { get; internal set; }

        /// Distance of the orbs from the player in world units (orbit).
        public float OrbitRadius { get; internal set; }

        /// Angular speed in degrees per second as written in the file (orbit).
        public float AngularSpeedDegPerSec { get; internal set; }

        /// Angular speed in radians per step (R3, orbit).
        public float AngularSpeedRadPerStep { get; internal set; }

        /// Damage tick interval in seconds as written in the file (orbit, zone).
        public float HitIntervalSeconds { get; internal set; }

        /// Damage tick interval in steps, at least 1 (orbit, zone; TAC-F11-8).
        public int HitIntervalSteps { get; internal set; }

        /// Zone radius in world units (zone).
        public float ZoneRadius { get; internal set; }

        /// Zone duration in seconds as written in the file (zone).
        public float DurationSeconds { get; internal set; }

        /// Zone duration in steps, at least 1 (zone).
        public int DurationSteps { get; internal set; }

        /// Zone placement (zone, default NearestEnemy).
        public ZonePlacement Placement { get; internal set; }
    }
}
