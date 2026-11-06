namespace Survivor.Simulation
{
    /// Why the run gate was closed last; the first close inside a step wins.
    public enum GateCloseReason { None, Paused, LevelUp, Death, Clear, StopAtStep, SingleStepDone, RunReset, Error }
}
