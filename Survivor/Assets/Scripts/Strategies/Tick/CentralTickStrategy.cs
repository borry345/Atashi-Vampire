using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Tick
{
    /// F17 B: one list per tick group, ticked in registration order from the drivers' RunGroup (TAC-F17-1).
    /// Unregistering nulls the slot (compacted by the next RunGroup, before and after iterating); registering during RunGroup is
    /// deferred until it ends.
    public sealed class CentralTickStrategy : ITickStrategy
    {
        private const int GroupCount = (int)TickGroup.Projectiles + 1;

        private readonly List<IUpdatable>[] _lists = new List<IUpdatable>[GroupCount];
        private readonly List<IUpdatable>[] _pending = new List<IUpdatable>[GroupCount];
        private readonly bool[] _hasHoles = new bool[GroupCount];
        private readonly int[] _counts = new int[GroupCount];
        private readonly Dictionary<IUpdatable, int> _slots = new Dictionary<IUpdatable, int>();
        private readonly Dictionary<IUpdatable, TickGroup> _groups = new Dictionary<IUpdatable, TickGroup>();
        private int _iteratingGroup = -1;

        /// Creates an empty strategy.
        public CentralTickStrategy()
        {
            for (int g = 0; g < GroupCount; g++)
            {
                _lists[g] = new List<IUpdatable>();
                _pending[g] = new List<IUpdatable>();
            }
        }

        /// Always CentralTick.
        public TickStrategyKind Kind => TickStrategyKind.CentralTick;

        /// Appends updatable to its group (host is not used). Throws ArgumentNullException for null, InvalidOperationException when already registered.
        public void Register(IUpdatable updatable, GameObject host)
        {
            if (updatable == null)
            {
                throw new ArgumentNullException(nameof(updatable));
            }

            if (_groups.ContainsKey(updatable))
            {
                throw new InvalidOperationException("CentralTickStrategy: updatable " + updatable.Id + " is already registered.");
            }

            TickGroup group = updatable.Group;
            int g = GroupIndex(group);
            _groups.Add(updatable, group);
            _counts[g]++;
            if (_iteratingGroup == g)
            {
                _pending[g].Add(updatable);
                return;
            }

            _slots.Add(updatable, _lists[g].Count);
            _lists[g].Add(updatable);
        }

        /// Removes updatable; safe during RunGroup (it is not ticked again). Throws InvalidOperationException when it is not registered.
        public void Unregister(IUpdatable updatable)
        {
            if (updatable == null)
            {
                throw new ArgumentNullException(nameof(updatable));
            }

            if (!_groups.TryGetValue(updatable, out TickGroup group))
            {
                throw new InvalidOperationException("CentralTickStrategy: updatable " + updatable.Id + " is not registered.");
            }

            int g = (int)group;
            _groups.Remove(updatable);
            _counts[g]--;
            if (_slots.TryGetValue(updatable, out int slot))
            {
                _slots.Remove(updatable);
                // Compaction is deferred to the next RunGroup so a burst of despawns stays O(n) instead of O(n^2).
                _lists[g][slot] = null;
                _hasHoles[g] = true;
            }
            else
            {
                _pending[g].Remove(updatable);
            }
        }

        /// Ticks every updatable of group in registration order; throws InvalidOperationException when called re-entrantly.
        public void RunGroup(TickGroup group, in StepContext context)
        {
            int g = GroupIndex(group);
            if (_iteratingGroup != -1)
            {
                throw new InvalidOperationException("CentralTickStrategy.RunGroup is not reentrant.");
            }

            Compact(g);
            List<IUpdatable> list = _lists[g];
            _iteratingGroup = g;
            try
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    IUpdatable updatable = list[i];
                    if (updatable != null)
                    {
                        updatable.Tick(context);
                    }
                }
            }
            finally
            {
                _iteratingGroup = -1;
                Compact(g);
                AppendPending(g);
            }
        }

        /// Number of registered updatables in group (including ones registered during the current RunGroup).
        public int RegisteredCount(TickGroup group)
        {
            return _counts[GroupIndex(group)];
        }

        /// Removes every registration (run reset).
        public void Clear()
        {
            for (int g = 0; g < GroupCount; g++)
            {
                _lists[g].Clear();
                _pending[g].Clear();
                _hasHoles[g] = false;
                _counts[g] = 0;
            }

            _slots.Clear();
            _groups.Clear();
        }

        // Removes null slots keeping the relative order, so the tick order stays the registration order.
        private void Compact(int g)
        {
            if (!_hasHoles[g])
            {
                return;
            }

            List<IUpdatable> list = _lists[g];
            int write = 0;
            for (int read = 0; read < list.Count; read++)
            {
                IUpdatable updatable = list[read];
                if (updatable == null)
                {
                    continue;
                }

                if (write != read)
                {
                    list[write] = updatable;
                    _slots[updatable] = write;
                }

                write++;
            }

            list.RemoveRange(write, list.Count - write);
            _hasHoles[g] = false;
        }

        private void AppendPending(int g)
        {
            List<IUpdatable> pending = _pending[g];
            for (int i = 0; i < pending.Count; i++)
            {
                _slots.Add(pending[i], _lists[g].Count);
                _lists[g].Add(pending[i]);
            }

            pending.Clear();
        }

        private static int GroupIndex(TickGroup group)
        {
            int g = (int)group;
            if (g < 0 || g >= GroupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown tick group.");
            }

            return g;
        }
    }
}
