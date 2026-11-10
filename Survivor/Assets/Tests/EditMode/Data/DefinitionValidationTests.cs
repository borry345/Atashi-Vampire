using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Data;
using UnityEngine.TestTools.Constraints;

namespace Survivor.Tests.EditMode.Data
{
    /// Definition-level validation per file: missing field / type error / duplicate id / reference error / unknown key (5c.5 matrix).
    public sealed class DefinitionValidationTests
    {
        private static (GameData data, DataValidationReport report) LoadExpecting(
            Dictionary<DataFileKind, string> texts, IssueSeverity severity, DataFileKind kind, string definitionId, string fieldPath)
        {
            var result = DataTestFixtures.Load(texts);
            Assert.IsTrue(DataTestFixtures.Has(result.report, severity, kind, definitionId, fieldPath),
                "expected " + severity + " " + kind + " " + definitionId + "." + fieldPath + "\n" + DataTestFixtures.Dump(result.report));
            return result;
        }

        // ---------------- weapons.json ----------------

        [Test]
        public void Weapons_MissingField_SkipsDefinition_AndNamesTheField()
        {
            var texts = DataTestFixtures.With(DataFileKind.Weapons, "\"cooldownSeconds\": 2.4, \"zoneRadius\": 1.75, ", "\"cooldownSeconds\": 2.4, ");
            var (data, report) = LoadExpecting(texts, IssueSeverity.Warning, DataFileKind.Weapons, "W3", "levels[3].zoneRadius");
            Assert.IsFalse(data.TryGetWeapon("W3", out _));
            Assert.AreEqual(5, data.Weapons.Count);
            Assert.IsTrue(report.IsPlayable);
            ValidationIssue issue = report.Issues.First(i => i.FieldPath == "levels[3].zoneRadius");
            StringAssert.Contains("필수 필드가 없습니다", issue.Message);
            StringAssert.StartsWith("[weapons.json:", issue.ToString());
            StringAssert.Contains("W3.levels[3].zoneRadius", issue.ToString());
            Assert.Greater(issue.Line, 0);
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Weapons, "W3", null), "skip summary");
        }

        [Test]
        public void Weapons_TypeErrors_SkipDefinition()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"displayName\": \"유도 투사체\"", "\"displayName\": 42"),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "displayName");
            Assert.IsFalse(data.TryGetWeapon("W4", out _));

            var (data2, report2) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"damage\": 14, \"cooldownSeconds\": 1.2", "\"damage\": 14, \"pierceCount\": 1.5, \"cooldownSeconds\": 1.2"),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "levels[3].pierceCount");
            Assert.IsFalse(data2.TryGetWeapon("W4", out _), "a real number in an int field is a type error");
            StringAssert.Contains("정수여야 합니다", report2.Issues.First(i => i.FieldPath == "levels[3].pierceCount").Message);
        }

        [Test]
        public void Weapons_RangeAndEnumAndLevelCount_SkipDefinition()
        {
            LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"damage\": 8,  \"cooldownSeconds\": 1.5", "\"damage\": 8,  \"cooldownSeconds\": -1"),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "levels[0].cooldownSeconds");
            LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"behavior\": \"homing\"", "\"behavior\": \"laser\""),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "behavior");
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"assetKey\": \"shape.projectile.pink\"", "\"assetKey\": \"shape.projectile.pink\", \"evolvedOnly\": true"),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "levels");
            Assert.IsFalse(data.TryGetWeapon("W4", out _), "evolved weapons need exactly 1 level");
        }

        [Test]
        public void Weapons_DuplicateId_SkipsSecond()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"id\": \"W4\"", "\"id\": \"W1\""),
                IssueSeverity.Warning, DataFileKind.Weapons, "W1", "id");
            CollectionAssert.AreEqual(new[] { "W1", "W2", "W3", "E1", "E2" }, data.Weapons.Select(w => w.Id).ToArray());
            Assert.AreEqual("투사체", data.Weapons[0].DisplayName, "the first definition wins");
            StringAssert.Contains("중복", report.Issues.First(i => i.FieldPath == "id").Message);
        }

        [Test]
        public void Weapons_InvalidIdPattern_IsReportedByIndex()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"id\": \"W4\"", "\"id\": \"W-4\""),
                IssueSeverity.Warning, DataFileKind.Weapons, null, "weapons[3].id");
            Assert.AreEqual(5, data.Weapons.Count);
        }

        [Test]
        public void Weapons_UnknownKey_WarnsAndKeepsDefinition()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"assetKey\": \"shape.projectile.pink\"", "\"assetKey\": \"shape.projectile.pink\", \"colour\": \"red\""),
                IssueSeverity.Warning, DataFileKind.Weapons, "W4", "colour");
            Assert.IsTrue(data.TryGetWeapon("W4", out _));
            Assert.AreEqual(1, report.Issues.Count, DataTestFixtures.Dump(report));
        }

        [Test]
        public void Weapons_KeyOfAnotherBehavior_WarnsAndKeepsDefinition()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Weapons, "\"damage\": 8,  \"orbCount\": 2", "\"damage\": 8,  \"projectileSpeed\": 3, \"orbCount\": 2"),
                IssueSeverity.Warning, DataFileKind.Weapons, "W2", "levels[0].projectileSpeed");
            Assert.IsTrue(data.TryGetWeapon("W2", out _));
            StringAssert.Contains("orbit", report.Issues[0].Message);
        }

        [Test]
        public void Weapons_ReferenceError_StartingWeaponMissingOrEvolved_Blocks()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.PlayerStats, "\"startingWeaponId\": \"W1\"", "\"startingWeaponId\": \"W9\""),
                IssueSeverity.Blocking, DataFileKind.PlayerStats, null, "startingWeaponId");
            Assert.IsNull(data.PlayerStats);
            Assert.IsFalse(report.IsPlayable);

            var (_, evolved) = LoadExpecting(DataTestFixtures.With(DataFileKind.PlayerStats, "\"startingWeaponId\": \"W1\"", "\"startingWeaponId\": \"E1\""),
                IssueSeverity.Blocking, DataFileKind.PlayerStats, null, "startingWeaponId");
            Assert.IsFalse(evolved.IsPlayable);
        }

        // ---------------- monsters.json ----------------

        [Test]
        public void Monsters_MissingNestedField_SkipsDefinition()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"charge\": { \"triggerDistance\": 6, ", "\"charge\": { "),
                IssueSeverity.Warning, DataFileKind.Monsters, "M2", "charge.triggerDistance");
            Assert.IsFalse(data.TryGetMonster("M2", out _));
            Assert.IsTrue(report.IsPlayable, "M1 and M3 are still spawnable");
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[1].entries[1].monsterId"),
                "spawn entries pointing at the skipped monster become reference errors");
        }

        [Test]
        public void Monsters_TypeError_CascadesToReferences()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"maxHealth\": 10, \"moveSpeed\": 2.0", "\"maxHealth\": \"10\", \"moveSpeed\": 2.0"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M1", "maxHealth");
            Assert.IsFalse(data.TryGetMonster("M1", out _));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Monsters, "M4", "bossPatterns[1].monsterId"), "boss summons M1");
            Assert.IsFalse(data.TryGetMonster("M4", out _));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.SpawnTable, null, "bossMonsterId"));
            Assert.IsNull(data.SpawnTable.BossMonsterId);
            Assert.AreEqual(3, data.SpawnTable.Segments.Count, "the M1-only segment is skipped");
            Assert.IsTrue(report.IsPlayable, "boss problems are warnings only (D6)");
        }

        [Test]
        public void Monsters_DuplicateId_SkipsSecond()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"id\": \"M3\"", "\"id\": \"M1\""),
                IssueSeverity.Warning, DataFileKind.Monsters, "M1", "id");
            CollectionAssert.AreEqual(new[] { "M1", "M2", "M4" }, data.Monsters.Select(m => m.Id).ToArray());
            Assert.AreEqual(MonsterBehavior.Chaser, data.Monsters[0].Behavior);
        }

        [Test]
        public void Monsters_UnknownKeys_Warn()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"contactDamage\": 4,", "\"contactDamage\": 4, \"armor\": 3,"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M3", "armor");
            Assert.IsTrue(data.TryGetMonster("M3", out _));

            var (data2, report2) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"radius\": 0.45, \"contactDamage\": 5 }", "\"radius\": 0.45, \"contactDamage\": 5, \"ranged\": { } }"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M1", "ranged");
            Assert.IsTrue(data2.TryGetMonster("M1", out MonsterDefinition m1));
            Assert.IsNull(m1.Ranged);
            StringAssert.Contains("chaser", report2.Issues[0].Message);
        }

        [Test]
        public void Monsters_SummonReferenceErrors_SkipBoss_WarningOnly()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"monsterId\": \"M1\", \"count\": 6", "\"monsterId\": \"M9\", \"count\": 6"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M4", "bossPatterns[1].monsterId");
            Assert.IsFalse(data.TryGetMonster("M4", out _));
            Assert.IsTrue(report.IsPlayable);

            LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"monsterId\": \"M1\", \"count\": 6", "\"monsterId\": \"M4\", \"count\": 6"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M4", "bossPatterns[1].monsterId");
        }

        [Test]
        public void Monsters_NegativeRadius_SkipsDefinition()
        {
            LoadExpecting(DataTestFixtures.With(DataFileKind.Monsters, "\"maxHealth\": 15, \"moveSpeed\": 1.8, \"radius\": 0.45", "\"maxHealth\": 15, \"moveSpeed\": 1.8, \"radius\": -0.45"),
                IssueSeverity.Warning, DataFileKind.Monsters, "M3", "radius");
        }

        // ---------------- spawnTable.json ----------------

        [Test]
        public void SpawnTable_MissingTableField_Blocks()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"despawnDistance\": 45, ", string.Empty),
                IssueSeverity.Blocking, DataFileKind.SpawnTable, null, "despawnDistance");
            Assert.IsNull(data.SpawnTable);
            Assert.IsFalse(report.IsPlayable);

            LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"despawnDistance\": 45, ", "\"despawnDistance\": 10, "),
                IssueSeverity.Blocking, DataFileKind.SpawnTable, null, "despawnDistance");
        }

        [Test]
        public void SpawnTable_EntryTypeError_SkipsEntryAndEmptySegment()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "{ \"monsterId\": \"M1\", \"weight\": 1 }", "{ \"monsterId\": \"M1\", \"weight\": \"1\" }"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[0].entries[0].weight");
            Assert.AreEqual(3, data.SpawnTable.Segments.Count);
            Assert.AreEqual(7200, data.SpawnTable.Segments[0].StartStep);
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void SpawnTable_ReferenceErrors_UnknownAndBossEntries()
        {
            LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "{ \"monsterId\": \"M1\", \"weight\": 1 }", "{ \"monsterId\": \"M9\", \"weight\": 1 }"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[0].entries[0].monsterId");
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "{ \"monsterId\": \"M1\", \"weight\": 1 }", "{ \"monsterId\": \"M4\", \"weight\": 1 }"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[0].entries[0].monsterId");
            Assert.IsFalse(data.SpawnTable.Segments.SelectMany(s => s.Entries).Any(e => e.MonsterId == "M4"), "boss never enters normal spawns");
        }

        [Test]
        public void SpawnTable_BossIdNotABoss_WarnsOnly()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"bossMonsterId\": \"M4\"", "\"bossMonsterId\": \"M1\""),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "bossMonsterId");
            Assert.IsNull(data.SpawnTable.BossMonsterId);
            Assert.IsTrue(report.IsPlayable);

            var (data2, report2) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"bossMonsterId\": \"M4\",", string.Empty),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "bossMonsterId");
            Assert.IsNull(data2.SpawnTable.BossMonsterId);
            Assert.IsTrue(report2.IsPlayable);
        }

        [Test]
        public void SpawnTable_EndPast600_IsClamped()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"startSeconds\": 480, \"endSeconds\": 600", "\"startSeconds\": 480, \"endSeconds\": 700"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[3].endSeconds");
            Assert.AreEqual(36000, data.SpawnTable.Segments[3].EndStep);
            Assert.AreEqual(600f, data.SpawnTable.Segments[3].EndSeconds);
        }

        [Test]
        public void SpawnTable_OverlappingSegment_IsSkipped()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"startSeconds\": 120, \"endSeconds\": 300", "\"startSeconds\": 100, \"endSeconds\": 300"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[1]");
            Assert.AreEqual(3, data.SpawnTable.Segments.Count);
            Assert.AreEqual(18000, data.SpawnTable.Segments[1].StartStep);
        }

        [Test]
        public void SpawnTable_UnknownKey_Warns()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.SpawnTable, "\"despawnDistance\": 45,", "\"despawnDistance\": 45, \"spawnRadius\": 20,"),
                IssueSeverity.Warning, DataFileKind.SpawnTable, null, "spawnRadius");
            Assert.IsNotNull(data.SpawnTable);
            Assert.IsTrue(report.IsPlayable);
        }

        // ---------------- spawnTable.json: hpMultiplier (5.13.4, TAC-F3-13) and R31 HpMultiplierAt (TAC-F3-14) ----------------

        private const string Seg0 = "\"endSeconds\": 120, \"spawnsPerSecond\": 2, ";
        private const string Seg1 = "\"endSeconds\": 300, \"spawnsPerSecond\": 4, ";
        private const string Seg2 = "\"endSeconds\": 480, \"spawnsPerSecond\": 7, ";
        private const string Seg3 = "\"endSeconds\": 600, \"spawnsPerSecond\": 10,";

        /// The fixture table with multipliers 1.0 / 1.5 / 2.2 / 3.0 and segment 1's value replaced by a raw JSON literal.
        private static Dictionary<DataFileKind, string> MultiplierTexts(string segment1Literal = "1.5")
        {
            string text = DataTestFixtures.SpawnTable;
            text = DataTestFixtures.ReplaceOnce(text, Seg0, Seg0 + " \"hpMultiplier\": 1.0, ");
            text = DataTestFixtures.ReplaceOnce(text, Seg1, Seg1 + " \"hpMultiplier\": " + segment1Literal + ", ");
            text = DataTestFixtures.ReplaceOnce(text, Seg2, Seg2 + " \"hpMultiplier\": 2.2, ");
            text = DataTestFixtures.ReplaceOnce(text, Seg3, Seg3 + " \"hpMultiplier\": 3.0, ");
            return DataTestFixtures.WithText(DataFileKind.SpawnTable, text);
        }

        private static SpawnTableDefinition LoadTable(string spawnTableText)
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.SpawnTable, spawnTableText));
            Assert.IsNotNull(data.SpawnTable, DataTestFixtures.Dump(report));
            return data.SpawnTable;
        }

        [Test]
        public void SpawnTable_HpMultiplierOmitted_IsOne()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.AreEqual(4, data.SpawnTable.Segments.Count);
            Assert.IsTrue(data.SpawnTable.Segments.All(s => s.HpMultiplier == 1f));
            Assert.AreEqual(0, DataTestFixtures.Find(report, IssueSeverity.Warning, DataFileKind.SpawnTable).Count, DataTestFixtures.Dump(report));
        }

        [Test]
        public void SpawnTable_HpMultiplierValid_IsBoundWithoutIssues()
        {
            var (data, report) = DataTestFixtures.Load(MultiplierTexts());
            Assert.AreEqual(0, report.Issues.Count, "hpMultiplier is a known key\n" + DataTestFixtures.Dump(report));
            CollectionAssert.AreEqual(new[] { 1.0f, 1.5f, 2.2f, 3.0f }, data.SpawnTable.Segments.Select(s => s.HpMultiplier).ToArray());
        }

        [TestCase("0")]
        [TestCase("-1.5")]
        [TestCase("\"1.5\"")]
        [TestCase("null")]
        [TestCase("true")]
        [TestCase("1e39")]
        public void SpawnTable_HpMultiplierInvalid_WarnsAndOnlyThatSegmentFallsBackToOne(string literal)
        {
            var (data, report) = DataTestFixtures.Load(MultiplierTexts(literal));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.SpawnTable, null, "segments[1].hpMultiplier"), DataTestFixtures.Dump(report));
            Assert.IsTrue(report.IsPlayable);
            Assert.AreEqual(0, report.Issues.Count(i => i.Severity == IssueSeverity.Blocking), DataTestFixtures.Dump(report));
            Assert.IsTrue(report.Issues.All(i => i.FieldPath == "segments[1].hpMultiplier"), "the segment is not skipped and nothing else is reported\n" + DataTestFixtures.Dump(report));

            Assert.AreEqual(4, data.SpawnTable.Segments.Count);
            SpawnSegmentDefinition segment = data.SpawnTable.Segments[1];
            Assert.AreEqual(1f, segment.HpMultiplier);
            Assert.AreEqual(4f, segment.SpawnsPerSecond);
            Assert.AreEqual(7200, segment.StartStep);
            Assert.AreEqual(18000, segment.EndStep);
            Assert.AreEqual(2, segment.Entries.Count);
            Assert.AreEqual(1.0f, data.SpawnTable.Segments[0].HpMultiplier);
            Assert.AreEqual(2.2f, data.SpawnTable.Segments[2].HpMultiplier);
            Assert.AreEqual(3.0f, data.SpawnTable.Segments[3].HpMultiplier);
        }

        [Test]
        public void SpawnTable_HpMultiplierInvalid_MessageNamesFileSegmentAndField()
        {
            var (_, report) = DataTestFixtures.Load(MultiplierTexts("0"));
            List<ValidationIssue> issues = report.Issues.Where(i => i.FieldPath == "segments[1].hpMultiplier").ToList();
            Assert.Greater(issues.Count, 0, DataTestFixtures.Dump(report));
            Assert.IsTrue(issues.All(i => i.FileName == "spawnTable.json"));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("1.0")), "the fallback value is stated\n" + DataTestFixtures.Dump(report));
            StringAssert.Contains("segments[1].hpMultiplier", issues[0].ToString());
        }

        [TestCase(0L, 1.0f)]
        [TestCase(7199L, 1.0f)]
        [TestCase(7200L, 1.5f)]
        [TestCase(17999L, 1.5f)]
        [TestCase(18000L, 2.2f)]
        [TestCase(28799L, 2.2f)]
        [TestCase(28800L, 3.0f)]
        [TestCase(35999L, 3.0f)]
        [TestCase(36000L, 3.0f)]
        [TestCase(1000000L, 3.0f)]
        [TestCase(-1L, 1.0f)]
        public void R31_HpMultiplierAt_ContiguousTable(long step, float expected)
        {
            var (data, report) = DataTestFixtures.Load(MultiplierTexts());
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
            Assert.AreEqual(expected, data.SpawnTable.HpMultiplierAt(step), "exact value, no rounding");
        }

        private const string GapTable = @"{ ""schemaVersion"": 1, ""despawnDistance"": 45, ""bossMonsterId"": ""M4"",
  ""segments"": [
    { ""startSeconds"": 60,  ""endSeconds"": 120, ""spawnsPerSecond"": 2, ""hpMultiplier"": 1.5, ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 1 } ] },
    { ""startSeconds"": 180, ""endSeconds"": 300, ""spawnsPerSecond"": 2, ""hpMultiplier"": 2.25, ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 1 } ] } ] }";

        [TestCase(0L, 1.0f)]
        [TestCase(3599L, 1.0f)]
        [TestCase(3600L, 1.5f)]
        [TestCase(7199L, 1.5f)]
        [TestCase(7200L, 1.5f)]
        [TestCase(10799L, 1.5f)]
        [TestCase(10800L, 2.25f)]
        [TestCase(17999L, 2.25f)]
        [TestCase(18000L, 2.25f)]
        [TestCase(36000L, 2.25f)]
        public void R31_HpMultiplierAt_GapAndLateFirstSegment(long step, float expected)
        {
            SpawnTableDefinition table = LoadTable(GapTable);
            Assert.AreEqual(2, table.Segments.Count);
            Assert.AreEqual(expected, table.HpMultiplierAt(step));
        }

        [Test]
        public void R31_HpMultiplierAt_NoSegments_IsOne()
        {
            // The only segment is skipped (spawnsPerSecond 0), so the table is valid but has no segments.
            SpawnTableDefinition table = LoadTable(@"{ ""schemaVersion"": 1, ""despawnDistance"": 45, ""bossMonsterId"": ""M4"",
  ""segments"": [ { ""startSeconds"": 0, ""endSeconds"": 600, ""spawnsPerSecond"": 0, ""hpMultiplier"": 2.0, ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 1 } ] } ] }");
            Assert.AreEqual(0, table.Segments.Count);
            Assert.AreEqual(1f, table.HpMultiplierAt(0));
            Assert.AreEqual(1f, table.HpMultiplierAt(36000));
        }

        [Test]
        public void R31_HpMultiplierAt_IsRepeatable_AndDoesNotAllocate()
        {
            SpawnTableDefinition table = LoadTable(GapTable);
            float first = table.HpMultiplierAt(9000);
            Assert.AreEqual(first, table.HpMultiplierAt(9000));
            float sink = 0f;
            Assert.That(() =>
            {
                for (long step = 0; step < 40000; step += 97)
                {
                    sink += table.HpMultiplierAt(step);
                }
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.Greater(sink, 0f);
        }

        // ---------------- passives.json ----------------

        [Test]
        public void Passives_MissingField_SkipsDefinition()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Passives, "\"iconKey\": \"icon.p2\", \"stat\": \"moveSpeed\",", "\"iconKey\": \"icon.p2\","),
                IssueSeverity.Warning, DataFileKind.Passives, "P2", "stat");
            Assert.IsFalse(data.TryGetPassive("P2", out _));
        }

        [Test]
        public void Passives_TypeError_SkipsDefinition()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Passives, "\"displayName\": \"공격 속도\"", "\"displayName\": 1"),
                IssueSeverity.Warning, DataFileKind.Passives, "P1", "displayName");
            Assert.IsFalse(data.TryGetPassive("P1", out _));
        }

        [Test]
        public void Passives_WrongLevelCount_SkipsDefinition_AndBreaksCombination()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Passives, "{ \"bonus\": 0.40 }, { \"bonus\": 0.50 } ] } ] }", "{ \"bonus\": 0.40 } ] } ] }"),
                IssueSeverity.Warning, DataFileKind.Passives, "P3", "levels");
            Assert.IsFalse(data.TryGetPassive("P3", out _));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W2", "passiveId"));
            CollectionAssert.AreEqual(new[] { "evolve_W1" }, data.Combinations.Select(c => c.Id).ToArray());
        }

        [Test]
        public void Passives_DuplicateId_And_UnknownKey()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Passives, "\"id\": \"P2\"", "\"id\": \"P1\""),
                IssueSeverity.Warning, DataFileKind.Passives, "P1", "id");
            CollectionAssert.AreEqual(new[] { "P1", "P3" }, data.Passives.Select(p => p.Id).ToArray());

            var (data2, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Passives, "\"iconKey\": \"icon.p1\",", "\"iconKey\": \"icon.p1\", \"maxLevel\": 5,"),
                IssueSeverity.Warning, DataFileKind.Passives, "P1", "maxLevel");
            Assert.IsTrue(data2.TryGetPassive("P1", out _));
        }

        // ---------------- combinations.json ----------------

        [Test]
        public void Combinations_MissingAndTypeErrors_SkipRule()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Combinations, ", \"resultWeaponId\": \"E2\"", string.Empty),
                IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W2", "resultWeaponId");
            Assert.AreEqual(1, data.Combinations.Count);

            LoadExpecting(DataTestFixtures.With(DataFileKind.Combinations, "\"weaponId\": \"W2\"", "\"weaponId\": 2"),
                IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W2", "weaponId");
        }

        [Test]
        public void Combinations_DuplicateId_And_UnknownKey()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Combinations, "\"id\": \"evolve_W2\"", "\"id\": \"evolve_W1\""),
                IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W1", "id");
            Assert.AreEqual(1, data.Combinations.Count);

            var (data2, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.Combinations, "\"resultWeaponId\": \"E1\"", "\"resultWeaponId\": \"E1\", \"note\": \"x\""),
                IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W1", "note");
            Assert.AreEqual(2, data2.Combinations.Count);
        }

        [TestCase("\"weaponId\": \"W2\"", "\"weaponId\": \"W9\"", "weaponId")]
        [TestCase("\"weaponId\": \"W2\"", "\"weaponId\": \"E1\"", "weaponId")]
        [TestCase("\"passiveId\": \"P3\"", "\"passiveId\": \"P9\"", "passiveId")]
        [TestCase("\"resultWeaponId\": \"E2\"", "\"resultWeaponId\": \"W3\"", "resultWeaponId")]
        [TestCase("\"weaponId\": \"W2\"", "\"weaponId\": \"W1\"", "weaponId")]
        public void Combinations_ReferenceErrors_SkipRule(string oldText, string newText, string field)
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.Combinations, oldText, newText),
                IssueSeverity.Warning, DataFileKind.Combinations, "evolve_W2", field);
            CollectionAssert.AreEqual(new[] { "evolve_W1" }, data.Combinations.Select(c => c.Id).ToArray());
            Assert.IsTrue(report.IsPlayable);
        }

        // ---------------- levelUpPool.json ----------------

        [Test]
        public void LevelUpPool_MissingTopField_FallsBack()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"defaultWeight\": 10, ", string.Empty),
                IssueSeverity.Warning, DataFileKind.LevelUpPool, null, "defaultWeight");
            Assert.IsTrue(data.LevelUpPool.IsFallback);
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void LevelUpPool_RuleErrors_SkipRule()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"targetId\": \"W1\", \"weight\": 12", "\"targetId\": \"W1\", \"weight\": \"12\""),
                IssueSeverity.Warning, DataFileKind.LevelUpPool, null, "rules[1].weight");
            Assert.AreEqual(1, data.LevelUpPool.Rules.Count);
            Assert.IsFalse(data.LevelUpPool.IsFallback);

            LoadExpecting(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"kind\": \"weaponUpgrade\"", "\"kind\": \"heal\""),
                IssueSeverity.Warning, DataFileKind.LevelUpPool, null, "rules[1].kind");

            var (data3, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"targetId\": \"W1\"", "\"targetId\": \"W9\""),
                IssueSeverity.Warning, DataFileKind.LevelUpPool, null, "rules[1].targetId");
            Assert.AreEqual(1, data3.LevelUpPool.Rules.Count);
        }

        [Test]
        public void LevelUpPool_UnknownKey_Warns()
        {
            var (data, _) = LoadExpecting(DataTestFixtures.With(DataFileKind.LevelUpPool, "\"healBaseWeight\": 30,", "\"healBaseWeight\": 30, \"healRatio\": 0.3,"),
                IssueSeverity.Warning, DataFileKind.LevelUpPool, null, "healRatio");
            Assert.IsFalse(data.LevelUpPool.IsFallback);
        }

        // ---------------- playerStats.json ----------------

        [Test]
        public void PlayerStats_MissingInvulnerability_Blocks()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.PlayerStats, "\"invulnerabilitySeconds\": 0.5, ", string.Empty),
                IssueSeverity.Blocking, DataFileKind.PlayerStats, null, "invulnerabilitySeconds");
            Assert.IsNull(data.PlayerStats, "no default substitution");
            Assert.IsFalse(report.IsPlayable);
        }

        [TestCase("\"gemXpValue\": 1,", "\"gemXpValue\": 1.5,", "pickups.gemXpValue")]
        [TestCase("\"invulnerabilitySeconds\": 0.5", "\"invulnerabilitySeconds\": -0.5", "invulnerabilitySeconds")]
        [TestCase("\"maxHealth\": 100", "\"maxHealth\": \"100\"", "maxHealth")]
        [TestCase("[5, 8, 12, 16, 20, 25, 30]", "[]", "xpCurve.thresholds")]
        [TestCase("[5, 8, 12, 16, 20, 25, 30]", "[0, 8]", "xpCurve.thresholds[0]")]
        [TestCase(", \"chestRadius\": 0.5", "", "pickups.chestRadius")]
        public void PlayerStats_AnyError_Blocks(string oldText, string newText, string field)
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.PlayerStats, oldText, newText),
                IssueSeverity.Blocking, DataFileKind.PlayerStats, null, field);
            Assert.IsNull(data.PlayerStats);
            Assert.IsFalse(report.IsPlayable);
        }

        [Test]
        public void PlayerStats_UnknownKey_WarnsOnly()
        {
            var (data, report) = LoadExpecting(DataTestFixtures.With(DataFileKind.PlayerStats, "\"maxHealth\": 100,", "\"maxHealth\": 100, \"luck\": 7,"),
                IssueSeverity.Warning, DataFileKind.PlayerStats, null, "luck");
            Assert.IsNotNull(data.PlayerStats);
            Assert.IsTrue(report.IsPlayable);
        }
    }
}
