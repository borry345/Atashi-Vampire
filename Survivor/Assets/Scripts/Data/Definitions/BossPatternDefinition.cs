namespace Survivor.Data
{
    /// One boss pattern (monsters.json "bossPatterns[]"). Fields of the other pattern kind are 0 / null.
    public sealed class BossPatternDefinition
    {
        /// Pattern kind.
        public BossPatternKind Kind { get; internal set; }

        /// Pattern interval in seconds as written in the file.
        public float CooldownSeconds { get; internal set; }

        /// Pattern interval in steps, at least 1.
        public int CooldownSteps { get; internal set; }

        /// Hazard zone radius in world units (HazardZone).
        public float ZoneRadius { get; internal set; }

        /// Hazard zone duration in seconds as written in the file (HazardZone).
        public float DurationSeconds { get; internal set; }

        /// Hazard zone duration in steps, at least 1 (HazardZone).
        public int DurationSteps { get; internal set; }

        /// Damage dealt to the player by the hazard zone (HazardZone).
        public float Damage { get; internal set; }

        /// Visual key of the hazard zone (HazardZone).
        public string AssetKey { get; internal set; }

        /// Hazard zone placement, default Player (HazardZone).
        public ZonePlacement Placement { get; internal set; }

        /// Id of the summoned non-boss monster (Summon).
        public string MonsterId { get; internal set; }

        /// Monsters summoned per activation (Summon).
        public int Count { get; internal set; }

        /// Radius of the summon circle around the boss in world units (Summon).
        public float SummonRadius { get; internal set; }
    }
}
