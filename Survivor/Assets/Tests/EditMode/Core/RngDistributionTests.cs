using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    /// Seed-fixed distribution checks from 5c.5 "RNG distribution"; a failure is a defect, never bad luck.
    public sealed class RngDistributionTests
    {
        private const ulong Seed = 20261106UL;

        [Test]
        public void ChestDrop_TenThousandKills_IsTwoPercentWithinPointFourPoints()
        {
            DeterministicRng rng = new RngStreams(Seed).Get(RngStream.Drop);
            int drops = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (rng.Chance(GameRules.ChestDropChance))
                {
                    drops++;
                }
            }

            Assert.AreEqual(0.02, drops / 10000.0, 0.004);
        }

        [Test]
        public void ChestEffect_OneThousandChests_IsSeventyThirtyWithinThreePoints()
        {
            DeterministicRng rng = new RngStreams(Seed).Get(RngStream.Chest);
            int upgrades = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (rng.Chance(GameRules.ChestUpgradeChance))
                {
                    upgrades++;
                }
            }

            Assert.AreEqual(0.70, upgrades / 1000.0, 0.03);
        }

        [Test]
        public void LevelUp_OneThousandDraws_FrequencyFollowsWeightOrder()
        {
            DeterministicRng rng = new RngStreams(Seed).Get(RngStream.LevelUp);
            var weights = new List<float> { 1f, 2f, 3f, 4f, 5f };
            var counts = new int[weights.Count];
            var picked = new List<int>();
            for (int i = 0; i < 1000; i++)
            {
                WeightedSampler.SampleWithoutReplacement(weights, 3, rng, picked);
                foreach (int index in picked)
                {
                    counts[index]++;
                }
            }

            for (int i = 1; i < counts.Length; i++)
            {
                Assert.Greater(counts[i], counts[i - 1], $"weight {weights[i]} vs {weights[i - 1]}");
            }
        }

        [Test]
        public void HealFrequency_DecreasesAsHealthRises()
        {
            DeterministicRng rng = new RngStreams(Seed).Get(RngStream.LevelUp);
            float[] healthRatios = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            var healCounts = new int[healthRatios.Length];
            for (int h = 0; h < healthRatios.Length; h++)
            {
                float healWeight = HealWeight.Compute(1f, healthRatios[h] * 100f, 100f);
                var weights = new List<float> { 1f, 1f, 1f, healWeight };
                for (int i = 0; i < 1000; i++)
                {
                    if (WeightedSampler.PickIndex(weights, rng) == 3)
                    {
                        healCounts[h]++;
                    }
                }
            }

            for (int h = 1; h < healCounts.Length; h++)
            {
                Assert.Less(healCounts[h], healCounts[h - 1], $"health ratio {healthRatios[h]}");
            }

            Assert.AreEqual(0, healCounts[healCounts.Length - 1]);
        }
    }
}
