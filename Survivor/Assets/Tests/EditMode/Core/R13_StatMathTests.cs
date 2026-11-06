using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R13_StatMathTests
    {
        [TestCase(60, 0f, 60)]
        [TestCase(60, 0.5f, 40)]
        [TestCase(3, 1f, 2)]     // 1.5 -> 2
        [TestCase(5, 1f, 3)]     // 2.5 -> 3
        [TestCase(1, 10f, 1)]    // 0.09 -> clamped to 1
        [TestCase(0, 0.2f, 1)]   // floor of 1 step
        public void ApplyAttackSpeed_RoundsAndKeepsAtLeastOneStep(int baseSteps, float bonus, int expected)
        {
            Assert.AreEqual(expected, StatMath.ApplyAttackSpeed(baseSteps, bonus));
        }

        [Test]
        public void ApplyAttackSpeed_InvalidArgumentsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StatMath.ApplyAttackSpeed(60, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StatMath.ApplyAttackSpeed(60, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => StatMath.ApplyAttackSpeed(-1, 0f));
        }

        [Test]
        public void ApplyMultiplier_ScalesByOnePlusBonus()
        {
            Assert.AreEqual(2.5f, StatMath.ApplyMultiplier(2f, 0.25f));
            Assert.AreEqual(2f, StatMath.ApplyMultiplier(2f, 0f));
            Assert.AreEqual(3f, StatMath.ApplyMultiplier(1.5f, 1f));
        }
    }
}
