namespace Survivor.Data
{
    /// Ranged (M3) parameters from monsters.json "ranged".
    public sealed class RangedDefinition
    {
        /// Distance at which the monster stops and fires, in world units.
        public float AttackRange { get; internal set; }

        /// Fire interval in seconds as written in the file.
        public float FireCooldownSeconds { get; internal set; }

        /// Fire interval in steps, at least 1.
        public int FireCooldownSteps { get; internal set; }

        /// Projectile fired by the monster.
        public EnemyProjectileDefinition Projectile { get; internal set; }
    }
}
