namespace Survivor.Data
{
    /// One weighted monster entry of a spawn segment.
    public sealed class SpawnEntryDefinition
    {
        /// Referenced monster id (valid, non-boss).
        public string MonsterId { get; internal set; }

        /// Selection weight (> 0).
        public float Weight { get; internal set; }
    }
}
