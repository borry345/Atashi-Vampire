using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// First driver of a step: TryBeginStep, Begin, PlayerMove, StepContext finalisation, (central) MonsterTick; opens Tick.Monsters (5.3).
    [DefaultExecutionOrder(-1000)]
    public sealed class SimulationDriverEarly : MonoBehaviour
    {
        private SimulationClock _clock;
        private SimulationPipeline _pipeline;
        private SimulationRules _rules;
        private ITargetProvider _targets;
        private ISimulationCommands _commands;
        private Func<ITickStrategy> _currentTicks;
        private Func<Vector2> _playerPosition;

        /// Binds the loop objects. currentTicks returns the run's tick strategy (e.g. () => lifecycle.Ticks) and playerPosition
        /// the authoritative player position (read before Begin and again after PlayerMove). Null arguments throw ArgumentNullException.
        public void Initialize(SimulationClock clock, SimulationPipeline pipeline, SimulationRules rules, TargetProviderSlot targets,
                               ISimulationCommands commands, Func<ITickStrategy> currentTicks, Func<Vector2> playerPosition)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _currentTicks = currentTicks ?? throw new ArgumentNullException(nameof(currentTicks));
            _playerPosition = playerPosition ?? throw new ArgumentNullException(nameof(playerPosition));
        }

        private void FixedUpdate()
        {
            if (_clock == null || !_clock.TryBeginStep(_rules.StopAtStep))
            {
                return;
            }

            long step = _clock.Step;
            SimulationPhase phase = SimulationPhase.Begin;
            try
            {
                _clock.Markers.BeginStep();

                // Begin/PlayerMove handlers see the previous player position.
                var before = new StepContext(step, _playerPosition(), _rules, _targets, _commands);
                _clock.SetContext(before);
                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, before);

                phase = SimulationPhase.PlayerMove;
                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, before);

                var context = new StepContext(step, _playerPosition(), _rules, _targets, _commands);
                _clock.SetContext(context);

                phase = SimulationPhase.MonsterTick;
                _clock.SetPhase(phase);
                _clock.Markers.BeginMonsters();
                _currentTicks().RunGroup(TickGroup.Monsters, context);
                _pipeline.RunPhase(phase, context);
            }
            catch (Exception exception)
            {
                LoopLog.PhaseError(phase, step, exception);
                _clock.AbortStep();
            }
        }
    }
}
