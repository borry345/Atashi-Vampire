using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R22_R23_ProgressionRulesTests
    {
        [TestCase(1, 5, false, true)]
        [TestCase(4, 5, false, true)]
        [TestCase(5, 5, false, false)]
        [TestCase(6, 5, false, false)]
        [TestCase(1, 5, true, false)]
        [TestCase(0, 1, true, false)]
        public void R22_WeaponUpgradable_BelowMaxAndNotEvolvedOnly(int level, int count, bool evolvedOnly, bool expected)
        {
            Assert.AreEqual(expected, ProgressionRules.IsWeaponUpgradable(level, count, evolvedOnly));
        }

        [TestCase(0, 1, true)]
        [TestCase(4, 5, true)]
        [TestCase(5, 5, false)]
        [TestCase(1, 1, false)]
        public void R22_PassiveUpgradable_BelowMax(int level, int count, bool expected)
        {
            Assert.AreEqual(expected, ProgressionRules.IsPassiveUpgradable(level, count));
        }

        [TestCase(50f, 100f, 80f)]
        [TestCase(0f, 100f, 30f)]
        [TestCase(70f, 100f, 100f)]
        [TestCase(90f, 100f, 100f)]
        [TestCase(100f, 100f, 100f)]
        public void R23_ApplyHeal_AddsThirtyPercentAndStopsAtMax(float hp, float maxHp, float expected)
        {
            Assert.AreEqual(expected, ProgressionRules.ApplyHeal(hp, maxHp), 1e-4f);
        }

        [Test]
        public void R23_ApplyHeal_NeverExceedsMax()
        {
            for (int hp = 0; hp <= 120; hp++)
            {
                Assert.LessOrEqual(ProgressionRules.ApplyHeal(hp, 120f), 120f);
            }
        }
    }
}
