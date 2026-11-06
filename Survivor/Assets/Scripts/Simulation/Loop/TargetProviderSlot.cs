using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Wraps the current target provider and is what StepContext.Targets exposes (5.7). Not a phase handler:
    /// SimulationDriverMid calls OnSnapshotBuilt right after the BuildIndex handlers.
    public sealed class TargetProviderSlot : ITargetProvider
    {
        private readonly IStepClockReader _clock;
        private ITargetProvider _current;

        /// Creates an empty slot guarded by clock; null clock throws ArgumentNullException.
        public TargetProviderSlot(IStepClockReader clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// Provider currently wrapped, or null.
        public ITargetProvider Current => _current;

        /// Kind of the wrapped provider; throws InvalidOperationException when the slot is empty.
        public TargetProviderKind Kind
        {
            get
            {
                if (_current == null)
                {
                    throw new InvalidOperationException("TargetProviderSlot is empty.");
                }

                return _current.Kind;
            }
        }

        /// Swaps the provider (null empties the slot). Allowed in Begin (SwitchTargetProvider pending command) and outside a step
        /// (run reset); throws InvalidOperationException in any other phase of an active step (TAC-F16-5).
        public void Replace(ITargetProvider next)
        {
            if (_clock.StepActive && _clock.CurrentPhase != SimulationPhase.Begin)
            {
                throw new InvalidOperationException("TargetProviderSlot.Replace is only allowed in Begin or at a run reset (phase "
                    + _clock.CurrentPhase + ").");
            }

            if (ReferenceEquals(next, _current))
            {
                return;
            }

            _current = next;
            if (next != null)
            {
                LoopLog.Info("Target provider switched to " + next.Kind + ".");
            }
        }

        /// Forwards the frozen snapshot to the wrapped provider.
        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
            _current?.OnSnapshotBuilt(snapshot);
        }

        /// Nearest monster through the wrapped provider; false when the slot is empty. Never throws by itself.
        public bool TryFindNearest(Vector2 point, out TargetHit hit)
        {
            if (_current == null)
            {
                hit = default;
                return false;
            }

            return _current.TryFindNearest(point, out hit);
        }
    }
}
