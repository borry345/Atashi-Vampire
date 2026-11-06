using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Data
{
    /// Level-up choice weights from levelUpPool.json; UniformFallback when the file is missing or broken.
    public sealed class LevelUpPoolDefinition
    {
        private static readonly LevelUpRuleDefinition[] NoRules = new LevelUpRuleDefinition[0];

        /// Fallback pool: defaultWeight 1, healBaseWeight 1, no rules (heal still uses the (1 - r)^2 curve, R8).
        public static readonly LevelUpPoolDefinition UniformFallback = new LevelUpPoolDefinition
        {
            DefaultWeight = 1f,
            HealBaseWeight = 1f,
            Rules = NoRules,
            IsFallback = true,
        };

        /// Weight of candidates no rule matches (> 0).
        public float DefaultWeight { get; internal set; }

        /// Heal base weight w_base (>= 0) for HealWeight.Compute (R8).
        public float HealBaseWeight { get; internal set; }

        /// Valid rules in file order.
        public IReadOnlyList<LevelUpRuleDefinition> Rules { get; internal set; }

        /// True for UniformFallback.
        public bool IsFallback { get; internal set; }

        /// Weight of a weapon/passive candidate: the first rule with the exact target id, else the first "*" rule of the kind, else DefaultWeight. Heal is not handled here (use HealBaseWeight with R8).
        public float ResolveWeight(UpgradeKind kind, string targetId)
        {
            if (kind == UpgradeKind.Heal)
            {
                throw new ArgumentException("Heal weight is computed from HealBaseWeight with HealWeight.Compute (R8).", nameof(kind));
            }

            LevelUpRuleDefinition wildcard = null;
            for (int i = 0; i < Rules.Count; i++)
            {
                LevelUpRuleDefinition rule = Rules[i];
                if (rule.Kind != kind)
                {
                    continue;
                }

                if (string.Equals(rule.TargetId, targetId, StringComparison.Ordinal))
                {
                    return rule.Weight;
                }

                if (wildcard == null && rule.TargetId == LevelUpRuleDefinition.AnyTarget)
                {
                    wildcard = rule;
                }
            }

            return wildcard != null ? wildcard.Weight : DefaultWeight;
        }
    }
}
