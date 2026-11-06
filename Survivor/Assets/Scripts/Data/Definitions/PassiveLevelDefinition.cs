namespace Survivor.Data
{
    /// One passive level (passives.json "levels[]").
    public sealed class PassiveLevelDefinition
    {
        /// Cumulative absolute bonus ratio at this level (0.10 = +10%, >= 0); applied by StatMath (R13).
        public float Bonus { get; internal set; }
    }
}
