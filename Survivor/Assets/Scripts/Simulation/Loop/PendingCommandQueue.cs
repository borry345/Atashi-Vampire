using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Begin order 0: applies the commands queued outside the step (Update) in the order they were queued (TAC-0-1).
    /// Spawn requests become SimulationCommands (Source = None) for this step's Lifecycle; player/flow kinds go to registered handlers.
    public sealed class PendingCommandQueue : IStepPhaseHandler
    {
        private const int KindCount = (int)PendingCommandKind.SetPassiveLoadout + 1;

        private readonly TargetProviderSlot _targets;
        private readonly GameDataRegistry _data;
        private readonly SimulationRules _rules;
        private readonly Action<PendingCommand, StepContext>[] _handlers = new Action<PendingCommand, StepContext>[KindCount];
        private List<PendingCommand> _queued = new List<PendingCommand>();
        private List<PendingCommand> _applying = new List<PendingCommand>();

        /// Creates the queue over the target slot, the data registry and the runtime rules; null arguments throw ArgumentNullException.
        public PendingCommandQueue(TargetProviderSlot targets, GameDataRegistry data, SimulationRules rules)
        {
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// Read by SpawnBossNow in Begin (MonsterSpawner.BossSpawnedThisRun, wired by the composition root); null means "not spawned".
        public Func<bool> BossSpawnedThisRun { get; set; }

        /// Commands waiting for the next Begin.
        public int PendingCount => _queued.Count;

        /// True for the kinds whose handler an owner registers (SetLoadout, SetHealth, AddXp, LevelUpNow, SetSpawnsEnabled,
        /// SetPassiveLoadout).
        public static bool IsOwnerHandled(PendingCommandKind kind)
        {
            return kind == PendingCommandKind.SetLoadout || kind == PendingCommandKind.SetHealth
                || kind == PendingCommandKind.AddXp || kind == PendingCommandKind.LevelUpNow
                || kind == PendingCommandKind.SetSpawnsEnabled || kind == PendingCommandKind.SetPassiveLoadout;
        }

        /// Registers the handler of an owner-handled kind. Throws ArgumentException for a built-in kind, InvalidOperationException
        /// when the kind already has a handler, ArgumentNullException for null.
        public void RegisterHandler(PendingCommandKind kind, Action<PendingCommand, StepContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!IsOwnerHandled(kind))
            {
                throw new ArgumentException("PendingCommandQueue applies " + kind + " itself; only SetLoadout, SetHealth, AddXp, LevelUpNow, SetSpawnsEnabled and SetPassiveLoadout take handlers.",
                    nameof(kind));
            }

            if (_handlers[(int)kind] != null)
            {
                throw new InvalidOperationException("PendingCommandQueue: a handler for " + kind + " is already registered.");
            }

            _handlers[(int)kind] = handler;
        }

        /// Queues command for the next Begin. Throws InvalidOperationException for an owner-handled kind without a registered handler.
        public void Enqueue(in PendingCommand command)
        {
            int k = (int)command.Kind;
            if (k < 0 || k >= KindCount)
            {
                throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Unknown pending command kind.");
            }

            if (IsOwnerHandled(command.Kind) && _handlers[k] == null)
            {
                throw new InvalidOperationException("PendingCommandQueue: no handler is registered for " + command.Kind + ".");
            }

            _queued.Add(command);
        }

        /// Queues a target provider switch for the next Begin (5.7).
        public void Enqueue(in SwitchTargetProviderCommand command)
        {
            Enqueue(PendingCommand.SwitchTargetProvider(command));
        }

        /// Begin: applies every queued command in queue order; other phases do nothing. Commands queued while applying wait for the next Begin.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Begin || _queued.Count == 0)
            {
                return;
            }

            List<PendingCommand> batch = _queued;
            _queued = _applying;
            _applying = batch;
            try
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    Apply(batch[i], context);
                }
            }
            finally
            {
                batch.Clear();
            }
        }

        /// Run reset (TAC-F8-2 step 4): drops every queued command; handlers stay registered.
        public void Clear()
        {
            _queued.Clear();
        }

        private void Apply(in PendingCommand command, in StepContext context)
        {
            switch (command.Kind)
            {
                case PendingCommandKind.SwitchTargetProvider:
                    _targets.Replace(command.TargetSwitch.Provider);
                    command.TargetSwitch.ApplyRequirements();
                    break;
                case PendingCommandKind.SwapData:
                    if (!_data.TrySwap(command.Data, command.Report))
                    {
                        LoopLog.Warning("SwapData refused: the new data is not playable (" + command.Report.BlockingCount
                            + " blocking issues); keeping the current data.");
                    }

                    break;
                case PendingCommandKind.SpawnMonstersNow:
                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None,
                        position: command.Position, radius: command.Radius, definitionId: command.DefinitionId,
                        count: command.Count, ignoreCap: command.IgnoreCap));
                    break;
                case PendingCommandKind.SpawnBossNow:
                    if (BossSpawnedThisRun != null && BossSpawnedThisRun())
                    {
                        LoopLog.Info("SpawnBossNow refused: the boss already spawned this run.");
                        break;
                    }

                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None,
                        position: command.Position, definitionId: command.DefinitionId, count: 1, ignoreCap: true));
                    break;
                case PendingCommandKind.SpawnChestNow:
                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None,
                        position: command.Position));
                    break;
                case PendingCommandKind.SetDespawnDistance:
                    _rules.DespawnDistance = command.Value;
                    _rules.DespawnDistanceOverridden = true;
                    break;
                default:
                    Action<PendingCommand, StepContext> handler = _handlers[(int)command.Kind];
                    if (handler == null)
                    {
                        throw new InvalidOperationException("PendingCommandQueue: no handler is registered for " + command.Kind + ".");
                    }

                    handler(command, context);
                    break;
            }
        }
    }
}
