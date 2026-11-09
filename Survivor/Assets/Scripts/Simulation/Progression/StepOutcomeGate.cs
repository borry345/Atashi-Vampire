using System;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Lifecycle order 90: decides death > clear > level-up once per step and closes the gate after the step (TAC-F8-8, TAC-F5-8).
    /// Death = health.IsDead; clear = the boss was killed this step (any step, 5.14 rev 8); level-up = neither of them,
    /// Rules.LevelUpEnabled and one ExperienceSystem.TryBeginLevelUp. On death or clear no level-up is attempted (the stored
    /// experience is dropped with the run).
    public sealed class StepOutcomeGate : IStepPhaseHandler
    {
        private readonly SimulationClock _clock;
        private readonly PlayerHealth _health;
        private readonly KillPipeline _killPipeline;
        private readonly ExperienceSystem _experience;
        private readonly Func<GameData> _currentData;

        /// Creates the gate over the clock, player health, kill pipeline, experience and current data (GameDataRegistry.Current;
        /// the xp curve is read on every level-up check, 5c.3 #10). Null arguments throw ArgumentNullException.
        public StepOutcomeGate(SimulationClock clock, PlayerHealth health, KillPipeline killPipeline, ExperienceSystem experience,
                               Func<GameData> currentData)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _killPipeline = killPipeline ?? throw new ArgumentNullException(nameof(killPipeline));
            _experience = experience ?? throw new ArgumentNullException(nameof(experience));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
        }

        /// Death/Clear outcome of the latest evaluated step (SurvivalSteps = context.Step + 1); None otherwise.
        public RunOutcome PendingOutcome { get; private set; }

        /// Lifecycle: evaluates this step once; other phases do nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Lifecycle)
            {
                return;
            }

            PendingOutcome = RunOutcome.None;
            if (_health.IsDead)
            {
                End(RunOutcomeKind.Death, GateCloseReason.Death, context);
                return;
            }

            if (_killPipeline.BossKilledThisStep)
            {
                End(RunOutcomeKind.Clear, GateCloseReason.Clear, context);
                return;
            }

            if (context.Rules.LevelUpEnabled && _experience.TryBeginLevelUp(Curve()))
            {
                _clock.CloseAfterCurrentStep(GateCloseReason.LevelUp);
            }
        }

        /// Run reset: forgets the pending outcome.
        public void Reset()
        {
            PendingOutcome = RunOutcome.None;
        }

        private XpCurveDefinition Curve()
        {
            GameData data = _currentData() ?? throw new InvalidOperationException("StepOutcomeGate: no game data is loaded.");
            PlayerStatsDefinition stats = data.PlayerStats ?? throw new InvalidOperationException("StepOutcomeGate: no player stats are loaded.");
            return stats.XpCurve;
        }

        private void End(RunOutcomeKind kind, GateCloseReason reason, in StepContext context)
        {
            PendingOutcome = new RunOutcome(kind, context.Step + 1, _killPipeline.KillCount, _experience.Level);
            _clock.CloseAfterCurrentStep(reason);
            LoopLog.Info("Run ended: " + kind + " after " + PendingOutcome.SurvivalSteps.ToString(CultureInfo.InvariantCulture)
                + " steps, kills " + PendingOutcome.Kills.ToString(CultureInfo.InvariantCulture) + ", level "
                + PendingOutcome.Level.ToString(CultureInfo.InvariantCulture) + ".");
        }
    }
}
