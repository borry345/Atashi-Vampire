namespace Survivor.Core
{
    /// Canonical order of level-up candidates (rule R12).
    public static class UpgradeOrdering
    {
        /// UpgradeKind value ascending, then TargetId by string.CompareOrdinal. Usable as a List.Sort comparison.
        public static int Compare(UpgradeOption a, UpgradeOption b)
        {
            int byKind = ((int)a.Kind).CompareTo((int)b.Kind);
            return byKind != 0 ? byKind : string.CompareOrdinal(a.TargetId, b.TargetId);
        }
    }
}
