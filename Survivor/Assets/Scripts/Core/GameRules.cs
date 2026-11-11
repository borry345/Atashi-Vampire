namespace Survivor.Core
{
    /// Rule constants shared by every layer (rule R20). Values are fixed for this wave.
    public static class GameRules
    {
        /// Radius of the off-screen spawn circle around the player, in world units.
        public const float SpawnRadius = 20f;

        /// Default despawn distance from the player, in world units (N <= 400).
        public const float DefaultDespawnDistance = 45f;

        /// Cap on simultaneously alive monsters, excluding the boss (M4). Boss arrival skips the cap check; summons are checked.
        public const int MonsterCapDefault = 400;

        /// Step at which the boss spawns (10 minutes at 60 steps per second).
        public const long BossSpawnStep = 36000;

        /// Probability that a killed monster drops a chest.
        public const float ChestDropChance = 0.02f;

        /// Probability that a chest without an evolution candidate upgrades a weapon or passive (otherwise heals).
        public const float ChestUpgradeChance = 0.70f;

        /// Fraction of max HP restored by a heal.
        public const float HealRatio = 0.30f;

        /// Maximum number of weapon slots.
        public const int MaxWeaponSlots = 4;

        /// Maximum number of passive slots.
        public const int MaxPassiveSlots = 3;

        /// Camera orthographic size; one world unit is 1/18 of the screen height.
        public const float CameraOrthographicSize = 9f;

        /// Monsters with a radius above this value are "large" and kept out of the spatial hash (default value).
        public const float LargeMonsterRadiusThreshold = 1.0f;

        /// Lowest allowed dev time scale.
        public const float MinTimeScale = 0.25f;

        /// Highest allowed dev time scale.
        public const float MaxTimeScale = 3f;

        /// Steps excluded from the start of a measurement session.
        public const int MeasurementWarmupSteps = 300;

        /// FPS threshold used to find the monster count limit in measurements.
        public const float MonsterLimitFpsThreshold = 55f;

        /// Display-only radius scale of body-type entities (monsters, projectiles, pickups, player); never read by hit tests.
        public const float BodyVisualScale = 1.25f;
    }
}
