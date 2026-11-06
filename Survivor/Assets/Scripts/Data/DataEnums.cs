namespace Survivor.Data
{
    /// The seven data files under StreamingAssets/Data (5.13 table).
    public enum DataFileKind { Weapons, Monsters, Combinations, SpawnTable, Passives, LevelUpPool, PlayerStats }

    /// Severity of a validation issue; Blocking prevents starting a run.
    public enum IssueSeverity { Warning, Blocking }
}
