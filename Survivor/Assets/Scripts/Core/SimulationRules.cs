namespace Survivor.Core
{
    /// Runtime-tunable values read by gameplay; defaults equal normal play. Dev mode only mutates these.
    public sealed class SimulationRules
    {
        /// Monster cap excluding the boss (400); dev may set int.MaxValue.
        public int MonsterCap = GameRules.MonsterCapDefault;

        /// Despawn distance, from spawnTable.json; dev overrides via R26.
        public float DespawnDistance;

        /// True when dev overrode DespawnDistance; hot reload keeps the override.
        public bool DespawnDistanceOverridden;

        /// False makes the player invulnerable (dev).
        public bool PlayerTakesDamage = true;

        /// False stops every weapon from firing (dev).
        public bool WeaponsEnabled = true;

        /// False disables level-up choices (dev).
        public bool LevelUpEnabled = true;

        /// Step at which the run closes with StopAtStep.
        public long StopAtStep = long.MaxValue;

        /// False when dev mode is attached (SPEC F8).
        public bool RecordBestTime = true;

        /// False during measurements (SPEC F8, F19).
        public bool AutoPauseOnFocusLoss = true;

        /// Chest drop chance (0.02); measurement presets set 0 (SPEC F10).
        public float ChestDropChance = GameRules.ChestDropChance;
    }
}
