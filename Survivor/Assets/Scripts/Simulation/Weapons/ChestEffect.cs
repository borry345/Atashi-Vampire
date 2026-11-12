namespace Survivor.Simulation
{
    /// Outcome of one chest (TAC-F4-4): evolution (1), random weapon or passive upgrade (2, 70%) or heal (2, 30% or nothing
    /// upgradable). PassiveUpgraded is appended last so the existing values keep their numbers (TAC-F4-10).
    public enum ChestEffect { Evolved, WeaponUpgraded, Healed, PassiveUpgraded }
}
