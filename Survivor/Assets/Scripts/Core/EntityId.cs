using System;
using System.Globalization;

namespace Survivor.Core
{
    /// Identity of a simulation entity; Index is reused, Generation increments on each free.
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        /// 1-based slot index. 0 means None.
        public readonly int Index;

        /// Generation of the slot; starts at 1 for every index.
        public readonly int Generation;

        /// Creates an id from an index and a generation.
        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        /// The invalid id (default value).
        public static readonly EntityId None = default;

        /// True when Index > 0.
        public bool IsValid => Index > 0;

        /// Index in the high 32 bits and Generation in the low 32 bits.
        public long Packed => ((long)Index << 32) | (uint)Generation;

        /// Orders by Index, then Generation (rule R10).
        public int CompareTo(EntityId other)
        {
            int byIndex = Index.CompareTo(other.Index);
            return byIndex != 0 ? byIndex : Generation.CompareTo(other.Generation);
        }

        /// True when both Index and Generation match.
        public bool Equals(EntityId other) => Index == other.Index && Generation == other.Generation;

        /// True when obj is an EntityId with the same Index and Generation.
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        /// Hash of the packed value.
        public override int GetHashCode() => Packed.GetHashCode();

        /// Formats as "{Index}:{Generation}", e.g. "17:3" (rule R15).
        public override string ToString()
        {
            return Index.ToString(CultureInfo.InvariantCulture) + ":" + Generation.ToString(CultureInfo.InvariantCulture);
        }

        /// True when a orders before b.
        public static bool operator <(EntityId a, EntityId b) => a.CompareTo(b) < 0;

        /// True when a orders after b.
        public static bool operator >(EntityId a, EntityId b) => a.CompareTo(b) > 0;

        /// True when both ids are equal.
        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);

        /// True when the ids differ.
        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
    }
}
