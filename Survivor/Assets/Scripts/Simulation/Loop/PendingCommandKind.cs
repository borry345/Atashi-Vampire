namespace Survivor.Simulation
{
    /// Kind of a between-steps command applied by PendingCommandQueue in the next Begin (5.15 list order).
    /// SetLoadout, SetHealth, AddXp, LevelUpNow and SetSpawnsEnabled are applied by handlers their owners register; the rest are built in.
    public enum PendingCommandKind
    {
        SetLoadout = 0, SetHealth = 1, AddXp = 2, LevelUpNow = 3, SpawnMonstersNow = 4, SpawnBossNow = 5,
        SpawnChestNow = 6, SwapData = 7, SwitchTargetProvider = 8, SetDespawnDistance = 9, SetSpawnsEnabled = 10
    }
}
