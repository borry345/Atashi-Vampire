using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Handler of SimulationCommandKind.Despawn: EntityLifecycle.Despawn(Target, Reason); an already-gone target is ignored.
    public sealed class EntityDespawnCommandHandler : ISimulationCommandHandler
    {
        private readonly EntityLifecycle _lifecycle;

        /// Creates the handler over lifecycle; null throws ArgumentNullException.
        public EntityDespawnCommandHandler(EntityLifecycle lifecycle)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        /// Despawns command.Target with command.Reason.
        public void Handle(in SimulationCommand command, in StepContext context)
        {
            _lifecycle.Despawn(command.Target, command.Reason);
        }
    }
}
