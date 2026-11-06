using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Last driver: closes Tick.Projectiles, Collision, Resolve, Lifecycle, then SimulationClock.EndStep (5.3; End has no handlers).
    /// Does nothing when no step is active.
    [DefaultExecutionOrder(1000)]
    public sealed class SimulationDriverLate : MonoBehaviour
    {
        private SimulationClock _clock;
        private SimulationPipeline _pipeline;
        private SimulationRules _rules;

        /// Binds the loop objects; rules.StopAtStep is read at EndStep. Null arguments throw ArgumentNullException.
        public void Initialize(SimulationClock clock, SimulationPipeline pipeline, SimulationRules rules)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        private void FixedUpdate()
        {
            if (_clock == null || !_clock.StepActive)
            {
                return;
            }

            StepContext context = _clock.CurrentContext;
            SimulationPhase phase = SimulationPhase.Collision;
            try
            {
                _clock.Markers.EndProjectiles();

                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, context);

                phase = SimulationPhase.Resolve;
                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, context);

                phase = SimulationPhase.Lifecycle;
                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, context);

                phase = SimulationPhase.End;
                _clock.EndStep(_rules.StopAtStep);
                _clock.Markers.EndStep();
            }
            catch (Exception exception)
            {
                LoopLog.PhaseError(phase, context.Step, exception);
                _clock.AbortStep();
            }
        }
    }
}
