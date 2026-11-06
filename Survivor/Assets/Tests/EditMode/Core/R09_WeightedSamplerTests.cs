using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R09_WeightedSamplerTests
    {
        [Test]
        public void PickIndex_FollowsCumulativeRule()
        {
            // Re-derive each draw from a twin generator: u = NextFloat01() * total, first cum > u.
            var weights = new List<float> { 1f, 0f, 3f, -2f, 4f };
            var rng = new DeterministicRng(11UL, 3UL);
            var twin = new DeterministicRng(11UL, 3UL);
            for (int i = 0; i < 1000; i++)
            {
                float u = twin.NextFloat01() * 8f;
                int expected = u < 1f ? 0 : (u < 4f ? 2 : 4);
                Assert.AreEqual(expected, WeightedSampler.PickIndex(weights, rng));
            }
        }

        [Test]
        public void PickIndex_NeverPicksNonPositiveWeights()
        {
            var weights = new List<float> { 0f, 2f, -1f, 1f };
            var rng = new DeterministicRng(5UL, 1UL);
            for (int i = 0; i < 5000; i++)
            {
                int index = WeightedSampler.PickIndex(weights, rng);
                Assert.IsTrue(index == 1 || index == 3, $"picked {index}");
            }
        }

        [Test]
        public void PickIndex_NoPositiveWeight_ReturnsMinusOneWithoutConsumingRng()
        {
            var rng = new DeterministicRng(5UL, 1UL);
            var twin = new DeterministicRng(5UL, 1UL);
            Assert.AreEqual(-1, WeightedSampler.PickIndex(new List<float> { 0f, -1f }, rng));
            Assert.AreEqual(-1, WeightedSampler.PickIndex(new List<float>(), rng));
            Assert.AreEqual(twin.NextUInt(), rng.NextUInt());
        }

        [Test]
        public void PickIndex_SinglePositiveWeight_AlwaysPicksIt()
        {
            var weights = new List<float> { 0f, 0f, 0.0001f };
            var rng = new DeterministicRng(9UL, 2UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(2, WeightedSampler.PickIndex(weights, rng));
            }
        }

        [Test]
        public void SampleWithoutReplacement_ReturnsDistinctIndicesInDrawOrder()
        {
            var weights = new List<float> { 1f, 2f, 3f, 4f, 5f };
            var output = new List<int>();
            var rng = new DeterministicRng(21UL, 5UL);
            for (int trial = 0; trial < 500; trial++)
            {
                WeightedSampler.SampleWithoutReplacement(weights, 3, rng, output);
                Assert.AreEqual(3, output.Count);
                CollectionAssert.AllItemsAreUnique(output);
            }
        }

        [Test]
        public void SampleWithoutReplacement_IsReproducibleForSameSeed()
        {
            var weights = new List<float> { 1f, 2f, 3f, 4f, 5f };
            var a = new List<int>();
            var b = new List<int>();
            var rngA = new DeterministicRng(99UL, 5UL);
            var rngB = new DeterministicRng(99UL, 5UL);
            for (int trial = 0; trial < 200; trial++)
            {
                WeightedSampler.SampleWithoutReplacement(weights, 3, rngA, a);
                WeightedSampler.SampleWithoutReplacement(weights, 3, rngB, b);
                CollectionAssert.AreEqual(a, b);
            }
        }

        [Test]
        public void SampleWithoutReplacement_StopsWhenPositiveWeightsRunOut()
        {
            var output = new List<int> { 42 };
            var rng = new DeterministicRng(1UL, 1UL);
            WeightedSampler.SampleWithoutReplacement(new List<float> { 0f, 1f, 0f, 2f }, 3, rng, output);
            Assert.AreEqual(2, output.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, output);

            WeightedSampler.SampleWithoutReplacement(new List<float> { 1f }, 0, rng, output);
            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var rng = new DeterministicRng(1UL, 1UL);
            var weights = new List<float> { 1f };
            Assert.Throws<ArgumentNullException>(() => WeightedSampler.PickIndex(null, rng));
            Assert.Throws<ArgumentNullException>(() => WeightedSampler.PickIndex(weights, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeightedSampler.SampleWithoutReplacement(weights, -1, rng, new List<int>()));
            Assert.Throws<ArgumentNullException>(() => WeightedSampler.SampleWithoutReplacement(weights, 1, rng, null));
        }
    }
}
