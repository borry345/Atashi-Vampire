using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R20_GameRulesTests
    {
        [Test]
        public void Constants_MatchDocumentedValues()
        {
            Assert.AreEqual(20f, GameRules.SpawnRadius);
            Assert.AreEqual(45f, GameRules.DefaultDespawnDistance);
            Assert.AreEqual(400, GameRules.MonsterCapDefault);
            Assert.AreEqual(36000L, GameRules.BossSpawnStep);
            Assert.AreEqual(0.02f, GameRules.ChestDropChance);
            Assert.AreEqual(0.70f, GameRules.ChestUpgradeChance);
            Assert.AreEqual(0.30f, GameRules.HealRatio);
            Assert.AreEqual(4, GameRules.MaxWeaponSlots);
            Assert.AreEqual(3, GameRules.MaxPassiveSlots);
            Assert.AreEqual(9f, GameRules.CameraOrthographicSize);
            Assert.AreEqual(1.0f, GameRules.LargeMonsterRadiusThreshold);
            Assert.AreEqual(0.25f, GameRules.MinTimeScale);
            Assert.AreEqual(3f, GameRules.MaxTimeScale);
            Assert.AreEqual(300, GameRules.MeasurementWarmupSteps);
            Assert.AreEqual(55f, GameRules.MonsterLimitFpsThreshold);
        }
    }
}
