using System.Linq;
using NUnit.Framework;
using Survivor.Data;

namespace Survivor.Tests.EditMode.Data
{
    /// File-level policy (5.11): required files block, optional files warn and fall back; never throws.
    public sealed class FilePolicyTests
    {
        private static readonly DataFileKind[] Required = DataTestFixtures.RequiredKinds;
        private static readonly DataFileKind[] Optional = DataTestFixtures.OptionalKinds;
        private static readonly DataFileKind[] All = DataTestFixtures.AllKinds;

        [TestCaseSource(nameof(Required))]
        public void MissingRequiredFile_Blocks(DataFileKind kind)
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(kind, null));
            Assert.IsFalse(report.IsPlayable);
            var blocking = DataTestFixtures.Find(report, IssueSeverity.Blocking, kind);
            Assert.IsTrue(blocking.Any(i => i.Message.Contains("파일이 없습니다")), DataTestFixtures.Dump(report));
        }

        [TestCaseSource(nameof(Optional))]
        public void MissingOptionalFile_WarnsAndFallsBack(DataFileKind kind)
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(kind, null));
            AssertOptionalFallback(kind, data, report);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Warning, kind).Any(i => i.Message.Contains("파일이 없습니다")), DataTestFixtures.Dump(report));
        }

        [TestCaseSource(nameof(Required))]
        public void BrokenRequiredFile_BlocksWithPosition(DataFileKind kind)
        {
            string text = DataTestFixtures.ValidTexts()[kind];
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(kind, text.Substring(0, text.Length - 1)));
            Assert.IsFalse(report.IsPlayable);
            ValidationIssue issue = DataTestFixtures.Find(report, IssueSeverity.Blocking, kind).First();
            StringAssert.Contains("JSON 문법 오류", issue.Message);
            Assert.Greater(issue.Line, 0, "parse errors carry the line");
            StringAssert.StartsWith("[" + GameDataLoader.FileNameOf(kind) + ":" + issue.Line, issue.ToString());
        }

        [TestCaseSource(nameof(Optional))]
        public void BrokenOptionalFile_WarnsAndFallsBack(DataFileKind kind)
        {
            string text = DataTestFixtures.ValidTexts()[kind];
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(kind, text.Substring(0, text.Length - 1)));
            AssertOptionalFallback(kind, data, report);
            ValidationIssue issue = DataTestFixtures.Find(report, IssueSeverity.Warning, kind).First();
            StringAssert.Contains("JSON 문법 오류", issue.Message);
            Assert.Greater(issue.Line, 0);
        }

        [TestCaseSource(nameof(All))]
        public void DuplicatePropertyName_IsAFileLevelParseError(DataFileKind kind)
        {
            var texts = DataTestFixtures.With(kind, "\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1");
            var (data, report) = DataTestFixtures.Load(texts);
            IssueSeverity expected = Required.Contains(kind) ? IssueSeverity.Blocking : IssueSeverity.Warning;
            Assert.IsTrue(DataTestFixtures.Find(report, expected, kind).Any(i => i.Message.Contains("JSON 문법 오류") && i.Line > 0), DataTestFixtures.Dump(report));
            Assert.AreEqual(!Required.Contains(kind), report.IsPlayable);
        }

        [TestCaseSource(nameof(All))]
        public void RootNotObject_IsAFileLevelFailure(DataFileKind kind)
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(kind, "[1, 2, 3]"));
            IssueSeverity expected = Required.Contains(kind) ? IssueSeverity.Blocking : IssueSeverity.Warning;
            Assert.IsTrue(DataTestFixtures.Find(report, expected, kind).Any(i => i.Message.Contains("최상위 값은 객체")), DataTestFixtures.Dump(report));
        }

        [TestCaseSource(nameof(All))]
        public void SchemaVersionMissingOrUnsupported_IsAFileLevelFailure(DataFileKind kind)
        {
            IssueSeverity expected = Required.Contains(kind) ? IssueSeverity.Blocking : IssueSeverity.Warning;

            var (_, missing) = DataTestFixtures.Load(DataTestFixtures.With(kind, "\"schemaVersion\": 1,", string.Empty));
            Assert.IsTrue(DataTestFixtures.Find(missing, expected, kind).Any(i => i.Message.Contains("schemaVersion")), DataTestFixtures.Dump(missing));

            var (_, unsupported) = DataTestFixtures.Load(DataTestFixtures.With(kind, "\"schemaVersion\": 1", "\"schemaVersion\": 2"));
            Assert.IsTrue(DataTestFixtures.Find(unsupported, expected, kind).Any(i => i.Message.Contains("지원하지 않는 schemaVersion")), DataTestFixtures.Dump(unsupported));
        }

        [Test]
        public void TrailingContent_IsAParseError()
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Weapons, DataTestFixtures.Weapons + " {}"));
            Assert.IsFalse(report.IsPlayable);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.Weapons).Any(i => i.Message.Contains("JSON 문법 오류")));
        }

        [Test]
        public void EmptyText_DoesNotThrow()
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Monsters, string.Empty));
            Assert.IsFalse(report.IsPlayable);
        }

        [Test]
        public void AllFilesMissing_DoesNotThrow_AndBlocks()
        {
            DataTestFixtures.IgnoreErrorLogs();
            var (data, report) = GameDataLoader.LoadFromTexts(new System.Collections.Generic.Dictionary<DataFileKind, string>());
            Assert.IsFalse(report.IsPlayable);
            Assert.AreEqual(0, data.Weapons.Count);
            Assert.IsTrue(data.LevelUpPool.IsFallback);
            Assert.AreEqual(4 + 4, report.BlockingIssues.Count(), DataTestFixtures.Dump(report));
        }

        [Test]
        public void CommentsAreIgnored()
        {
            var (_, report) = DataTestFixtures.Load(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"defaultWeight\": 10,", "/* note */ \"defaultWeight\": 10, // tail\n"));
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
        }

        private static void AssertOptionalFallback(DataFileKind kind, GameData data, DataValidationReport report)
        {
            Assert.IsTrue(report.IsPlayable, DataTestFixtures.Dump(report));
            switch (kind)
            {
                case DataFileKind.Combinations:
                    Assert.AreEqual(0, data.Combinations.Count);
                    break;
                case DataFileKind.Passives:
                    Assert.AreEqual(0, data.Passives.Count);
                    Assert.AreEqual(0, data.Combinations.Count, "without passives every rule is a reference error");
                    break;
                case DataFileKind.LevelUpPool:
                    Assert.AreSame(LevelUpPoolDefinition.UniformFallback, data.LevelUpPool);
                    Assert.AreEqual(1f, data.LevelUpPool.DefaultWeight);
                    Assert.AreEqual(1f, data.LevelUpPool.HealBaseWeight);
                    Assert.AreEqual(0, data.LevelUpPool.Rules.Count);
                    break;
            }
        }
    }
}
