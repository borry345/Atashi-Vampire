namespace Survivor.Simulation
{
    /// How a run ended (5.17); None while the run goes on.
    public enum RunOutcomeKind { None, Death, Clear }

    /// Result of a finished run as the result screen and the best record read it (5.17, TAC-F8-3).
    public readonly struct RunOutcome
    {
        /// No outcome yet.
        public static readonly RunOutcome None = default;

        /// Death or Clear; None while the run goes on.
        public readonly RunOutcomeKind Kind;

        /// Completed steps including the step the run ended in (Q12).
        public readonly long SurvivalSteps;

        /// Kills of the run (KillPipeline.KillCount, R24).
        public readonly int Kills;

        /// Player level when the run ended.
        public readonly int Level;

        /// Creates an outcome from its kind, survival steps, kill count and level.
        public RunOutcome(RunOutcomeKind kind, long survivalSteps, int kills, int level)
        {
            Kind = kind;
            SurvivalSteps = survivalSteps;
            Kills = kills;
            Level = level;
        }
    }
}
