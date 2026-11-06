using System.Linq;
using NUnit.Framework;
using Survivor.Data;

namespace Survivor.Tests.EditMode.Data
{
    /// The four "not playable" criteria (5.11): each one alone blocks the run.
    public sealed class PlayabilityTests
    {
        private const string EvolvedOnlyWeapons = @"{ ""schemaVersion"": 1, ""weapons"": [
  { ""id"": ""E1"", ""displayName"": ""연사 투사체"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.gold"", ""evolvedOnly"": true,
    ""levels"": [ { ""damage"": 22, ""cooldownSeconds"": 0.2, ""projectileSpeed"": 14, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 } ] } ] }";

        private const string UnknownMonsterSpawnTable = @"{ ""schemaVersion"": 1, ""despawnDistance"": 45, ""bossMonsterId"": ""M4"",
  ""segments"": [ { ""startSeconds"": 0, ""endSeconds"": 600, ""spawnsPerSecond"": 2, ""entries"": [ { ""monsterId"": ""M9"", ""weight"": 1 } ] } ] }";

        [Test]
        public void NoUsableWeapon_Blocks()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Weapons, EvolvedOnlyWeapons));
            Assert.AreEqual(0, PlayabilityChecker.CountUsableWeapons(data));
            Assert.IsFalse(report.IsPlayable);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.Weapons).Any(i => i.Message.Contains("무기가 0개")), DataTestFixtures.Dump(report));
        }

        [Test]
        public void NoSpawnableMonster_Blocks()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.SpawnTable, UnknownMonsterSpawnTable));
            Assert.IsNotNull(data.SpawnTable);
            Assert.AreEqual(0, PlayabilityChecker.CountSpawnableMonsters(data));
            Assert.IsFalse(report.IsPlayable);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.SpawnTable).Any(i => i.Message.Contains("몬스터가 0종")), DataTestFixtures.Dump(report));
        }

        [Test]
        public void NoPlayerStats_Blocks()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.PlayerStats, null));
            Assert.IsNull(data.PlayerStats);
            Assert.IsFalse(report.IsPlayable);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.PlayerStats).Any(i => i.Message.Contains("플레이어 기본 스탯이 없습니다")));
        }

        [Test]
        public void NoSpawnTable_Blocks()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.SpawnTable, "{ \"schemaVersion\": 1, "));
            Assert.IsNull(data.SpawnTable);
            Assert.IsFalse(report.IsPlayable);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.SpawnTable).Any(i => i.Message.Contains("스폰 테이블이 없습니다")));
        }

        [Test]
        public void ValidData_PassesAllFourCriteria()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.AreEqual(4, PlayabilityChecker.CountUsableWeapons(data));
            Assert.AreEqual(3, PlayabilityChecker.CountSpawnableMonsters(data), "boss is not a normal spawn");
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void BlockingIssues_ListsOnlyBlocking()
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.PlayerStats, null));
            Assert.IsTrue(report.BlockingIssues.All(i => i.Severity == IssueSeverity.Blocking));
            Assert.AreEqual(report.BlockingCount, report.BlockingIssues.Count());
            Assert.AreEqual(report.Issues.Count, report.BlockingCount + report.WarningCount);
        }
    }
}
