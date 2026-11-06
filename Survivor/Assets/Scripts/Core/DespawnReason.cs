namespace Survivor.Core
{
    /// Why an entity was released; only Killed counts as a kill (rule R24).
    public enum DespawnReason : byte { Killed = 0, OutOfRange = 1, LifetimeExpired = 2, Consumed = 3, Replaced = 4, RunReset = 5 }
}
