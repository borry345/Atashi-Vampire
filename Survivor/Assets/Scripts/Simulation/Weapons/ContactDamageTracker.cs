using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Attacker-side per-target cooldown for persistent hitters (W2/E2 orbs, W3 zones). One per weapon instance (5.12, TAC-F6-6).
    /// Records are keyed by EntityHandle (generation included) and kept in ascending id order (R10, TAC-0-10).
    public sealed class ContactDamageTracker
    {
        private struct Entry
        {
            public EntityHandle Target;
            public int OverlapCount;
            public long NextAllowed;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _intervalSteps;

        /// Creates a tracker hitting each overlapped target at most once per hitIntervalSteps (>= 1, else ArgumentOutOfRangeException).
        public ContactDamageTracker(int hitIntervalSteps)
        {
            SetInterval(hitIntervalSteps);
        }

        /// Current hit interval in steps.
        public int IntervalSteps => _intervalSteps;

        /// Number of targets with an overlap or a pending cooldown record.
        public int TrackedCount => _entries.Count;

        /// Overlap begins (overlapCount++); the next CollectHits hits it at once when its cooldown allows.
        public void OnEnter(EntityHandle target, long step)
        {
            int index = Find(target);
            if (index < 0)
            {
                index = ~index;
                _entries.Insert(index, new Entry { Target = target, OverlapCount = 0, NextAllowed = long.MinValue });
            }

            Entry entry = _entries[index];
            entry.OverlapCount++;
            _entries[index] = entry;
        }

        /// Overlap ends (overlapCount--); the cooldown record is kept so re-entering cannot bypass it (SPEC F6).
        public void OnExit(EntityHandle target)
        {
            int index = Find(target);
            if (index < 0)
            {
                return;
            }

            Entry entry = _entries[index];
            if (entry.OverlapCount > 0)
            {
                entry.OverlapCount--;
            }

            _entries[index] = entry;
        }

        /// Appends targets to hit this step in ascending Id order and advances their nextAllowed to step + interval.
        public void CollectHits(long step, List<EntityHandle> hitsOut)
        {
            if (hitsOut == null)
            {
                throw new ArgumentNullException(nameof(hitsOut));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (step < entry.NextAllowed)
                {
                    continue;
                }

                if (entry.OverlapCount == 0)
                {
                    // An expired record of a target that left is the same as no record; dropping it bounds memory.
                    _entries.RemoveAt(i);
                    i--;
                    continue;
                }

                hitsOut.Add(entry.Target);
                entry.NextAllowed = step + _intervalSteps;
                _entries[i] = entry;
            }
        }

        /// Drops every record of target (EntityLifecycle.Despawned subscriber; unregistering emits no Exit, TAC-0-10).
        public void Forget(EntityHandle target)
        {
            int index = Find(target);
            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }
        }

        /// Changes the interval for the next computation only; existing nextAllowed records are kept (TAC-F2-3).
        public void SetInterval(int hitIntervalSteps)
        {
            if (hitIntervalSteps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hitIntervalSteps), hitIntervalSteps, "Hit interval must be at least 1 step.");
            }

            _intervalSteps = hitIntervalSteps;
        }

        /// True with the next allowed hit step of target when a record exists.
        public bool TryGetNextAllowed(EntityHandle target, out long nextAllowedStep)
        {
            int index = Find(target);
            if (index < 0)
            {
                nextAllowedStep = 0;
                return false;
            }

            nextAllowedStep = _entries[index].NextAllowed;
            return true;
        }

        /// Removes every record.
        public void Clear()
        {
            _entries.Clear();
        }

        // Binary search over the ascending list; returns the index or the bitwise complement of the insertion point.
        private int Find(EntityHandle target)
        {
            int low = 0;
            int high = _entries.Count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                int c = _entries[mid].Target.CompareTo(target);
                if (c == 0)
                {
                    return mid;
                }

                if (c < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return ~low;
        }
    }
}
