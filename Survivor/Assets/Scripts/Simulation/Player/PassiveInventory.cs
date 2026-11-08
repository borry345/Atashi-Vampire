using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// The player's passive slots (GameRules.MaxPassiveSlots, fixed order). Written only by PlayerProgressionApplier (A11).
    public sealed class PassiveInventory
    {
        private readonly PassiveInstance[] _slots = new PassiveInstance[GameRules.MaxPassiveSlots];

        /// Slot contents in slot order; null for an empty slot.
        public IReadOnlyList<PassiveInstance> Slots => _slots;

        /// Number of occupied slots.
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// True when a slot holds passiveId.
        public bool Owns(string passiveId)
        {
            return Find(passiveId) != null;
        }

        /// 1-based level of passiveId, or 0 when not owned.
        public int Level(string passiveId)
        {
            PassiveInstance passive = Find(passiveId);
            return passive == null ? 0 : passive.Level;
        }

        /// Sum of the current-level bonus of every owned passive with stat; 0 when none (R13).
        public float BonusOf(PassiveStat stat)
        {
            float bonus = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].Definition.Stat == stat)
                {
                    bonus += _slots[i].Bonus;
                }
            }

            return bonus;
        }

        /// Run reset: empties every slot.
        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
        }

        internal PassiveInstance Find(string passiveId)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && string.Equals(_slots[i].Id, passiveId, StringComparison.Ordinal))
                {
                    return _slots[i];
                }
            }

            return null;
        }

        internal int FirstFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        internal void SetSlot(int index, PassiveInstance passive)
        {
            _slots[index] = passive;
        }
    }
}
