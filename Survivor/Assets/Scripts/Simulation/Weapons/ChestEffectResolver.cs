using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Decides and applies one chest's effect (TAC-F4-4): (1) the first evolvable weapon evolves, no RNG drawn; (2) otherwise
    /// Chance(ChestUpgradeChance) picks upgrade (NextInt over upgradable owned weapons in slot order; empty list -> heal) or heal.
    /// Called once per chest, in event order, so an earlier chest's evolution is visible to the next one.
    public sealed class ChestEffectResolver
    {
        private readonly EvolutionService _evolution;
        private readonly PlayerProgressionApplier _applier;
        private readonly WeaponInventory _weapons;
        private readonly List<int> _upgradable = new List<int>(GameRules.MaxWeaponSlots);

        /// Creates the resolver; null arguments throw ArgumentNullException.
        public ChestEffectResolver(EvolutionService evolution, PlayerProgressionApplier applier, WeaponInventory weapons)
        {
            _evolution = evolution ?? throw new ArgumentNullException(nameof(evolution));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        }

        /// Resolves one chest with the Chest stream (null throws ArgumentNullException) and applies it through the applier.
        public ChestEffect Resolve(DeterministicRng chestRng, in StepContext context)
        {
            if (chestRng == null)
            {
                throw new ArgumentNullException(nameof(chestRng));
            }

            if (_evolution.TryFindFirstEligible(out int evolveSlot, out CombinationRule rule))
            {
                _applier.ApplyEvolution(evolveSlot, rule, context);
                return ChestEffect.Evolved;
            }

            if (!chestRng.Chance(GameRules.ChestUpgradeChance))
            {
                _applier.Heal();
                return ChestEffect.Healed;
            }

            _upgradable.Clear();
            IReadOnlyList<WeaponInstance> slots = _weapons.Slots;
            for (int s = 0; s < slots.Count; s++)
            {
                WeaponInstance weapon = slots[s];
                if (weapon != null
                    && ProgressionRules.IsWeaponUpgradable(weapon.Level, weapon.Definition.Levels.Count, weapon.Definition.EvolvedOnly))
                {
                    _upgradable.Add(s);
                }
            }

            if (_upgradable.Count == 0)
            {
                _applier.Heal();
                return ChestEffect.Healed;
            }

            _applier.ApplyChestUpgrade(_upgradable[chestRng.NextInt(_upgradable.Count)]);
            return ChestEffect.WeaponUpgraded;
        }
    }
}
