using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Ordered registry of phase handlers; same order for every strategy combination (5c.3 #9).
    public sealed class SimulationPipeline
    {
        private const int PhaseCount = (int)SimulationPhase.End + 1;

        private readonly List<int>[] _orders = new List<int>[PhaseCount];
        private readonly List<IStepPhaseHandler>[] _handlers = new List<IStepPhaseHandler>[PhaseCount];

        /// Creates an empty pipeline.
        public SimulationPipeline()
        {
            for (int i = 0; i < PhaseCount; i++)
            {
                _orders[i] = new List<int>();
                _handlers[i] = new List<IStepPhaseHandler>();
            }
        }

        /// Adds handler to phase at order (ascending order runs first). Throws InvalidOperationException when the
        /// (phase, order) slot is taken, ArgumentNullException for a null handler, ArgumentOutOfRangeException for an unknown phase.
        public void AddHandler(SimulationPhase phase, int order, IStepPhaseHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            int p = PhaseIndex(phase);
            List<int> orders = _orders[p];
            int index = orders.BinarySearch(order);
            if (index >= 0)
            {
                throw new InvalidOperationException(
                    "SimulationPipeline.AddHandler: phase " + phase + " already has a handler at order " + order + ".");
            }

            index = ~index;
            orders.Insert(index, order);
            _handlers[p].Insert(index, handler);
        }

        /// Number of handlers registered for phase.
        public int HandlerCount(SimulationPhase phase)
        {
            return _handlers[PhaseIndex(phase)].Count;
        }

        /// Runs every handler of phase in ascending order; exceptions propagate to the driver.
        public void RunPhase(SimulationPhase phase, in StepContext context)
        {
            List<IStepPhaseHandler> handlers = _handlers[PhaseIndex(phase)];
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].OnPhase(phase, context);
            }
        }

        private static int PhaseIndex(SimulationPhase phase)
        {
            int p = (int)phase;
            if (p < 0 || p >= PhaseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown simulation phase.");
            }

            return p;
        }
    }
}
