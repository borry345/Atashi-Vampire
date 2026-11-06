namespace Survivor.Data
{
    /// Gem and chest parameters (playerStats.json "pickups").
    public sealed class PickupsDefinition
    {
        /// Gem visual key.
        public string GemAssetKey { get; internal set; }

        /// Gem collision radius in world units.
        public float GemRadius { get; internal set; }

        /// Experience granted per gem (>= 1).
        public int GemXpValue { get; internal set; }

        /// Attracted gem speed in units per second as written in the file.
        public float GemAttractSpeed { get; internal set; }

        /// Attracted gem speed in units per step (R3).
        public float GemAttractSpeedPerStep { get; internal set; }

        /// Chest visual key.
        public string ChestAssetKey { get; internal set; }

        /// Chest collision radius in world units.
        public float ChestRadius { get; internal set; }
    }
}
