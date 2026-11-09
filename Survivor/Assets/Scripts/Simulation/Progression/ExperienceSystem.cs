using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Player level and experience toward the next level (5.14, TAC-F5-4). AddXp only accumulates; the level grows only through
    /// TryBeginLevelUp, which StepOutcomeGate calls once per step and the flow calls again after each applied choice.
    public sealed class ExperienceSystem
    {
        private readonly Func<GameData> _currentData;

        /// Creates the system at level 1; currentData returns GameDataRegistry.Current (the curve is read on every use, 5c.3 #10).
        /// Null throws ArgumentNullException.
        public ExperienceSystem(Func<GameData> currentData)
        {
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
            Reset();
        }

        /// Current level, starting at 1.
        public int Level { get; private set; }

        /// Experience collected toward the next level (carried over after each level-up).
        public int Xp { get; private set; }

        /// Adds amount (>= 0, else ArgumentOutOfRangeException) without levelling up; saturates at int.MaxValue.
        public void AddXp(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience amount must not be negative.");
            }

            long sum = (long)Xp + amount;
            Xp = sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// If Xp >= XpCurve.RequiredForNext(Level, curve): subtracts it, Level + 1 and returns true. At most one level per call,
        /// so several levels of stored experience open one round at a time (TAC-F5-4). Null curve throws ArgumentNullException.
        public bool TryBeginLevelUp(XpCurveDefinition curve)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }

            int required = XpCurve.RequiredForNext(Level, curve);
            if (Xp < required)
            {
                return false;
            }

            Xp -= required;
            Level++;
            return true;
        }

        /// Run reset: level 1, no experience.
        public void Reset()
        {
            Level = 1;
            Xp = 0;
        }

        /// PendingCommandQueue handler for AddXp (registered by the composition root): AddXp(command.Count).
        public void HandleAddXp(PendingCommand command, StepContext context)
        {
            AddXp(command.Count);
        }

        /// PendingCommandQueue handler for LevelUpNow (registered by the composition root): adds exactly what is missing for the
        /// next level of the current curve (nothing when Xp already suffices); the gate of the same step levels up (5.15 [default]).
        public void HandleLevelUpNow(PendingCommand command, StepContext context)
        {
            int required = XpCurve.RequiredForNext(Level, CurrentCurve());
            if (Xp < required)
            {
                AddXp(required - Xp);
            }
        }

        /// Experience curve of the current data; InvalidOperationException when no data or player stats are loaded.
        private XpCurveDefinition CurrentCurve()
        {
            GameData data = _currentData() ?? throw new InvalidOperationException("ExperienceSystem: no game data is loaded.");
            PlayerStatsDefinition stats = data.PlayerStats ?? throw new InvalidOperationException("ExperienceSystem: no player stats are loaded.");
            return stats.XpCurve ?? throw new InvalidOperationException("ExperienceSystem: the player stats have no xp curve.");
        }
    }
}
