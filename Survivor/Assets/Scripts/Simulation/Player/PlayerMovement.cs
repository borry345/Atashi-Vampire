using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Begin order 10: reads IInputSource.ReadMove once per step and caches it; PlayerMove order 0: moves the player by
    /// direction x speed per step. Speed = MoveSpeedPerStep x (1 + P2 bonus) (R13), or SpeedOverride (units per second) converted
    /// with StepMath when >= 0 (TAC-F1-3). No bounds or correction of any kind (TAC-F1-4).
    public sealed class PlayerMovement : IStepPhaseHandler
    {
        private readonly PlayerEntity _player;
        private readonly PassiveInventory _passives;
        private readonly Func<GameData> _currentData;
        private IInputSource _input;
        private MoveCommand _cached;
        private long _cachedStep = -1;

        /// Creates the movement over player, input (keyboard or dev override), passives and current data; null throws.
        public PlayerMovement(PlayerEntity player, IInputSource input, PassiveInventory passives, Func<GameData> currentData)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
        }

        /// Input source in use.
        public IInputSource Input => _input;

        /// Run start only: binds the input source (IDevModeHost.InputOverride or the keyboard, 5.15); null throws.
        public void SetInputSource(IInputSource input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _cachedStep = -1;
        }

        /// Begin: caches this step's input; PlayerMove: applies it. Other phases do nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase == SimulationPhase.Begin)
            {
                // One read per step: the keyboard latch is shared by every step of a frame (U17).
                _cached = _input.ReadMove(context.Step, _player.Position);
                _cachedStep = context.Step;
            }
            else if (phase == SimulationPhase.PlayerMove)
            {
                if (_cachedStep != context.Step)
                {
                    return;
                }

                MoveCommand move = _cached;
                if (move.Direction.x == 0f && move.Direction.y == 0f)
                {
                    return;
                }

                _player.MoveTo(_player.Position + move.Direction * SpeedPerStep(move));
            }
        }

        private float SpeedPerStep(in MoveCommand move)
        {
            if (move.SpeedOverride >= 0f)
            {
                return StepMath.PerSecondToPerStep(move.SpeedOverride);
            }

            PlayerStatsDefinition stats = _currentData()?.PlayerStats;
            if (stats == null)
            {
                throw new InvalidOperationException("PlayerMovement: no player stats are loaded.");
            }

            return StatMath.ApplyMultiplier(stats.MoveSpeedPerStep, _passives.BonusOf(PassiveStat.MoveSpeed));
        }
    }
}
