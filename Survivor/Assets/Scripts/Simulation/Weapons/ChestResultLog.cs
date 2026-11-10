using System;

namespace Survivor.Simulation
{
    /// The latest chest results, at most Capacity, oldest dropped first (SPEC F4, TAC-F4-9). Pre-allocated ring: Add allocates
    /// nothing. Owned by PlayerProgressionApplier, written only by ChestEffectResolver; not a state hash input (5.16).
    public sealed class ChestResultLog
    {
        /// Maximum number of results kept.
        public const int Capacity = 10;

        private readonly ChestResult[] _ring = new ChestResult[Capacity];
        private int _start;

        /// Number of results kept, in [0, Capacity].
        public int Count { get; private set; }

        /// Sequence of the newest result ever added (0 before the first); Clear keeps it.
        public long LastSequence { get; private set; }

        /// 0 = oldest kept, Count - 1 = newest; outside [0, Count) throws ArgumentOutOfRangeException.
        public ChestResult this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ChestResultLog index must be in [0, " + Count + ").");
                }

                return _ring[(_start + index) % Capacity];
            }
        }

        // Appends one result with Sequence = LastSequence + 1; when full, the oldest one is dropped.
        internal void Add(long step, ChestEffect kind, string targetId, int fromLevel, int toLevel, float healAmount)
        {
            LastSequence++;
            var result = new ChestResult(LastSequence, step, kind, targetId, fromLevel, toLevel, healAmount);
            if (Count < Capacity)
            {
                _ring[(_start + Count) % Capacity] = result;
                Count++;
                return;
            }

            _ring[_start] = result;
            _start = (_start + 1) % Capacity;
        }

        // Run start only (PlayerProgressionApplier.ResetForRun): drops every result and keeps LastSequence.
        internal void Clear()
        {
            Array.Clear(_ring, 0, Capacity);
            _start = 0;
            Count = 0;
        }
    }
}
