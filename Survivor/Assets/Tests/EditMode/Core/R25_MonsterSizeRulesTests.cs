using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R25_MonsterSizeRulesTests
    {
        [TestCase(0.5f, false, false)]
        [TestCase(1.0f, false, false)]     // strictly greater than the threshold
        [TestCase(1.0001f, false, true)]
        [TestCase(3f, false, true)]
        [TestCase(0.1f, true, true)]       // bosses are always large
        [TestCase(1.0f, true, true)]
        public void IsLarge_RadiusAboveThresholdOrBoss(float radius, bool isBoss, bool expected)
        {
            Assert.AreEqual(expected, MonsterSizeRules.IsLarge(radius, isBoss));
        }
    }
}
