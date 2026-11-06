using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R08_HealWeightTests
    {
        [Test]
        public void FullHealth_GivesZeroWeight()
        {
            Assert.AreEqual(0f, HealWeight.Compute(10f, 100f, 100f));
        }

        [Test]
        public void ZeroHealth_GivesBaseWeight()
        {
            Assert.AreEqual(10f, HealWeight.Compute(10f, 0f, 100f));
        }

        [Test]
        public void HalfHealth_GivesQuarterWeight()
        {
            Assert.AreEqual(2.5f, HealWeight.Compute(10f, 50f, 100f));
        }

        [Test]
        public void RatioIsClampedToUnitRange()
        {
            Assert.AreEqual(0f, HealWeight.Compute(10f, 150f, 100f));
            Assert.AreEqual(10f, HealWeight.Compute(10f, -20f, 100f));
        }

        [Test]
        public void WeightDecreasesMonotonicallyWithHealth()
        {
            float previous = float.PositiveInfinity;
            for (int hp = 0; hp <= 100; hp++)
            {
                float w = HealWeight.Compute(1f, hp, 100f);
                Assert.LessOrEqual(w, previous);
                previous = w;
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void NonPositiveMaxHp_Throws(float maxHp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HealWeight.Compute(1f, 1f, maxHp));
        }
    }
}
