using System;
using NUnit.Framework;
using Survivor.Data;

namespace Survivor.Tests.EditMode.Data
{
    /// R21: required experience = thresholds[L-1], then last + growth per level past the table.
    public sealed class R21_XpCurveTests
    {
        private static readonly XpCurveDefinition Curve = new XpCurveDefinition(new[] { 5, 8, 12, 16, 20, 25, 30 }, 5);

        [TestCase(1, 5)]
        [TestCase(2, 8)]
        [TestCase(7, 30)]
        [TestCase(8, 35)]
        [TestCase(9, 40)]
        [TestCase(20, 95)]
        public void RequiredForNext_FollowsTableThenGrowth(int level, int expected)
        {
            Assert.AreEqual(expected, XpCurve.RequiredForNext(level, Curve));
        }

        [Test]
        public void ZeroGrowth_RepeatsLastValue()
        {
            var flat = new XpCurveDefinition(new[] { 3 }, 0);
            Assert.AreEqual(3, XpCurve.RequiredForNext(1, flat));
            Assert.AreEqual(3, XpCurve.RequiredForNext(100, flat));
        }

        [Test]
        public void HugeLevel_Saturates()
        {
            var steep = new XpCurveDefinition(new[] { 1 }, int.MaxValue);
            Assert.AreEqual(int.MaxValue, XpCurve.RequiredForNext(int.MaxValue, steep));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => XpCurve.RequiredForNext(0, Curve));
            Assert.Throws<ArgumentNullException>(() => XpCurve.RequiredForNext(1, null));
            Assert.Throws<ArgumentException>(() => new XpCurveDefinition(new int[0], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new XpCurveDefinition(new[] { 0 }, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new XpCurveDefinition(new[] { 1 }, -1));
        }

        [Test]
        public void LoadedCurve_MatchesFile()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.AreEqual(35, XpCurve.RequiredForNext(8, data.PlayerStats.XpCurve));
        }
    }
}
