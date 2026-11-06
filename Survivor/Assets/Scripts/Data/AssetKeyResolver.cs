using System;
using System.Collections.Generic;

namespace Survivor.Data
{
    /// Resolves string asset keys through the registry; unknown keys fall back to the default circle and warn once per key.
    public sealed class AssetKeyResolver
    {
        private readonly Dictionary<string, AssetKeyRegistry.Entry> _entries = new Dictionary<string, AssetKeyRegistry.Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly DataValidationReport _report;

        /// Builds the lookup; a null registry makes every key fall back (one warning). Issues go to report, or straight to the log when report is null.
        public AssetKeyResolver(AssetKeyRegistry registryOrNull, DataValidationReport report)
        {
            _report = report;
            if (registryOrNull == null)
            {
                AddWarning(new ValidationIssue(IssueSeverity.Warning, null, null, null,
                    "에셋 키 레지스트리(Resources/AssetKeyRegistry.asset)가 없어 모든 키를 기본 도형(마젠타 원)으로 대체합니다. 에디터에서 Survivor/Asset Key Registry 에셋을 만드세요."));
                return;
            }

            if (registryOrNull.Entries == null)
            {
                return;
            }

            for (int i = 0; i < registryOrNull.Entries.Count; i++)
            {
                AssetKeyRegistry.Entry entry = registryOrNull.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                if (_entries.ContainsKey(entry.Key))
                {
                    AddWarning(new ValidationIssue(IssueSeverity.Warning, null, null, null,
                        "에셋 키 레지스트리에 같은 키 '" + entry.Key + "'가 여러 번 있습니다. 첫 항목만 씁니다."));
                    continue;
                }

                _entries.Add(entry.Key, entry);
            }
        }

        /// Returns the registered visual, or the magenta default circle (warning once per key). Never returns a null sprite.
        public VisualAsset Resolve(string key)
        {
            return Resolve(key, null, null, null);
        }

        /// True when the key is registered.
        public bool Contains(string key)
        {
            return key != null && _entries.ContainsKey(key);
        }

        /// Load-time check: resolves every asset/icon key referenced by the data so missing keys are warned with their file and definition.
        public void ValidateKeys(GameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string weaponFile = GameDataLoader.FileNameOf(DataFileKind.Weapons);
            for (int i = 0; i < data.Weapons.Count; i++)
            {
                WeaponDefinition weapon = data.Weapons[i];
                Resolve(weapon.AssetKey, weaponFile, weapon.Id, "assetKey");
                Resolve(weapon.IconKey, weaponFile, weapon.Id, "iconKey");
            }

            string monsterFile = GameDataLoader.FileNameOf(DataFileKind.Monsters);
            for (int i = 0; i < data.Monsters.Count; i++)
            {
                MonsterDefinition monster = data.Monsters[i];
                Resolve(monster.AssetKey, monsterFile, monster.Id, "assetKey");
                if (monster.Ranged != null && monster.Ranged.Projectile != null)
                {
                    Resolve(monster.Ranged.Projectile.AssetKey, monsterFile, monster.Id, "ranged.projectile.assetKey");
                }

                for (int p = 0; p < monster.BossPatterns.Count; p++)
                {
                    if (monster.BossPatterns[p].Kind == BossPatternKind.HazardZone)
                    {
                        Resolve(monster.BossPatterns[p].AssetKey, monsterFile, monster.Id, "bossPatterns[" + p.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].assetKey");
                    }
                }
            }

            string passiveFile = GameDataLoader.FileNameOf(DataFileKind.Passives);
            for (int i = 0; i < data.Passives.Count; i++)
            {
                Resolve(data.Passives[i].IconKey, passiveFile, data.Passives[i].Id, "iconKey");
            }

            if (data.PlayerStats != null)
            {
                string playerFile = GameDataLoader.FileNameOf(DataFileKind.PlayerStats);
                Resolve(data.PlayerStats.Pickups.GemAssetKey, playerFile, null, "pickups.gemAssetKey");
                Resolve(data.PlayerStats.Pickups.ChestAssetKey, playerFile, null, "pickups.chestAssetKey");
            }
        }

        private VisualAsset Resolve(string key, string fileName, string definitionId, string fieldPath)
        {
            AssetKeyRegistry.Entry entry;
            if (key != null && _entries.TryGetValue(key, out entry))
            {
                return new VisualAsset(entry.Sprite != null ? entry.Sprite : DefaultShapeFactory.Circle, entry.Color, entry.PrefabOverride);
            }

            string warnKey = key ?? string.Empty;
            if (_warnedKeys.Add(warnKey))
            {
                AddWarning(new ValidationIssue(IssueSeverity.Warning, fileName, definitionId, fieldPath,
                    "에셋 키 '" + warnKey + "'가 레지스트리에 없어 기본 도형(마젠타 원)으로 대체합니다. AssetKeyRegistry에 이 키를 등록하세요."));
            }

            return new VisualAsset(DefaultShapeFactory.Circle, DefaultShapeFactory.FallbackColor, null);
        }

        private void AddWarning(ValidationIssue issue)
        {
            if (_report != null)
            {
                _report.Add(issue);
            }
            else
            {
                DataLog.Warning(issue.ToString());
            }
        }
    }
}
