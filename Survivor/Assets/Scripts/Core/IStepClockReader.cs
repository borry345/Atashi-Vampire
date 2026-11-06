namespace Survivor.Core
{
    /// Read-only view of the step gate for code outside Simulation (self tickers, dev tools).
    public interface IStepClockReader
    {
        /// True between Begin and End of an executing step.
        bool StepActive { get; }

        /// Completed steps in the current run.
        long Step { get; }

        /// Phase currently executing.
        SimulationPhase CurrentPhase { get; }

        /// Context of the current step; valid only while StepActive and phase >= MonsterTick.
        StepContext CurrentContext { get; }
    }
}
