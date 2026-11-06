namespace Survivor.Core
{
    /// Collision layer of a body; Unity layer index = 8 + value (rule R30).
    public enum CollisionLayer : byte
    {
        PlayerBody = 0, PlayerMagnet = 1, Monster = 2, PlayerProjectile = 3, PlayerOrb = 4,
        PlayerZone = 5, EnemyProjectile = 6, EnemyZone = 7, Gem = 8, Chest = 9
    }

    /// The nine layer pairs that produce events; the first layer in each name is the A side (rule R5).
    public enum CollisionChannel : byte
    {
        PlayerProjectileVsMonster = 0, PlayerOrbVsMonster = 1, PlayerZoneVsMonster = 2,
        PlayerBodyVsMonster = 3, EnemyProjectileVsPlayerBody = 4, EnemyZoneVsPlayerBody = 5,
        PlayerMagnetVsGem = 6, PlayerBodyVsGem = 7, PlayerBodyVsChest = 8
    }

    /// Transition reported for a pair.
    public enum CollisionEventKind : byte { Enter = 0, Exit = 1 }
}
