using UnityEngine;

namespace Survivor.Core
{
    /// Read-only per-step view handed to every phase handler and IUpdatable.
    public readonly struct StepContext
    {
        /// Index of the executing step (completed steps before it).
        public readonly long Step;

        /// Player position after PlayerMove of this step (Begin/PlayerMove see the previous position).
        public readonly Vector2 PlayerPosition;

        /// Runtime-tunable rules.
        public readonly SimulationRules Rules;

        /// Nearest-target queries; valid from BuildIndex onward.
        public readonly ITargetProvider Targets;

        /// Deferred command sink.
        public readonly ISimulationCommands Commands;

        /// Creates a context.
        public StepContext(long step, Vector2 playerPosition, SimulationRules rules,
                           ITargetProvider targets, ISimulationCommands commands)
        {
            Step = step;
            PlayerPosition = playerPosition;
            Rules = rules;
            Targets = targets;
            Commands = commands;
        }
    }
}
