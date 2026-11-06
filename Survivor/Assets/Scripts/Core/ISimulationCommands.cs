namespace Survivor.Core
{
    /// Deferred, order-independent side effects. Applied in Lifecycle order 0, sorted by (Source, Sequence).
    public interface ISimulationCommands
    {
        /// Queues a command for this step's Lifecycle phase.
        void Enqueue(in SimulationCommand command);
    }
}
