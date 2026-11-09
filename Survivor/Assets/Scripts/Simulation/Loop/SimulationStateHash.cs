using System;
using System.Collections.Generic;

namespace Survivor.Simulation
{
    /// FNV-1a 64 over the run state, in a fixed order (5.16): 1 step; 2 player position x/y, hp, xp, level; 3 every live entity in
    /// ascending EntityHandle order (index, generation, category, position x/y, plus hp for a Monster); 4 kill count (R24);
    /// 5 every occupied weapon slot in slot order (slot index, NextFireStep). Floats go in by bit pattern, ints and longs as
    /// little-endian bytes. Used by determinism tests and the dev N-step result.
    public static class SimulationStateHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// Hash of session's current state; null throws ArgumentNullException.
        public static ulong Compute(RunSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            ulong hash = OffsetBasis;
            hash = AddLong(hash, session.Clock.Step);

            PlayerEntity player = session.Player;
            hash = AddFloat(hash, player.Position.x);
            hash = AddFloat(hash, player.Position.y);
            hash = AddFloat(hash, session.Health.Hp);
            hash = AddInt(hash, session.Experience.Xp);
            hash = AddInt(hash, session.Experience.Level);

            // ActiveEntities has no order (swap-remove); hash a sorted copy.
            var entities = new List<SimEntity>(session.Lifecycle.ActiveEntities);
            entities.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < entities.Count; i++)
            {
                SimEntity entity = entities[i];
                hash = AddInt(hash, entity.Id.Index);
                hash = AddInt(hash, entity.Id.Generation);
                hash = AddInt(hash, (int)entity.Category);
                hash = AddFloat(hash, entity.Position.x);
                hash = AddFloat(hash, entity.Position.y);
                if (entity is Monster monster)
                {
                    hash = AddFloat(hash, monster.Hp);
                }
            }

            hash = AddInt(hash, session.KillPipeline.KillCount);

            IReadOnlyList<WeaponInstance> slots = session.Weapons.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                WeaponInstance weapon = slots[i];
                if (weapon != null)
                {
                    hash = AddInt(hash, weapon.SlotIndex);
                    hash = AddLong(hash, weapon.NextFireStep);
                }
            }

            return hash;
        }

        private static ulong AddFloat(ulong hash, float value)
        {
            return AddInt(hash, BitConverter.SingleToInt32Bits(value));
        }

        private static ulong AddLong(ulong hash, long value)
        {
            hash = AddInt(hash, (int)value);
            return AddInt(hash, (int)(value >> 32));
        }

        private static ulong AddInt(ulong hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (byte)(value >> (8 * i));
                    hash *= Prime;
                }
            }

            return hash;
        }
    }
}
