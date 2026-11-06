namespace Survivor.Core
{
    /// Kind of a deferred simulation command; selects the Lifecycle handler.
    public enum SimulationCommandKind : byte
    {
        Despawn = 0, SpawnPlayerProjectile = 1, SpawnPlayerOrb = 2, SpawnPlayerZone = 3,
        SpawnEnemyProjectile = 4, SpawnEnemyZone = 5, SpawnMonsters = 6, SpawnGem = 7, SpawnChest = 8
    }
}
