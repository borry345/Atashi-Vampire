using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Loads and validates the seven data files. Pipeline order is fixed (5.11.0): read -> JsonTreeReader -> binders
    /// (PlayerStats, Weapons, Passives, Monsters, SpawnTable, Combinations, LevelUpPool) -> references -> playability -> tunneling.
    public static class GameDataLoader
    {
        /// Sub-folder of StreamingAssets that holds the files.
        public const string DataFolder = "Data";

        private static readonly DataFileKind[] AllKinds =
        {
            DataFileKind.Weapons, DataFileKind.Monsters, DataFileKind.Combinations, DataFileKind.SpawnTable,
            DataFileKind.Passives, DataFileKind.LevelUpPool, DataFileKind.PlayerStats,
        };

        /// Loads and validates all 7 files from rootDirectory/Data (rootDirectory = Application.streamingAssetsPath); never throws for data errors.
        public static (GameData data, DataValidationReport report) LoadAll(string rootDirectory)
        {
            if (rootDirectory == null)
            {
                throw new ArgumentNullException(nameof(rootDirectory));
            }

            var texts = new Dictionary<DataFileKind, string>();
            var readErrors = new Dictionary<DataFileKind, string>();
            var encoding = new UTF8Encoding(false, false);
            for (int i = 0; i < AllKinds.Length; i++)
            {
                DataFileKind kind = AllKinds[i];
                string path = Path.Combine(rootDirectory, DataFolder, FileNameOf(kind));
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    texts[kind] = File.ReadAllText(path, encoding);
                }
                catch (IOException e)
                {
                    readErrors[kind] = e.Message;
                }
                catch (UnauthorizedAccessException e)
                {
                    readErrors[kind] = e.Message;
                }
            }

            return Run(texts, readErrors);
        }

        /// Same pipeline over in-memory texts (EditMode tests); a missing key or null text means the file is missing.
        public static (GameData data, DataValidationReport report) LoadFromTexts(IReadOnlyDictionary<DataFileKind, string> texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            var copy = new Dictionary<DataFileKind, string>();
            foreach (KeyValuePair<DataFileKind, string> pair in texts)
            {
                if (pair.Value != null)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return Run(copy, new Dictionary<DataFileKind, string>());
        }

        /// File name under StreamingAssets/Data for a kind (5.13 table).
        public static string FileNameOf(DataFileKind kind)
        {
            switch (kind)
            {
                case DataFileKind.Weapons: return "weapons.json";
                case DataFileKind.Monsters: return "monsters.json";
                case DataFileKind.Combinations: return "combinations.json";
                case DataFileKind.SpawnTable: return "spawnTable.json";
                case DataFileKind.Passives: return "passives.json";
                case DataFileKind.LevelUpPool: return "levelUpPool.json";
                case DataFileKind.PlayerStats: return "playerStats.json";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown DataFileKind.");
            }
        }

        private static (GameData, DataValidationReport) Run(Dictionary<DataFileKind, string> texts, Dictionary<DataFileKind, string> readErrors)
        {
            var report = new DataValidationReport();

            // 1. Read: report decoding problems, keep going.
            var roots = new Dictionary<DataFileKind, JObject>();
            int filesParsed = 0;
            for (int i = 0; i < AllKinds.Length; i++)
            {
                DataFileKind kind = AllKinds[i];
                string error;
                if (readErrors.TryGetValue(kind, out error))
                {
                    JsonTreeReader.AddFileFailure(kind, report, "파일을 읽을 수 없습니다(" + error + ").");
                    continue;
                }

                string text;
                texts.TryGetValue(kind, out text);
                if (text != null)
                {
                    CheckEncoding(kind, text, report);
                }

                // 2. Parse to a tree (file-level failure policy).
                JObject root = JsonTreeReader.Read(kind, text, report);
                if (root != null)
                {
                    roots[kind] = root;
                    filesParsed++;
                }
            }

            // 3. Binders in fixed order (referenced files first).
            PlayerStatsDefinition playerStats = Bind(roots, DataFileKind.PlayerStats, report, PlayerStatsBinder.Bind);
            List<WeaponDefinition> weapons = Bind(roots, DataFileKind.Weapons, report, WeaponsBinder.Bind) ?? new List<WeaponDefinition>();
            List<PassiveDefinition> passives = Bind(roots, DataFileKind.Passives, report, PassivesBinder.Bind) ?? new List<PassiveDefinition>();
            List<MonsterDefinition> monsters = Bind(roots, DataFileKind.Monsters, report, MonstersBinder.Bind) ?? new List<MonsterDefinition>();
            SpawnTableDefinition spawnTable = Bind(roots, DataFileKind.SpawnTable, report, SpawnTableBinder.Bind);
            List<CombinationRule> combinations = Bind(roots, DataFileKind.Combinations, report, CombinationsBinder.Bind) ?? new List<CombinationRule>();
            LevelUpPoolDefinition levelUpPool = Bind(roots, DataFileKind.LevelUpPool, report, LevelUpPoolBinder.Bind);

            // 4. Cross-file references.
            playerStats = CheckStartingWeapon(playerStats, weapons, report);
            monsters = CheckBossSummons(monsters, report);
            spawnTable = CheckSpawnTable(spawnTable, monsters, report);
            combinations = CheckCombinations(combinations, weapons, passives, report);
            levelUpPool = CheckLevelUpRules(levelUpPool, weapons, passives, report);

            var data = new GameData(weapons, monsters, combinations, spawnTable, passives, levelUpPool, playerStats);

            // 5. Playability (Blocking issues), 6. tunneling invariant (warnings only).
            PlayabilityChecker.Check(data, report);
            TunnelingInvariantValidator.Validate(data, report);

            string summary = "파일 " + filesParsed.ToString(CultureInfo.InvariantCulture) + "/" + AllKinds.Length.ToString(CultureInfo.InvariantCulture)
                + ", 무기 " + Count(data.Weapons.Count) + ", 몬스터 " + Count(data.Monsters.Count) + ", 조합 " + Count(data.Combinations.Count)
                + ", 스폰 구간 " + Count(data.SpawnTable != null ? data.SpawnTable.Segments.Count : 0) + ", 패시브 " + Count(data.Passives.Count)
                + ", 레벨업 규칙 " + Count(data.LevelUpPool.Rules.Count) + (data.LevelUpPool.IsFallback ? "(균등 폴백)" : string.Empty)
                + ", 경고 " + Count(report.WarningCount) + ", 차단 " + Count(report.BlockingCount);
            if (report.IsPlayable)
            {
                DataLog.Info("데이터 로드 완료: " + summary);
            }
            else
            {
                DataLog.Warning("데이터 로드 결과 플레이 불가: " + summary);
            }

            return (data, report);
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static T Bind<T>(Dictionary<DataFileKind, JObject> roots, DataFileKind kind, DataValidationReport report, Func<JObject, DataValidationReport, T> binder)
            where T : class
        {
            JObject root;
            return roots.TryGetValue(kind, out root) ? binder(root, report) : null;
        }

        private static void CheckEncoding(DataFileKind kind, string text, DataValidationReport report)
        {
            int index = text.IndexOf('�');
            if (index < 0)
            {
                return;
            }

            int line = 1;
            int column = 1;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            report.Add(new ValidationIssue(IssueSeverity.Warning, FileNameOf(kind), null, null,
                "인코딩 오류 가능(UTF-8이 아닌 파일일 수 있습니다). 메모장 등에서 UTF-8로 다시 저장하세요.", line, column));
        }

        private static PlayerStatsDefinition CheckStartingWeapon(PlayerStatsDefinition stats, List<WeaponDefinition> weapons, DataValidationReport report)
        {
            if (stats == null)
            {
                return null;
            }

            WeaponDefinition weapon = weapons.Find(w => w.Id == stats.StartingWeaponId);
            string problem = weapon == null
                ? "무기 파일에 없는(또는 오류로 건너뛴) 무기 ID입니다"
                : weapon.EvolvedOnly ? "진화 전용 무기는 시작 무기로 쓸 수 없습니다" : null;
            if (problem == null)
            {
                return stats;
            }

            report.Add(new ValidationIssue(IssueSeverity.Blocking, FileNameOf(DataFileKind.PlayerStats), null, "startingWeaponId",
                problem + "(현재 값: \"" + stats.StartingWeaponId + "\"). 플레이어 기본 스탯 오류라 게임을 시작할 수 없습니다. weapons.json의 기본 무기 ID로 바꾸세요."));
            return null;
        }

        private static List<MonsterDefinition> CheckBossSummons(List<MonsterDefinition> monsters, DataValidationReport report)
        {
            var result = new List<MonsterDefinition>(monsters.Count);
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterDefinition monster = monsters[i];
                bool valid = true;
                for (int p = 0; p < monster.BossPatterns.Count; p++)
                {
                    BossPatternDefinition pattern = monster.BossPatterns[p];
                    if (pattern.Kind != BossPatternKind.Summon)
                    {
                        continue;
                    }

                    MonsterDefinition target = monsters.Find(m => m.Id == pattern.MonsterId);
                    string problem = target == null
                        ? "몬스터 파일에 없는(또는 오류로 건너뛴) 몬스터 ID입니다"
                        : target.Behavior == MonsterBehavior.Boss ? "보스는 소환 대상이 될 수 없습니다" : null;
                    if (problem != null)
                    {
                        valid = false;
                        report.Add(new ValidationIssue(IssueSeverity.Warning, FileNameOf(DataFileKind.Monsters), monster.Id,
                            "bossPatterns[" + p.ToString(CultureInfo.InvariantCulture) + "].monsterId",
                            "참조 오류: " + problem + "(현재 값: \"" + pattern.MonsterId + "\"). 이 정의를 건너뜁니다."));
                    }
                }

                if (valid)
                {
                    result.Add(monster);
                }
            }

            return result;
        }

        private static SpawnTableDefinition CheckSpawnTable(SpawnTableDefinition table, List<MonsterDefinition> monsters, DataValidationReport report)
        {
            if (table == null)
            {
                return null;
            }

            string fileName = FileNameOf(DataFileKind.SpawnTable);
            var segments = new List<SpawnSegmentDefinition>();
            for (int s = 0; s < table.Segments.Count; s++)
            {
                SpawnSegmentDefinition segment = table.Segments[s];
                var entries = new List<SpawnEntryDefinition>();
                for (int e = 0; e < segment.Entries.Count; e++)
                {
                    SpawnEntryDefinition entry = segment.Entries[e];
                    MonsterDefinition monster = monsters.Find(m => m.Id == entry.MonsterId);
                    string problem = monster == null
                        ? "몬스터 파일에 없는(또는 오류로 건너뛴) 몬스터 ID입니다"
                        : monster.Behavior == MonsterBehavior.Boss ? "boss 행동 몬스터는 일반 스폰 항목에 넣을 수 없습니다(bossMonsterId로 지정)" : null;
                    if (problem != null)
                    {
                        report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null,
                            "segments[" + s.ToString(CultureInfo.InvariantCulture) + "].entries[" + e.ToString(CultureInfo.InvariantCulture) + "].monsterId",
                            "참조 오류: " + problem + "(현재 값: \"" + entry.MonsterId + "\"). 이 항목을 건너뜁니다."));
                        continue;
                    }

                    entries.Add(entry);
                }

                if (entries.Count == 0)
                {
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, "segments[" + s.ToString(CultureInfo.InvariantCulture) + "]",
                        "유효한 스폰 항목이 없어 이 구간을 건너뜁니다(이 시간대에는 몬스터가 나오지 않습니다)."));
                    continue;
                }

                segments.Add(new SpawnSegmentDefinition
                {
                    StartSeconds = segment.StartSeconds,
                    StartStep = segment.StartStep,
                    EndSeconds = segment.EndSeconds,
                    EndStep = segment.EndStep,
                    SpawnsPerSecond = segment.SpawnsPerSecond,
                    Entries = entries,
                });
            }

            string bossId = table.BossMonsterId;
            if (bossId != null)
            {
                MonsterDefinition boss = monsters.Find(m => m.Id == bossId);
                string problem = boss == null
                    ? "몬스터 파일에 없는(또는 오류로 건너뛴) 몬스터 ID입니다"
                    : boss.Behavior != MonsterBehavior.Boss ? "boss 행동 몬스터가 아닙니다" : null;
                if (problem != null)
                {
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, "bossMonsterId",
                        "참조 오류: " + problem + "(현재 값: \"" + bossId + "\"). 보스가 등장하지 않아 10분에 클리어할 수 없습니다."));
                    bossId = null;
                }
            }

            return new SpawnTableDefinition { DespawnDistance = table.DespawnDistance, BossMonsterId = bossId, Segments = segments };
        }

        private static List<CombinationRule> CheckCombinations(List<CombinationRule> rules, List<WeaponDefinition> weapons, List<PassiveDefinition> passives, DataValidationReport report)
        {
            string fileName = FileNameOf(DataFileKind.Combinations);
            var result = new List<CombinationRule>();
            var usedWeapons = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rules.Count; i++)
            {
                CombinationRule rule = rules[i];
                bool valid = true;
                WeaponDefinition baseWeapon = weapons.Find(w => w.Id == rule.WeaponId);
                if (baseWeapon == null || baseWeapon.EvolvedOnly)
                {
                    valid = false;
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, rule.Id, "weaponId",
                        "참조 오류: " + (baseWeapon == null ? "무기 파일에 없는(또는 오류로 건너뛴) 무기 ID입니다" : "진화 전용 무기는 재료가 될 수 없습니다")
                        + "(현재 값: \"" + rule.WeaponId + "\"). 이 규칙을 건너뜁니다."));
                }

                if (!passives.Exists(p => p.Id == rule.PassiveId))
                {
                    valid = false;
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, rule.Id, "passiveId",
                        "참조 오류: 패시브 파일에 없는(또는 오류로 건너뛴) 패시브 ID입니다(현재 값: \"" + rule.PassiveId + "\"). 이 규칙을 건너뜁니다."));
                }

                WeaponDefinition resultWeapon = weapons.Find(w => w.Id == rule.ResultWeaponId);
                if (resultWeapon == null || !resultWeapon.EvolvedOnly)
                {
                    valid = false;
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, rule.Id, "resultWeaponId",
                        "참조 오류: " + (resultWeapon == null ? "무기 파일에 없는(또는 오류로 건너뛴) 무기 ID입니다" : "결과 무기는 evolvedOnly가 true여야 합니다")
                        + "(현재 값: \"" + rule.ResultWeaponId + "\"). 이 규칙을 건너뜁니다."));
                }

                if (valid && !usedWeapons.Add(rule.WeaponId))
                {
                    valid = false;
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, rule.Id, "weaponId",
                        "같은 무기(\"" + rule.WeaponId + "\")를 쓰는 규칙이 이미 있어 이 규칙을 건너뜁니다."));
                }

                if (valid)
                {
                    result.Add(rule);
                }
            }

            return result;
        }

        private static LevelUpPoolDefinition CheckLevelUpRules(LevelUpPoolDefinition pool, List<WeaponDefinition> weapons, List<PassiveDefinition> passives, DataValidationReport report)
        {
            if (pool == null)
            {
                return LevelUpPoolDefinition.UniformFallback;
            }

            var rules = new List<LevelUpRuleDefinition>();
            for (int i = 0; i < pool.Rules.Count; i++)
            {
                LevelUpRuleDefinition rule = pool.Rules[i];
                bool isWeapon = rule.Kind == UpgradeKind.WeaponNew || rule.Kind == UpgradeKind.WeaponUpgrade;
                bool exists = rule.TargetId == LevelUpRuleDefinition.AnyTarget
                    || (isWeapon ? weapons.Exists(w => w.Id == rule.TargetId) : passives.Exists(p => p.Id == rule.TargetId));
                if (!exists)
                {
                    report.Add(new ValidationIssue(IssueSeverity.Warning, FileNameOf(DataFileKind.LevelUpPool), null,
                        "rules[" + i.ToString(CultureInfo.InvariantCulture) + "].targetId",
                        "참조 오류: " + (isWeapon ? "무기" : "패시브") + " 파일에 없는 ID입니다(현재 값: \"" + rule.TargetId + "\"). 이 규칙을 건너뜁니다."));
                    continue;
                }

                rules.Add(rule);
            }

            return new LevelUpPoolDefinition
            {
                DefaultWeight = pool.DefaultWeight,
                HealBaseWeight = pool.HealBaseWeight,
                Rules = rules,
                IsFallback = false,
            };
        }
    }
}
