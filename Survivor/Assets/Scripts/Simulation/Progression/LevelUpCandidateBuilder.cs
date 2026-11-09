using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Level-up candidates with their weights (TAC-F5-1..F5-3, R8, R12, R22).
    public static class LevelUpCandidateBuilder
    {
        /// Heal candidates and fillers carry this target id (UpgradeOrdering.Compare also accepts null; "" keeps it printable).
        public const string HealTargetId = "";

        /// Clears output and fills it in canonical order (R12) with (option, weight). Weapon/passive weights come from
        /// data.LevelUpPool.ResolveWeight; heal = HealWeight.Compute(HealBaseWeight, health, maxHealth) and is left out at 0.
        /// Upgrades read levelCount / evolvedOnly from the owned instance's captured definition (kept across hot reload, TAC-F11-5);
        /// new acquisitions read data (= Current) and are left out when the slots are full, the item is owned, the weapon is
        /// evolution-only or it was consumed by an evolution this run. IsFiller is always false here.
        /// Null arguments throw ArgumentNullException; maxHealth <= 0 throws ArgumentOutOfRangeException (R8).
        public static void Build(GameData data, WeaponInventory weapons, PassiveInventory passives,
                                 float health, float maxHealth, List<(UpgradeOption option, float weight)> output)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (passives == null)
            {
                throw new ArgumentNullException(nameof(passives));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            LevelUpPoolDefinition pool = data.LevelUpPool ?? LevelUpPoolDefinition.UniformFallback;
            output.Clear();

            IReadOnlyList<WeaponInstance> weaponSlots = weapons.Slots;
            for (int i = 0; i < weaponSlots.Count; i++)
            {
                WeaponInstance weapon = weaponSlots[i];
                if (weapon != null
                    && ProgressionRules.IsWeaponUpgradable(weapon.Level, weapon.Definition.Levels.Count, weapon.Definition.EvolvedOnly))
                {
                    Add(output, pool, UpgradeKind.WeaponUpgrade, weapon.Id);
                }
            }

            if (weapons.Count < GameRules.MaxWeaponSlots)
            {
                IReadOnlyList<WeaponDefinition> definitions = data.Weapons;
                for (int i = 0; i < definitions.Count; i++)
                {
                    WeaponDefinition definition = definitions[i];
                    if (!definition.EvolvedOnly && !weapons.Owns(definition.Id) && !Contains(weapons.ConsumedByEvolution, definition.Id))
                    {
                        Add(output, pool, UpgradeKind.WeaponNew, definition.Id);
                    }
                }
            }

            IReadOnlyList<PassiveInstance> passiveSlots = passives.Slots;
            for (int i = 0; i < passiveSlots.Count; i++)
            {
                PassiveInstance passive = passiveSlots[i];
                if (passive != null && ProgressionRules.IsPassiveUpgradable(passive.Level, passive.Definition.Levels.Count))
                {
                    Add(output, pool, UpgradeKind.PassiveUpgrade, passive.Id);
                }
            }

            if (passives.Count < GameRules.MaxPassiveSlots)
            {
                IReadOnlyList<PassiveDefinition> definitions = data.Passives;
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (!passives.Owns(definitions[i].Id))
                    {
                        Add(output, pool, UpgradeKind.PassiveNew, definitions[i].Id);
                    }
                }
            }

            float healWeight = HealWeight.Compute(pool.HealBaseWeight, health, maxHealth);
            if (healWeight > 0f)
            {
                output.Add((new UpgradeOption(UpgradeKind.Heal, HealTargetId), healWeight));
            }

            output.Sort((a, b) => UpgradeOrdering.Compare(a.option, b.option));
        }

        private static void Add(List<(UpgradeOption option, float weight)> output, LevelUpPoolDefinition pool, UpgradeKind kind, string id)
        {
            output.Add((new UpgradeOption(kind, id), pool.ResolveWeight(kind, id)));
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
