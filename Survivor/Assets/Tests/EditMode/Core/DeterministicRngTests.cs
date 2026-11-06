using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class DeterministicRngTests
    {
        [Test]
        public void MatchesPcg32ReferenceVector()
        {
            // pcg32-demo (pcg-c-basic): pcg32_srandom_r(&rng, 42u, 54u), first six pcg32_random_r outputs.
            var rng = new DeterministicRng(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };
            foreach (uint value in expected)
            {
                Assert.AreEqual(value, rng.NextUInt());
            }
        }

        [Test]
        public void SameSeedAndStream_GiveSameSequence()
        {
            var a = new DeterministicRng(123456789UL, 3UL);
            var b = new DeterministicRng(123456789UL, 3UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void DifferentStream_GivesDifferentSequence()
        {
            var a = new DeterministicRng(1UL, 1UL);
            var b = new DeterministicRng(1UL, 2UL);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }

            Assert.Less(same, 5);
        }

        [Test]
        public void NextFloat01_IsTopTwentyFourBitsScaled()
        {
            var rng = new DeterministicRng(77UL, 4UL);
            var twin = new DeterministicRng(77UL, 4UL);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat01();
                Assert.AreEqual((twin.NextUInt() >> 8) * (1f / 16777216f), f);
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void NextInt_StaysInRangeAndCoversAllValues()
        {
            var rng = new DeterministicRng(5UL, 5UL);
            var seen = new bool[7];
            for (int i = 0; i < 2000; i++)
            {
                int v = rng.NextInt(7);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 7);
                seen[v] = true;
            }

            CollectionAssert.DoesNotContain(seen, false);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0, rng.NextInt(1));
                int big = rng.NextInt(int.MaxValue);
                Assert.GreaterOrEqual(big, 0);
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NextInt_NonPositiveBoundThrows(int bound)
        {
            var rng = new DeterministicRng(1UL, 1UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(bound));
        }

        [Test]
        public void Chance_ExtremesAreCertain()
        {
            var rng = new DeterministicRng(8UL, 2UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
            }
        }
    }
}
