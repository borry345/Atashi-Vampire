using System;
using System.Collections.Generic;
using System.Linq;
using Survivor.Data;
using UnityEngine.TestTools;

namespace Survivor.Tests.EditMode.Data
{
    /// In-memory copies of the 5.13 example files plus helpers to mutate them and query reports.
    internal static class DataTestFixtures
    {
        internal const string Weapons = @"{
  ""schemaVersion"": 1,
  ""weapons"": [
    { ""id"": ""W1"", ""displayName"": ""투사체"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.white"",
      ""levels"": [
        { ""damage"": 10, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
        { ""damage"": 12, ""cooldownSeconds"": 0.9, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
        { ""damage"": 14, ""cooldownSeconds"": 0.8, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
        { ""damage"": 16, ""cooldownSeconds"": 0.7, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 2, ""spreadDegrees"": 10 },
        { ""damage"": 20, ""cooldownSeconds"": 0.6, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 2, ""spreadDegrees"": 10 } ] },
    { ""id"": ""W2"", ""displayName"": ""근접 회전"", ""behavior"": ""orbit"", ""assetKey"": ""shape.orb.cyan"",
      ""levels"": [
        { ""damage"": 8,  ""orbCount"": 2, ""orbRadius"": 0.4,  ""orbitRadius"": 2.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 10, ""orbCount"": 2, ""orbRadius"": 0.4,  ""orbitRadius"": 2.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 12, ""orbCount"": 3, ""orbRadius"": 0.45, ""orbitRadius"": 2.2, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.45 },
        { ""damage"": 14, ""orbCount"": 3, ""orbRadius"": 0.45, ""orbitRadius"": 2.4, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.45 },
        { ""damage"": 16, ""orbCount"": 3, ""orbRadius"": 0.5,  ""orbitRadius"": 2.5, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.4 } ] },
    { ""id"": ""W3"", ""displayName"": ""장판"", ""behavior"": ""zone"", ""assetKey"": ""shape.zone.green"",
      ""levels"": [
        { ""damage"": 6,  ""cooldownSeconds"": 3.0, ""zoneRadius"": 1.5,  ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 8,  ""cooldownSeconds"": 2.8, ""zoneRadius"": 1.5,  ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 10, ""cooldownSeconds"": 2.6, ""zoneRadius"": 1.75, ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 12, ""cooldownSeconds"": 2.4, ""zoneRadius"": 1.75, ""durationSeconds"": 2.5, ""hitIntervalSeconds"": 0.5 },
        { ""damage"": 15, ""cooldownSeconds"": 2.2, ""zoneRadius"": 2.0,  ""durationSeconds"": 2.5, ""hitIntervalSeconds"": 0.5 } ] },
    { ""id"": ""W4"", ""displayName"": ""유도 투사체"", ""behavior"": ""homing"", ""assetKey"": ""shape.projectile.pink"",
      ""levels"": [
        { ""damage"": 8,  ""cooldownSeconds"": 1.5, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
        { ""damage"": 10, ""cooldownSeconds"": 1.4, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
        { ""damage"": 12, ""cooldownSeconds"": 1.3, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
        { ""damage"": 14, ""cooldownSeconds"": 1.2, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0, ""projectileCount"": 2, ""spreadDegrees"": 15 },
        { ""damage"": 17, ""cooldownSeconds"": 1.1, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0, ""projectileCount"": 2, ""spreadDegrees"": 15 } ] },
    { ""id"": ""E1"", ""displayName"": ""연사 투사체"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.gold"", ""evolvedOnly"": true,
      ""levels"": [ { ""damage"": 22, ""cooldownSeconds"": 0.2, ""projectileSpeed"": 14, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 3, ""spreadDegrees"": 12 } ] },
    { ""id"": ""E2"", ""displayName"": ""대형 회전"", ""behavior"": ""orbit"", ""assetKey"": ""shape.orb.gold"", ""evolvedOnly"": true,
      ""levels"": [ { ""damage"": 20, ""orbCount"": 4, ""orbRadius"": 0.6, ""orbitRadius"": 3.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.4 } ] }
  ]
}";

        internal const string Monsters = @"{
  ""schemaVersion"": 1,
  ""monsters"": [
    { ""id"": ""M1"", ""displayName"": ""좀비"", ""behavior"": ""chaser"", ""assetKey"": ""shape.monster.green"",
      ""maxHealth"": 10, ""moveSpeed"": 2.0, ""radius"": 0.45, ""contactDamage"": 5 },
    { ""id"": ""M2"", ""displayName"": ""돌진체"", ""behavior"": ""charger"", ""assetKey"": ""shape.monster.orange"",
      ""maxHealth"": 20, ""moveSpeed"": 1.5, ""radius"": 0.5, ""contactDamage"": 8,
      ""charge"": { ""triggerDistance"": 6, ""chargeSpeed"": 18, ""durationSeconds"": 0.5, ""cooldownSeconds"": 3 } },
    { ""id"": ""M3"", ""displayName"": ""원거리"", ""behavior"": ""ranged"", ""assetKey"": ""shape.monster.purple"",
      ""maxHealth"": 15, ""moveSpeed"": 1.8, ""radius"": 0.45, ""contactDamage"": 4,
      ""ranged"": { ""attackRange"": 8, ""fireCooldownSeconds"": 2,
                  ""projectile"": { ""assetKey"": ""shape.enemyshot.red"", ""speed"": 6, ""radius"": 0.2, ""damage"": 6, ""lifetimeSeconds"": 3 } } },
    { ""id"": ""M4"", ""displayName"": ""보스"", ""behavior"": ""boss"", ""assetKey"": ""shape.boss.red"",
      ""maxHealth"": 5000, ""moveSpeed"": 1.2, ""radius"": 1.5, ""contactDamage"": 20,
      ""bossPatterns"": [
        { ""type"": ""hazardZone"", ""cooldownSeconds"": 6, ""zoneRadius"": 3, ""durationSeconds"": 2, ""damage"": 15, ""assetKey"": ""shape.hazard.red"" },
        { ""type"": ""summon"", ""cooldownSeconds"": 8, ""monsterId"": ""M1"", ""count"": 6, ""summonRadius"": 3 } ] }
  ]
}";

        internal const string Combinations = @"{ ""schemaVersion"": 1, ""combinations"": [
  { ""id"": ""evolve_W1"", ""weaponId"": ""W1"", ""passiveId"": ""P1"", ""resultWeaponId"": ""E1"" },
  { ""id"": ""evolve_W2"", ""weaponId"": ""W2"", ""passiveId"": ""P3"", ""resultWeaponId"": ""E2"" } ] }";

        internal const string SpawnTable = @"{ ""schemaVersion"": 1, ""despawnDistance"": 45, ""bossMonsterId"": ""M4"",
  ""segments"": [
    { ""startSeconds"": 0,   ""endSeconds"": 120, ""spawnsPerSecond"": 2,  ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 1 } ] },
    { ""startSeconds"": 120, ""endSeconds"": 300, ""spawnsPerSecond"": 4,  ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 3 }, { ""monsterId"": ""M2"", ""weight"": 1 } ] },
    { ""startSeconds"": 300, ""endSeconds"": 480, ""spawnsPerSecond"": 7,  ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 4 }, { ""monsterId"": ""M2"", ""weight"": 2 }, { ""monsterId"": ""M3"", ""weight"": 1 } ] },
    { ""startSeconds"": 480, ""endSeconds"": 600, ""spawnsPerSecond"": 10, ""entries"": [ { ""monsterId"": ""M1"", ""weight"": 4 }, { ""monsterId"": ""M2"", ""weight"": 2 }, { ""monsterId"": ""M3"", ""weight"": 2 } ] } ] }";

        internal const string Passives = @"{ ""schemaVersion"": 1, ""passives"": [
  { ""id"": ""P1"", ""displayName"": ""공격 속도"", ""iconKey"": ""icon.p1"", ""stat"": ""attackSpeed"",
    ""levels"": [ { ""bonus"": 0.10 }, { ""bonus"": 0.20 }, { ""bonus"": 0.30 }, { ""bonus"": 0.40 }, { ""bonus"": 0.50 } ] },
  { ""id"": ""P2"", ""displayName"": ""이동 속도"", ""iconKey"": ""icon.p2"", ""stat"": ""moveSpeed"",
    ""levels"": [ { ""bonus"": 0.10 }, { ""bonus"": 0.20 }, { ""bonus"": 0.30 }, { ""bonus"": 0.40 }, { ""bonus"": 0.50 } ] },
  { ""id"": ""P3"", ""displayName"": ""획득 범위"", ""iconKey"": ""icon.p3"", ""stat"": ""pickupRadius"",
    ""levels"": [ { ""bonus"": 0.10 }, { ""bonus"": 0.20 }, { ""bonus"": 0.30 }, { ""bonus"": 0.40 }, { ""bonus"": 0.50 } ] } ] }";

        internal const string LevelUpPool = @"{ ""schemaVersion"": 1, ""defaultWeight"": 10, ""healBaseWeight"": 30,
  ""rules"": [ { ""kind"": ""weaponNew"", ""targetId"": ""*"", ""weight"": 8 }, { ""kind"": ""weaponUpgrade"", ""targetId"": ""W1"", ""weight"": 12 } ] }";

        internal const string PlayerStats = @"{ ""schemaVersion"": 1, ""maxHealth"": 100, ""moveSpeed"": 5, ""radius"": 0.5, ""pickupRadius"": 2.0,
  ""invulnerabilitySeconds"": 0.5, ""startingWeaponId"": ""W1"",
  ""xpCurve"": { ""thresholds"": [5, 8, 12, 16, 20, 25, 30], ""growthAfterTable"": 5 },
  ""pickups"": { ""gemAssetKey"": ""shape.gem.blue"", ""gemRadius"": 0.2, ""gemXpValue"": 1, ""gemAttractSpeed"": 10,
               ""chestAssetKey"": ""shape.chest.yellow"", ""chestRadius"": 0.5 } }";

        internal static readonly DataFileKind[] AllKinds =
        {
            DataFileKind.Weapons, DataFileKind.Monsters, DataFileKind.Combinations, DataFileKind.SpawnTable,
            DataFileKind.Passives, DataFileKind.LevelUpPool, DataFileKind.PlayerStats,
        };

        internal static readonly DataFileKind[] RequiredKinds = { DataFileKind.Weapons, DataFileKind.Monsters, DataFileKind.SpawnTable, DataFileKind.PlayerStats };

        internal static readonly DataFileKind[] OptionalKinds = { DataFileKind.Combinations, DataFileKind.Passives, DataFileKind.LevelUpPool };

        /// Fresh copy of the seven valid texts.
        internal static Dictionary<DataFileKind, string> ValidTexts()
        {
            return new Dictionary<DataFileKind, string>
            {
                { DataFileKind.Weapons, Weapons },
                { DataFileKind.Monsters, Monsters },
                { DataFileKind.Combinations, Combinations },
                { DataFileKind.SpawnTable, SpawnTable },
                { DataFileKind.Passives, Passives },
                { DataFileKind.LevelUpPool, LevelUpPool },
                { DataFileKind.PlayerStats, PlayerStats },
            };
        }

        /// Valid texts with one exact, unique substring of one file replaced (throws when the substring is not found exactly once).
        internal static Dictionary<DataFileKind, string> With(DataFileKind kind, string oldText, string newText)
        {
            Dictionary<DataFileKind, string> texts = ValidTexts();
            texts[kind] = ReplaceOnce(texts[kind], oldText, newText);
            return texts;
        }

        /// Replaces a substring that must occur exactly once.
        internal static string ReplaceOnce(string text, string oldText, string newText)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 || text.IndexOf(oldText, first + 1, StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("Fixture substring must occur exactly once: " + oldText);
            }

            return text.Substring(0, first) + newText + text.Substring(first + oldText.Length);
        }

        /// Valid texts with one file replaced by raw text (null = missing).
        internal static Dictionary<DataFileKind, string> WithText(DataFileKind kind, string text)
        {
            Dictionary<DataFileKind, string> texts = ValidTexts();
            texts[kind] = text;
            return texts;
        }

        /// Loads in-memory texts; Blocking issues log errors, so failing-message checks are turned off for the current test first.
        internal static (GameData data, DataValidationReport report) Load(Dictionary<DataFileKind, string> texts)
        {
            IgnoreErrorLogs();
            return GameDataLoader.LoadFromTexts(texts);
        }

        /// Data errors are logged with Debug.LogError by design; tests that provoke them must not fail on the log line itself.
        internal static void IgnoreErrorLogs()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        internal static List<ValidationIssue> Find(DataValidationReport report, IssueSeverity severity, DataFileKind kind)
        {
            string fileName = GameDataLoader.FileNameOf(kind);
            return report.Issues.Where(i => i.Severity == severity && i.FileName == fileName).ToList();
        }

        internal static bool Has(DataValidationReport report, IssueSeverity severity, DataFileKind kind, string definitionId, string fieldPath)
        {
            string fileName = GameDataLoader.FileNameOf(kind);
            return report.Issues.Any(i => i.Severity == severity && i.FileName == fileName && i.DefinitionId == definitionId && i.FieldPath == fieldPath);
        }

        internal static string Dump(DataValidationReport report)
        {
            return string.Join("\n", report.Issues.Select(i => i.Severity + " " + i));
        }
    }
}
