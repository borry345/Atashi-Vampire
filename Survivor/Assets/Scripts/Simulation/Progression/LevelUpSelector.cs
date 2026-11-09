using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Draws the three options of a level-up round (TAC-F5-1, TAC-F5-2, R9).
    public static class LevelUpSelector
    {
        private const int OptionCount = 3;

        /// Clears output3 and fills it with exactly three options: up to three candidates drawn without replacement by weight
        /// (WeightedSampler, R9) in draw order, then Heal fillers (IsFiller = true, even at full health) for the rest.
        /// rng must be the LevelUp stream; nothing else is drawn from. Null arguments throw ArgumentNullException.
        public static void Draw(List<(UpgradeOption option, float weight)> candidates, DeterministicRng rng, List<UpgradeOption> output3)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (output3 == null)
            {
                throw new ArgumentNullException(nameof(output3));
            }

            var weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = candidates[i].weight;
            }

            var drawn = new List<int>(OptionCount);
            WeightedSampler.SampleWithoutReplacement(weights, OptionCount, rng, drawn);
            output3.Clear();
            for (int i = 0; i < drawn.Count; i++)
            {
                output3.Add(candidates[drawn[i]].option);
            }

            while (output3.Count < OptionCount)
            {
                output3.Add(new UpgradeOption(UpgradeKind.Heal, LevelUpCandidateBuilder.HealTargetId, isFiller: true));
            }
        }
    }
}
