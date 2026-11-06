using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Collects deferred commands during a step and applies them in Lifecycle order 0, sorted by (Source, Sequence) (5.4, R10).
    /// Each command kind is applied by the handler its owner registered; an unregistered kind is a contract violation.
    public sealed class SimulationCommandBuffer : ISimulationCommands, IStepPhaseHandler
    {
        private const int KindCount = (int)SimulationCommandKind.SpawnChest + 1;

        private static readonly Comparison<SimulationCommand> CompareCommands =
            (a, b) => SimulationCommand.Compare(a, b);

        private readonly ISimulationCommandHandler[] _handlers = new ISimulationCommandHandler[KindCount];
        private readonly Dictionary<EntityHandle, int> _nextSequence = new Dictionary<EntityHandle, int>();
        private readonly HashSet<EntityHandle> _despawnTargets = new HashSet<EntityHandle>();
        private List<SimulationCommand> _queued = new List<SimulationCommand>();
        private List<SimulationCommand> _applying = new List<SimulationCommand>();
        private bool _isApplying;

        /// Commands waiting for the next Lifecycle phase.
        public int PendingCount => _queued.Count;

        /// Registers the handler of kind; throws InvalidOperationException when kind already has one, ArgumentNullException for null.
        public void RegisterHandler(SimulationCommandKind kind, ISimulationCommandHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            int k = KindIndex(kind);
            if (_handlers[k] != null)
            {
                throw new InvalidOperationException("SimulationCommandBuffer: a handler for " + kind + " is already registered.");
            }

            _handlers[k] = handler;
        }

        /// True when kind has a registered handler.
        public bool HasHandler(SimulationCommandKind kind)
        {
            return _handlers[KindIndex(kind)] != null;
        }

        /// Queues command with the next per-source Sequence of this step. Commands queued while the buffer is applying
        /// (from inside a handler) wait for the next step's Lifecycle.
        public void Enqueue(in SimulationCommand command)
        {
            KindIndex(command.Kind);
            _nextSequence.TryGetValue(command.Source, out int sequence);
            _nextSequence[command.Source] = sequence + 1;
            _queued.Add(command.WithSequence(sequence));
        }

        /// Lifecycle: sorts this step's commands by (Source, Sequence) and hands each to its kind's handler; other phases do nothing.
        /// A second Despawn of the same target in one batch is ignored. Throws InvalidOperationException, before applying anything,
        /// when a queued kind has no handler.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Lifecycle || _queued.Count == 0)
            {
                return;
            }

            if (_isApplying)
            {
                throw new InvalidOperationException("SimulationCommandBuffer.OnPhase is not reentrant.");
            }

            for (int i = 0; i < _queued.Count; i++)
            {
                SimulationCommandKind kind = _queued[i].Kind;
                if (_handlers[(int)kind] == null)
                {
                    throw new InvalidOperationException("SimulationCommandBuffer: no handler is registered for " + kind + ".");
                }
            }

            List<SimulationCommand> batch = _queued;
            _queued = _applying;
            _applying = batch;
            _nextSequence.Clear();
            _despawnTargets.Clear();
            _isApplying = true;
            try
            {
                batch.Sort(CompareCommands);
                for (int i = 0; i < batch.Count; i++)
                {
                    SimulationCommand command = batch[i];
                    if (command.Kind == SimulationCommandKind.Despawn && !_despawnTargets.Add(command.Target))
                    {
                        continue;
                    }

                    _handlers[(int)command.Kind].Handle(command, context);
                }
            }
            finally
            {
                batch.Clear();
                _despawnTargets.Clear();
                _isApplying = false;
            }
        }

        /// Run reset (TAC-F8-2 step 4): drops every queued command and the per-source sequence counters; handlers stay registered.
        public void Clear()
        {
            _queued.Clear();
            _nextSequence.Clear();
        }

        private static int KindIndex(SimulationCommandKind kind)
        {
            int k = (int)kind;
            if (k < 0 || k >= KindCount)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown simulation command kind.");
            }

            return k;
        }
    }
}
