using System;
using System.Globalization;

namespace Survivor.Core
{
    /// One Enter/Exit transition of a pair. A is always the body on the channel's A-side layer.
    public readonly struct CollisionEvent : IComparable<CollisionEvent>
    {
        /// Step in which the transition was detected.
        public readonly long Step;

        /// Enter or Exit.
        public readonly CollisionEventKind Kind;

        /// Channel of the pair.
        public readonly CollisionChannel Channel;

        /// Body on the channel's A-side layer.
        public readonly EntityHandle A;

        /// Body on the channel's B-side layer.
        public readonly EntityHandle B;

        /// Creates an event.
        public CollisionEvent(long step, CollisionEventKind kind, CollisionChannel channel, EntityHandle a, EntityHandle b)
        {
            Step = step;
            Kind = kind;
            Channel = channel;
            A = a;
            B = b;
        }

        /// (rev 4) Step, Channel, A, B, Kind - the single sort/dispatch/comparison key (TAC-F18-8).
        public int CompareTo(CollisionEvent other)
        {
            int c = Step.CompareTo(other.Step);
            if (c != 0)
            {
                return c;
            }

            c = ((byte)Channel).CompareTo((byte)other.Channel);
            if (c != 0)
            {
                return c;
            }

            c = A.CompareTo(other.A);
            if (c != 0)
            {
                return c;
            }

            c = B.CompareTo(other.B);
            if (c != 0)
            {
                return c;
            }

            return ((byte)Kind).CompareTo((byte)other.Kind);
        }

        /// (rev 4) CSV row "step,channel,a,b,kind" (same order as CompareTo), e.g. "120,PlayerBodyVsMonster,1:1,17:3,Enter".
        public override string ToString()
        {
            return Step.ToString(CultureInfo.InvariantCulture) + "," + Channel + "," + A + "," + B + "," + Kind;
        }
    }
}
