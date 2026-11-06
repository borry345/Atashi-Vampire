namespace Survivor.Data
{
    /// Weapon behavior type (weapons.json "behavior"); W4 is expressed as Homing only.
    public enum WeaponBehavior { Projectile, Homing, Orbit, Zone }

    /// Where a zone is placed ("placement"): nearest enemy (default for weapons) or the player.
    public enum ZonePlacement { NearestEnemy, Player }

    /// Monster behavior type (monsters.json "behavior").
    public enum MonsterBehavior { Chaser, Charger, Ranged, Boss }

    /// Boss pattern type (bossPatterns[].type).
    public enum BossPatternKind { HazardZone, Summon }

    /// Stat a passive modifies (passives.json "stat"): P1 / P2 / P3.
    public enum PassiveStat { AttackSpeed, MoveSpeed, PickupRadius }
}
