using System;
using System.Collections.Generic;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Finds the first evolvable weapon in slot order (Q42, TAC-F7-1): level == Levels.Count, not evolved-only, the rule's passive
    /// owned (any level). Combination rules are read from the current data on every call (5.11.1); a rule whose result weapon
    /// is not in the current data cannot evolve (5.14 rev 11).
    public sealed class EvolutionService
    {
        private readonly WeaponInventory _weapons;
        private readonly PassiveInventory _passives;
        private readonly Func<GameData> _currentData;

        /// Creates the service; currentData returns GameDataRegistry.Current. Null arguments throw ArgumentNullException.
        public EvolutionService(WeaponInventory weapons, PassiveInventory passives, Func<GameData> currentData)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
        }

        /// True with the slot and rule of the first eligible weapon in slot order; false (slotIndex -1, rule null) when none.
        public bool TryFindFirstEligible(out int slotIndex, out CombinationRule rule)
        {
            slotIndex = -1;
            rule = null;
            GameData data = _currentData();
            if (data == null)
            {
                return false;
            }

            IReadOnlyList<CombinationRule> rules = data.Combinations;
            IReadOnlyList<WeaponInstance> slots = _weapons.Slots;
            for (int s = 0; s < slots.Count; s++)
            {
                WeaponInstance weapon = slots[s];
                if (weapon == null || weapon.Definition.EvolvedOnly || weapon.Level != weapon.Definition.Levels.Count)
                {
                    continue;
                }

                for (int r = 0; r < rules.Count; r++)
                {
                    CombinationRule candidate = rules[r];
                    if (string.Equals(candidate.WeaponId, weapon.Id, StringComparison.Ordinal)
                        && _passives.Owns(candidate.PassiveId)
                        && data.TryGetWeapon(candidate.ResultWeaponId, out _))
                    {
                        slotIndex = s;
                        rule = candidate;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
