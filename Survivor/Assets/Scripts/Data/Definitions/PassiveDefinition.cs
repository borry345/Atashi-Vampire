using System.Collections.Generic;

namespace Survivor.Data
{
    /// One passive from passives.json; Levels[i].Bonus is the cumulative absolute bonus at level i+1.
    public sealed class PassiveDefinition
    {
        /// Unique passive id (e.g. "P1").
        public string Id { get; internal set; }

        /// Korean display name.
        public string DisplayName { get; internal set; }

        /// Icon key for the choice UI.
        public string IconKey { get; internal set; }

        /// Stat the passive modifies.
        public PassiveStat Stat { get; internal set; }

        /// Exactly five levels.
        public IReadOnlyList<PassiveLevelDefinition> Levels { get; internal set; }
    }
}
