using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Decides and applies one chest's effect (TAC-F4-4): (1) the first evolvable weapon evolves, no RNG drawn; (2) otherwise
    /// Chance(ChestUpgradeChance) picks upgrade (one NextInt over upgradable owned weapons in weapon slot order followed by
    /// upgradable owned passives in passive slot order, TAC-F4-10; empty list -> heal) or heal.
    /// Called once per chest, in event order, so an earlier chest's evolution is visible to the next one. Each applied effect is
    /// recorded once in applier.ChestResults right after it is applied (TAC-F4-9); recording draws nothing and changes no effect.
    public sealed class ChestEffectResolver
    {
        private readonly EvolutionService _evolution;
        private readonly PlayerProgressionApplier _applier;
        private readonly WeaponInventory _weapons;
        // Weapon candidates hold the weapon slot index; passive candidates hold MaxWeaponSlots + the passive slot index.
        private readonly List<int> _upgradable = new List<int>(GameRules.MaxWeaponSlots + GameRules.MaxPassiveSlots);

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
                _applier.ChestResults.Add(context.Step, ChestEffect.Evolved, rule.ResultWeaponId, 0, 1, 0f);
                return ChestEffect.Evolved;
            }

            if (!chestRng.Chance(GameRules.ChestUpgradeChance))
            {
                float healed = _applier.Heal();
                _applier.ChestResults.Add(context.Step, ChestEffect.Healed, null, 0, 0, healed);
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

            IReadOnlyList<PassiveInstance> passiveSlots = _applier.Passives.Slots;
            for (int s = 0; s < passiveSlots.Count; s++)
            {
                PassiveInstance passive = passiveSlots[s];
                if (passive != null && ProgressionRules.IsPassiveUpgradable(passive.Level, passive.Definition.Levels.Count))
                {
                    _upgradable.Add(GameRules.MaxWeaponSlots + s);
                }
            }

            if (_upgradable.Count == 0)
            {
                float healed = _applier.Heal();
                _applier.ChestResults.Add(context.Step, ChestEffect.Healed, null, 0, 0, healed);
                return ChestEffect.Healed;
            }

            int upgradeSlot = _upgradable[chestRng.NextInt(_upgradable.Count)];
            if (upgradeSlot >= GameRules.MaxWeaponSlots)
            {
                int passiveSlot = upgradeSlot - GameRules.MaxWeaponSlots;
                PassiveInstance passive = passiveSlots[passiveSlot];
                int passiveFrom = passive.Level;
                _applier.ApplyChestPassiveUpgrade(passiveSlot);
                _applier.ChestResults.Add(context.Step, ChestEffect.PassiveUpgraded, passive.Id, passiveFrom, passive.Level, 0f);
                return ChestEffect.PassiveUpgraded;
            }

            WeaponInstance upgraded = slots[upgradeSlot];
            int fromLevel = upgraded.Level;
            _applier.ApplyChestUpgrade(upgradeSlot);
            _applier.ChestResults.Add(context.Step, ChestEffect.WeaponUpgraded, upgraded.Id, fromLevel, upgraded.Level, 0f);
            return ChestEffect.WeaponUpgraded;
        }
    }
}
