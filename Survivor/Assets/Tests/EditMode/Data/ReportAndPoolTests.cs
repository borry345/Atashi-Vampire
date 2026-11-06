using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.EditMode.Data
{
    /// ValidationIssue format, report logging (TAC-F11-2, 5c.3 #8) and level-up pool weight lookup.
    public sealed class ReportAndPoolTests
    {
        private static readonly Regex WarnLine = new Regex(@"^\[\d\d:\d\d:\d\d\.\d{3}\] \[WARN\] \[Data\] \[weapons\.json:12:5\] W3\.levels\[2\]\.zoneRadius: ");
        private static readonly Regex ErrorLine = new Regex(@"^\[\d\d:\d\d:\d\d\.\d{3}\] \[ERROR\] \[Data\] \[playerStats\.json\] maxHealth: ");

        [Test]
        public void ValidationIssue_ToString_MatchesDocumentedFormat()
        {
            var issue = new ValidationIssue(IssueSeverity.Warning, "weapons.json", "W3", "levels[2].zoneRadius", "필수 필드가 없습니다", 12, 5);
            Assert.AreEqual("[weapons.json:12:5] W3.levels[2].zoneRadius: 필수 필드가 없습니다", issue.ToString());
            Assert.AreEqual("[weapons.json] 메시지", new ValidationIssue(IssueSeverity.Warning, "weapons.json", null, null, "메시지").ToString());
            Assert.AreEqual("[spawnTable.json:3] segments[0]: x", new ValidationIssue(IssueSeverity.Warning, "spawnTable.json", null, "segments[0]", "x", 3).ToString());
        }

        [Test]
        public void Add_LogsWarningAndErrorLines()
        {
            var report = new DataValidationReport();
            LogAssert.Expect(LogType.Warning, WarnLine);
            report.Add(new ValidationIssue(IssueSeverity.Warning, "weapons.json", "W3", "levels[2].zoneRadius", "필수 필드가 없습니다", 12, 5));
            LogAssert.Expect(LogType.Error, ErrorLine);
            report.Add(new ValidationIssue(IssueSeverity.Blocking, "playerStats.json", null, "maxHealth", "오류"));
            Assert.IsFalse(report.IsPlayable);
            Assert.AreEqual(1, report.WarningCount);
            Assert.AreEqual(1, report.BlockingCount);
            Assert.Throws<ArgumentNullException>(() => report.Add(null));
        }

        [Test]
        public void SuccessfulLoad_LogsOneInfoLine()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[\d\d:\d\d:\d\d\.\d{3}\] \[INFO\] \[Data\] 데이터 로드 완료: 파일 7/7, 무기 6, 몬스터 4, "));
            GameDataLoader.LoadFromTexts(DataTestFixtures.ValidTexts());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LevelUpPool_SpecificRuleBeatsWildcard()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            LevelUpPoolDefinition pool = data.LevelUpPool;
            Assert.AreEqual(12f, pool.ResolveWeight(UpgradeKind.WeaponUpgrade, "W1"), "specific id");
            Assert.AreEqual(10f, pool.ResolveWeight(UpgradeKind.WeaponUpgrade, "W2"), "no rule -> default");
            Assert.AreEqual(8f, pool.ResolveWeight(UpgradeKind.WeaponNew, "W3"), "wildcard");
            Assert.AreEqual(10f, pool.ResolveWeight(UpgradeKind.PassiveNew, "P1"));
            Assert.Throws<ArgumentException>(() => pool.ResolveWeight(UpgradeKind.Heal, null));

            var specific = DataTestFixtures.Load(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"kind\": \"weaponUpgrade\", \"targetId\": \"W1\"", "\"kind\": \"weaponNew\", \"targetId\": \"W2\""));
            Assert.AreEqual(12f, specific.data.LevelUpPool.ResolveWeight(UpgradeKind.WeaponNew, "W2"), "specific beats an earlier wildcard");
            Assert.AreEqual(8f, specific.data.LevelUpPool.ResolveWeight(UpgradeKind.WeaponNew, "W3"));
        }

        [Test]
        public void UniformFallback_AllWeightsOne()
        {
            LevelUpPoolDefinition pool = LevelUpPoolDefinition.UniformFallback;
            Assert.IsTrue(pool.IsFallback);
            Assert.AreEqual(1f, pool.ResolveWeight(UpgradeKind.WeaponNew, "W1"));
            Assert.AreEqual(1f, pool.ResolveWeight(UpgradeKind.PassiveUpgrade, "P1"));
            Assert.AreEqual(1f, pool.HealBaseWeight);
        }
    }
}
