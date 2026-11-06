namespace Survivor.Core
{
    /// Handler invoked by SimulationPipeline for one phase of one step. (moved to Core in rev 2 so Strategies can implement it)
    public interface IStepPhaseHandler
    {
        /// Runs this handler's work for the given phase.
        void OnPhase(SimulationPhase phase, in StepContext context);
    }
}
