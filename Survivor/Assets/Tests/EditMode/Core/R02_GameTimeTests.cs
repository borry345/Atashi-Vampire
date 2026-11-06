using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R02_GameTimeTests
    {
        [Test]
        public void StepsToSeconds_DividesBySixty()
        {
            Assert.AreEqual(0d, StepMath.StepsToSeconds(0));
            Assert.AreEqual(1.5d, StepMath.StepsToSeconds(90));
            Assert.AreEqual(600d, StepMath.StepsToSeconds(36000));
        }

        [TestCase(0L, "00:00")]
        [TestCase(59L, "00:00")]
        [TestCase(60L, "00:01")]
        [TestCase(3599L, "00:59")]
        [TestCase(3600L, "01:00")]
        [TestCase(36000L, "10:00")]
        [TestCase(359999L, "99:59")]
        [TestCase(360000L, "100:00")]
        public void FormatGameTime_FloorsSecondsAndKeepsGrowingMinutes(long steps, string expected)
        {
            Assert.AreEqual(expected, StepMath.FormatGameTime(steps));
        }

        [Test]
        public void FormatGameTime_NegativeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StepMath.FormatGameTime(-1));
        }
    }
}
