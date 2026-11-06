using Survivor.Core;

namespace Survivor.Simulation
{
    /// Applies one kind of deferred command in Lifecycle order 0; registered on SimulationCommandBuffer by the kind's owner.
    public interface ISimulationCommandHandler
    {
        /// Applies command; called in (Source, Sequence) order (R10).
        void Handle(in SimulationCommand command, in StepContext context);
    }
}
