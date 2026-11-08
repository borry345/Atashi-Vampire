using Survivor.Data;

namespace Survivor.Simulation
{
    /// One owned passive: the definition captured when acquired (kept across hot reload, 5.14) and the 1-based level.
    public sealed class PassiveInstance
    {
        internal PassiveInstance(PassiveDefinition definition, int level)
        {
            Definition = definition;
            Level = level;
        }

        /// Definition captured when the passive was acquired.
        public PassiveDefinition Definition { get; }

        /// Passive id (Definition.Id).
        public string Id => Definition.Id;

        /// 1-based level.
        public int Level { get; internal set; }

        /// Cumulative absolute bonus of the current level (levels[Level - 1].Bonus, R13).
        public float Bonus => Definition.Levels[Level - 1].Bonus;
    }
}
