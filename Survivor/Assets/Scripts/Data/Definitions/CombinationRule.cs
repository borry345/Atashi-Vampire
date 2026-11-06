namespace Survivor.Data
{
    /// One evolution rule from combinations.json: max-level WeaponId + owned PassiveId -> ResultWeaponId.
    public sealed class CombinationRule
    {
        /// Unique rule id.
        public string Id { get; internal set; }

        /// Base weapon id (not evolvedOnly).
        public string WeaponId { get; internal set; }

        /// Required passive id.
        public string PassiveId { get; internal set; }

        /// Resulting evolved weapon id (evolvedOnly).
        public string ResultWeaponId { get; internal set; }
    }
}
