using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R24_KillRulesTests
    {
        [TestCase(DespawnReason.Killed, true)]
        [TestCase(DespawnReason.OutOfRange, false)]
        [TestCase(DespawnReason.LifetimeExpired, false)]
        [TestCase(DespawnReason.Consumed, false)]
        [TestCase(DespawnReason.Replaced, false)]
        [TestCase(DespawnReason.RunReset, false)]
        public void OnlyKilled_CountsAsKill(DespawnReason reason, bool expected)
        {
            Assert.AreEqual(expected, KillRules.CountsAsKill(reason));
        }
    }
}
