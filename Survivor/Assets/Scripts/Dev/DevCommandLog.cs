using System;

namespace Survivor.Dev
{
    /// Fixed-size ring of the latest command-log lines (5.15.5, SPEC F19 32/74/75): lives as long as the DevController (a run
    /// reset never clears it) and is never exported. Pre-allocated, so Add allocates nothing beyond the caller's string.
    public sealed class DevCommandLog
    {
        /// Maximum number of lines kept.
        public const int Capacity = 10;

        private readonly string[] _ring = new string[Capacity];
        private int _next;

        /// Number of lines kept, in [0, Capacity].
        public int Count { get; private set; }

        /// 0 = newest line, Count - 1 = oldest kept; outside [0, Count) throws ArgumentOutOfRangeException.
        public string NewestFirst(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "DevCommandLog index must be in [0, " + Count + ").");
            }

            return _ring[(_next - 1 - index + Capacity) % Capacity];
        }

        /// Appends line as the newest one; when full, the oldest line is dropped. Null throws ArgumentNullException.
        public void Add(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            _ring[_next] = line;
            _next = (_next + 1) % Capacity;
            if (Count < Capacity)
            {
                Count++;
            }
        }
    }
}
