using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R01_SecondsToStepsTests
    {
        [TestCase(0d, 0)]
        [TestCase(1d, 60)]
        [TestCase(0.1d, 6)]
        [TestCase(2.5d, 150)]
        [TestCase(0.0625d, 4)]   // 3.75 -> 4
        [TestCase(0.125d, 8)]    // 7.5 -> 8 (banker's rounding would also give 8)
        [TestCase(0.375d, 23)]   // 22.5 -> 23 AwayFromZero (banker's rounding would give 22)
        [TestCase(0.008d, 0)]    // 0.48 -> 0
        [TestCase(0.009d, 1)]    // 0.54 -> 1
        public void SecondsToSteps_RoundsHalfAwayFromZero(double seconds, int expected)
        {
            Assert.AreEqual(expected, StepMath.SecondsToSteps(seconds));
        }

        [TestCase(0d, 1)]
        [TestCase(0.008d, 1)]
        [TestCase(0.009d, 1)]
        [TestCase(0.375d, 23)]
        [TestCase(1d, 60)]
        public void SecondsToIntervalSteps_IsAtLeastOne(double seconds, int expected)
        {
            Assert.AreEqual(expected, StepMath.SecondsToIntervalSteps(seconds));
        }

        [Test]
        public void FloatArgument_IsWidenedExactly()
        {
            Assert.AreEqual(23, StepMath.SecondsToSteps(0.375f));
            Assert.AreEqual(23, StepMath.SecondsToIntervalSteps(0.375f));
        }

        [TestCase(-0.001d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(1e9d)]
        public void InvalidSeconds_Throw(double seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StepMath.SecondsToSteps(seconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => StepMath.SecondsToIntervalSteps(seconds));
        }

        [Test]
        public void ClockConstants_MatchDocumentedValues()
        {
            Assert.AreEqual(1f / 60f, StepMath.StepSeconds);
            Assert.AreEqual(3, StepMath.MaxCatchUpSteps);
            Assert.AreEqual(0.05f, StepMath.MaxCatchUpSteps * StepMath.StepSeconds, 1e-6f);
        }
    }
}
