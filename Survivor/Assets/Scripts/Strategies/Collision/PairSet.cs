using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Strategies
{
    /// Overlapping pairs of the previous step plus the pairs being collected for this step; the diff of the two yields
    /// Enter (new pair) and Exit (vanished pair) events in CollisionEvent.CompareTo order (TAC-F18-2, TAC-F18-8).
    internal sealed class PairSet
    {
        private static readonly Comparison<Key> KeyComparison = (x, y) => x.CompareTo(y);

        private List<Key> _committed = new List<Key>();
        private List<Key> _building = new List<Key>();

        /// Number of pairs overlapping at the last Commit (minus pairs removed by RemoveBody).
        internal int Count => _committed.Count;

        /// Starts collecting this step's pairs.
        internal void Begin()
        {
            _building.Clear();
        }

        /// Adds an overlapping pair; a is the body on the channel's A-side layer. Duplicates are tolerated.
        internal void Add(CollisionChannel channel, EntityHandle a, EntityHandle b)
        {
            _building.Add(new Key(channel, a, b));
        }

        /// Appends the Enter/Exit events of step (previous set vs collected set) to events in CompareTo order and keeps the
        /// collected set as the new previous set.
        internal void Commit(long step, List<CollisionEvent> events)
        {
            _building.Sort(KeyComparison);
            RemoveAdjacentDuplicates(_building);

            int i = 0;
            int j = 0;
            while (i < _committed.Count || j < _building.Count)
            {
                int cmp;
                if (i == _committed.Count)
                {
                    cmp = 1;
                }
                else if (j == _building.Count)
                {
                    cmp = -1;
                }
                else
                {
                    cmp = _committed[i].CompareTo(_building[j]);
                }

                if (cmp == 0)
                {
                    i++;
                    j++;
                }
                else if (cmp < 0)
                {
                    Key gone = _committed[i++];
                    events.Add(new CollisionEvent(step, CollisionEventKind.Exit, gone.Channel, gone.A, gone.B));
                }
                else
                {
                    Key added = _building[j++];
                    events.Add(new CollisionEvent(step, CollisionEventKind.Enter, added.Channel, added.A, added.B));
                }
            }

            List<Key> swap = _committed;
            _committed = _building;
            _building = swap;
            _building.Clear();
        }

        /// Silently forgets every previous-step pair that contains id, so no Exit is reported for it (TAC-F18-5).
        internal void RemoveBody(EntityHandle id)
        {
            int write = 0;
            for (int read = 0; read < _committed.Count; read++)
            {
                Key key = _committed[read];
                if (key.A == id || key.B == id)
                {
                    continue;
                }

                _committed[write++] = key;
            }

            _committed.RemoveRange(write, _committed.Count - write);
        }

        /// Forgets every pair (run reset).
        internal void Clear()
        {
            _committed.Clear();
            _building.Clear();
        }

        private static void RemoveAdjacentDuplicates(List<Key> sorted)
        {
            if (sorted.Count < 2)
            {
                return;
            }

            int write = 1;
            for (int read = 1; read < sorted.Count; read++)
            {
                if (!sorted[read].Equals(sorted[write - 1]))
                {
                    sorted[write++] = sorted[read];
                }
            }

            sorted.RemoveRange(write, sorted.Count - write);
        }

        /// (Channel, A, B) of a pair; ordered like CollisionEvent.CompareTo without Step and Kind.
        internal readonly struct Key : IComparable<Key>, IEquatable<Key>
        {
            internal readonly CollisionChannel Channel;
            internal readonly EntityHandle A;
            internal readonly EntityHandle B;

            internal Key(CollisionChannel channel, EntityHandle a, EntityHandle b)
            {
                Channel = channel;
                A = a;
                B = b;
            }

            public int CompareTo(Key other)
            {
                int c = ((byte)Channel).CompareTo((byte)other.Channel);
                if (c != 0)
                {
                    return c;
                }

                c = A.CompareTo(other.A);
                return c != 0 ? c : B.CompareTo(other.B);
            }

            public bool Equals(Key other) => Channel == other.Channel && A == other.A && B == other.B;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => (A.GetHashCode() * 397) ^ (B.GetHashCode() * 31) ^ (int)Channel;
        }
    }
}
