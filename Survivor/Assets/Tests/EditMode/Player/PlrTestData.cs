using System.Collections.Generic;
using System.IO;
using System.Text;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // Real definitions from StreamingAssets/Data (definition constructors are internal to Data, so tests go through the loader).
    internal static class PlrTestData
    {
        internal static Dictionary<DataFileKind, string> RealTexts()
        {
            var texts = new Dictionary<DataFileKind, string>();
            string folder = Path.Combine(Application.dataPath, "StreamingAssets", GameDataLoader.DataFolder);
            var encoding = new UTF8Encoding(false);
            foreach (DataFileKind kind in System.Enum.GetValues(typeof(DataFileKind)))
            {
                texts[kind] = File.ReadAllText(Path.Combine(folder, GameDataLoader.FileNameOf(kind)), encoding);
            }

            return texts;
        }

        internal static GameData Real()
        {
            return Load(RealTexts());
        }

        // Real files with some texts replaced (null removes the file).
        internal static GameData With(DataFileKind kind, string text, DataFileKind? kind2 = null, string text2 = null)
        {
            Dictionary<DataFileKind, string> texts = RealTexts();
            texts[kind] = text;
            if (kind2.HasValue)
            {
                texts[kind2.Value] = text2;
            }

            return Load(texts);
        }

        private static GameData Load(Dictionary<DataFileKind, string> texts)
        {
            (GameData data, DataValidationReport report) = GameDataLoader.LoadFromTexts(texts);
            NUnit.Framework.Assert.IsNotNull(data, "test data failed to load");
            return data;
        }

        // Real weapons plus test-only base weapons (exactly 5 identical levels, as the binder requires): Z1 (zone, placement player),
        // PX (projectile, pierce 1).
        internal const string WeaponsWithExtras = @"{ ""schemaVersion"": 1, ""weapons"": [
  { ""id"": ""W1"", ""displayName"": ""w1"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.white"",
    ""levels"": [
      { ""damage"": 10, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
      { ""damage"": 12, ""cooldownSeconds"": 0.9, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
      { ""damage"": 14, ""cooldownSeconds"": 0.8, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0 },
      { ""damage"": 16, ""cooldownSeconds"": 0.7, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 2, ""spreadDegrees"": 10 },
      { ""damage"": 20, ""cooldownSeconds"": 0.6, ""projectileSpeed"": 12, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 2, ""spreadDegrees"": 10 } ] },
  { ""id"": ""W2"", ""displayName"": ""w2"", ""behavior"": ""orbit"", ""assetKey"": ""shape.orb.cyan"",
    ""levels"": [
      { ""damage"": 8,  ""orbCount"": 2, ""orbRadius"": 0.4,  ""orbitRadius"": 2.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 10, ""orbCount"": 2, ""orbRadius"": 0.4,  ""orbitRadius"": 2.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 12, ""orbCount"": 3, ""orbRadius"": 0.45, ""orbitRadius"": 2.2, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.45 },
      { ""damage"": 14, ""orbCount"": 3, ""orbRadius"": 0.45, ""orbitRadius"": 2.4, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.45 },
      { ""damage"": 16, ""orbCount"": 3, ""orbRadius"": 0.5,  ""orbitRadius"": 2.5, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.4 } ] },
  { ""id"": ""W3"", ""displayName"": ""w3"", ""behavior"": ""zone"", ""assetKey"": ""shape.zone.green"",
    ""levels"": [
      { ""damage"": 6,  ""cooldownSeconds"": 3.0, ""zoneRadius"": 1.5,  ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 8,  ""cooldownSeconds"": 2.8, ""zoneRadius"": 1.5,  ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 10, ""cooldownSeconds"": 2.6, ""zoneRadius"": 1.75, ""durationSeconds"": 2.0, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 12, ""cooldownSeconds"": 2.4, ""zoneRadius"": 1.75, ""durationSeconds"": 2.5, ""hitIntervalSeconds"": 0.5 },
      { ""damage"": 15, ""cooldownSeconds"": 2.2, ""zoneRadius"": 2.0,  ""durationSeconds"": 2.5, ""hitIntervalSeconds"": 0.5 } ] },
  { ""id"": ""W4"", ""displayName"": ""w4"", ""behavior"": ""homing"", ""assetKey"": ""shape.projectile.pink"",
    ""levels"": [
      { ""damage"": 8,  ""cooldownSeconds"": 1.5, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
      { ""damage"": 10, ""cooldownSeconds"": 1.4, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
      { ""damage"": 12, ""cooldownSeconds"": 1.3, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0 },
      { ""damage"": 14, ""cooldownSeconds"": 1.2, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0, ""projectileCount"": 2, ""spreadDegrees"": 15 },
      { ""damage"": 17, ""cooldownSeconds"": 1.1, ""projectileSpeed"": 9, ""projectileRadius"": 0.2, ""lifetimeSeconds"": 3.0, ""projectileCount"": 2, ""spreadDegrees"": 15 } ] },
  { ""id"": ""E1"", ""displayName"": ""e1"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.gold"", ""evolvedOnly"": true,
    ""levels"": [ { ""damage"": 22, ""cooldownSeconds"": 0.2, ""projectileSpeed"": 14, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""projectileCount"": 3, ""spreadDegrees"": 12 } ] },
  { ""id"": ""E2"", ""displayName"": ""e2"", ""behavior"": ""orbit"", ""assetKey"": ""shape.orb.gold"", ""evolvedOnly"": true,
    ""levels"": [ { ""damage"": 20, ""orbCount"": 4, ""orbRadius"": 0.6, ""orbitRadius"": 3.0, ""angularSpeedDegPerSec"": 180, ""hitIntervalSeconds"": 0.4 } ] },
  { ""id"": ""Z1"", ""displayName"": ""z1"", ""behavior"": ""zone"", ""assetKey"": ""shape.zone.green"",
    ""levels"": [ { ""damage"": 5, ""cooldownSeconds"": 1.0, ""zoneRadius"": 1.0, ""durationSeconds"": 0.5, ""hitIntervalSeconds"": 0.5, ""placement"": ""player"" }, { ""damage"": 5, ""cooldownSeconds"": 1.0, ""zoneRadius"": 1.0, ""durationSeconds"": 0.5, ""hitIntervalSeconds"": 0.5, ""placement"": ""player"" }, { ""damage"": 5, ""cooldownSeconds"": 1.0, ""zoneRadius"": 1.0, ""durationSeconds"": 0.5, ""hitIntervalSeconds"": 0.5, ""placement"": ""player"" }, { ""damage"": 5, ""cooldownSeconds"": 1.0, ""zoneRadius"": 1.0, ""durationSeconds"": 0.5, ""hitIntervalSeconds"": 0.5, ""placement"": ""player"" }, { ""damage"": 5, ""cooldownSeconds"": 1.0, ""zoneRadius"": 1.0, ""durationSeconds"": 0.5, ""hitIntervalSeconds"": 0.5, ""placement"": ""player"" } ] },
  { ""id"": ""PX"", ""displayName"": ""px"", ""behavior"": ""projectile"", ""assetKey"": ""shape.projectile.white"",
    ""levels"": [ { ""damage"": 7, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 6, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""pierceCount"": 1 }, { ""damage"": 7, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 6, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""pierceCount"": 1 }, { ""damage"": 7, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 6, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""pierceCount"": 1 }, { ""damage"": 7, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 6, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""pierceCount"": 1 }, { ""damage"": 7, ""cooldownSeconds"": 1.0, ""projectileSpeed"": 6, ""projectileRadius"": 0.25, ""lifetimeSeconds"": 2.0, ""pierceCount"": 1 } ] }
] }";

        // Player stats with invulnerabilitySeconds 0 (every step may damage).
        internal const string PlayerStatsNoInvulnerability = @"{ ""schemaVersion"": 1, ""maxHealth"": 100, ""moveSpeed"": 5, ""radius"": 0.5, ""pickupRadius"": 2.0,
  ""invulnerabilitySeconds"": 0, ""startingWeaponId"": ""W1"",
  ""xpCurve"": { ""thresholds"": [5, 8, 12, 16, 20, 25, 30], ""growthAfterTable"": 5 },
  ""pickups"": { ""gemAssetKey"": ""shape.gem.blue"", ""gemRadius"": 0.2, ""gemXpValue"": 1, ""gemAttractSpeed"": 10,
                 ""chestAssetKey"": ""shape.chest.yellow"", ""chestRadius"": 0.5 } }";
    }
}
