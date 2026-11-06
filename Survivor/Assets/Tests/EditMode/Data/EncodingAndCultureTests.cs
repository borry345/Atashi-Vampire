using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Tests.EditMode.Data
{
    /// UTF-8 reading with U+FFFD detection (TAC-F11-6, CP949 fixture) and culture-independent number parsing.
    public sealed class EncodingAndCultureTests
    {
        private string _tempRoot;

        private static string FixturePath(string fileName)
        {
            return Path.Combine(Application.dataPath, "Tests", "EditMode", "Data", "Fixtures", fileName);
        }

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "SurvivorDataTests_" + TestContext.CurrentContext.Test.MethodName);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }

            Directory.CreateDirectory(Path.Combine(_tempRoot, GameDataLoader.DataFolder));
            var utf8 = new UTF8Encoding(false);
            foreach (DataFileKind kind in DataTestFixtures.AllKinds)
            {
                File.WriteAllText(Path.Combine(_tempRoot, GameDataLoader.DataFolder, GameDataLoader.FileNameOf(kind)), DataTestFixtures.ValidTexts()[kind], utf8);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempRoot != null && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void Cp949File_WarnsEncoding_AndContinues()
        {
            byte[] bytes = File.ReadAllBytes(FixturePath("weapons_cp949.json"));
            Assert.IsTrue(bytes.Any(b => b >= 0x80), "fixture must contain non-ASCII CP949 bytes");
            File.WriteAllBytes(Path.Combine(_tempRoot, GameDataLoader.DataFolder, "weapons.json"), bytes);

            var (data, report) = GameDataLoader.LoadAll(_tempRoot);
            ValidationIssue issue = DataTestFixtures.Find(report, IssueSeverity.Warning, DataFileKind.Weapons)
                .FirstOrDefault(i => i.Message.Contains("인코딩 오류 가능(UTF-8이 아닌 파일일 수 있습니다)"));
            Assert.IsNotNull(issue, DataTestFixtures.Dump(report));
            Assert.Greater(issue.Line, 0);
            Assert.IsTrue(report.IsPlayable, "encoding problems never block");
            Assert.IsTrue(data.TryGetWeapon("W1", out WeaponDefinition w1));
            StringAssert.Contains("�", w1.DisplayName);
        }

        [Test]
        public void ReplacementCharacterInText_Warns()
        {
            var texts = DataTestFixtures.With(DataFileKind.Monsters, "\"displayName\": \"좀비\"", "\"displayName\": \"��\"");
            var (_, report) = DataTestFixtures.Load(texts);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Warning, DataFileKind.Monsters).Any(i => i.Message.Contains("인코딩 오류 가능")));
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void LoadAll_ReadsUtf8Files_AndBom()
        {
            string path = Path.Combine(_tempRoot, GameDataLoader.DataFolder, "passives.json");
            File.WriteAllText(path, DataTestFixtures.Passives, new UTF8Encoding(true));
            var (data, report) = GameDataLoader.LoadAll(_tempRoot);
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
            Assert.IsTrue(data.TryGetPassive("P1", out PassiveDefinition p1));
            Assert.AreEqual("공격 속도", p1.DisplayName);
        }

        [Test]
        public void LoadAll_MissingFile_UsesFilePolicy()
        {
            File.Delete(Path.Combine(_tempRoot, GameDataLoader.DataFolder, "combinations.json"));
            File.Delete(Path.Combine(_tempRoot, GameDataLoader.DataFolder, "monsters.json"));
            DataTestFixtures.IgnoreErrorLogs();
            var (data, report) = GameDataLoader.LoadAll(_tempRoot);
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Warning, DataFileKind.Combinations).Any(i => i.Message.Contains("파일이 없습니다")));
            Assert.IsTrue(DataTestFixtures.Find(report, IssueSeverity.Blocking, DataFileKind.Monsters).Any(i => i.Message.Contains("파일이 없습니다")));
            Assert.AreEqual(0, data.Combinations.Count);
            Assert.IsFalse(report.IsPlayable);
        }

        [Test]
        public void LoadAll_NullRoot_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GameDataLoader.LoadAll(null));
        }

        [TestCase("tr-TR")]
        [TestCase("de-DE")]
        public void NumberParsing_IgnoresCurrentCulture(string cultureName)
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            CultureInfo previousUi = Thread.CurrentThread.CurrentUICulture;
            try
            {
                var culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                var texts = DataTestFixtures.With(DataFileKind.Weapons, "\"cooldownSeconds\": 2.4, \"zoneRadius\": 1.75, ", "\"cooldownSeconds\": 2.4, ");

                var (data, report) = DataTestFixtures.Load(texts);
                Assert.IsTrue(data.TryGetWeapon("W1", out WeaponDefinition w1));
                Assert.AreEqual(0.25f, w1.Levels[0].ProjectileRadius);
                Assert.AreEqual(54, w1.Levels[1].CooldownSteps, "0.9 s");
                Assert.AreEqual(0.5f, data.PlayerStats.InvulnerabilitySeconds);
                Assert.AreEqual(0.10f, data.Passives[0].Levels[0].Bonus);
                Assert.AreEqual(1.5f, data.Monsters[1].MoveSpeed);
                ValidationIssue issue = report.Issues.First(i => i.FieldPath == "levels[3].zoneRadius");
                StringAssert.IsMatch(@"^\[weapons\.json:\d+:\d+\] W3\.levels\[3\]\.zoneRadius: ", issue.ToString());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
                Thread.CurrentThread.CurrentUICulture = previousUi;
            }
        }
    }
}
