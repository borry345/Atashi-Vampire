using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Payload of the Begin-phase SwitchTargetProvider pending command. Built by App (IMP-FLOW), applied by PendingCommandQueue (5.7).
    public readonly struct SwitchTargetProviderCommand
    {
        /// New provider instance (Core interface), created by App's StrategyFactory.
        public readonly ITargetProvider Provider;

        /// App closure run right after the slot swap: recomputes MonsterSpatialIndex.Required, then PhysicsBodySynchronizer.ApplyRequirements (R19).
        public readonly Action ApplyRequirements;

        /// Creates the command; null arguments throw ArgumentNullException.
        public SwitchTargetProviderCommand(ITargetProvider provider, Action applyRequirements)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            ApplyRequirements = applyRequirements ?? throw new ArgumentNullException(nameof(applyRequirements));
        }
    }
}
