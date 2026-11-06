namespace Survivor.Core
{
    /// Phases of one fixed step, in execution order.
    public enum SimulationPhase
    {
        Begin = 0, PlayerMove = 1, MonsterTick = 2, BuildIndex = 3, WeaponTick = 4,
        ProjectileTick = 5, Collision = 6, Resolve = 7, Lifecycle = 8, End = 9
    }
}
