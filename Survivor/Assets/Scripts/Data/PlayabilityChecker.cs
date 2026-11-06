using System;
using System.Collections.Generic;

namespace Survivor.Data
{
    /// The four "not playable" criteria of F11; each failed criterion becomes one Blocking issue.
    public static class PlayabilityChecker
    {
        /// Adds a Blocking issue for each failed criterion: no usable weapon, no spawnable monster, no player stats, no spawn table.
        public static void Check(GameData data, DataValidationReport report)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (CountUsableWeapons(data) == 0)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, GameDataLoader.FileNameOf(DataFileKind.Weapons), null, null,
                    "플레이 불가: 사용 가능한 무기가 0개입니다(진화 전용이 아닌 유효한 무기가 필요합니다). weapons.json의 오류를 고치세요."));
            }

            if (CountSpawnableMonsters(data) == 0)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, GameDataLoader.FileNameOf(DataFileKind.SpawnTable), null, null,
                    "플레이 불가: 스폰 가능한 몬스터가 0종입니다(스폰 테이블이 참조하는 유효한 몬스터가 없습니다). spawnTable.json과 monsters.json을 확인하세요."));
            }

            if (data.PlayerStats == null)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, GameDataLoader.FileNameOf(DataFileKind.PlayerStats), null, null,
                    "플레이 불가: 플레이어 기본 스탯이 없습니다. playerStats.json의 오류를 고치세요."));
            }

            if (data.SpawnTable == null)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, GameDataLoader.FileNameOf(DataFileKind.SpawnTable), null, null,
                    "플레이 불가: 스폰 테이블이 없습니다. spawnTable.json의 오류를 고치세요."));
            }
        }

        /// Number of valid weapons that are not evolvedOnly.
        public static int CountUsableWeapons(GameData data)
        {
            int count = 0;
            for (int i = 0; i < data.Weapons.Count; i++)
            {
                if (!data.Weapons[i].EvolvedOnly)
                {
                    count++;
                }
            }

            return count;
        }

        /// Number of distinct valid monsters referenced by the spawn table entries (0 without a table).
        public static int CountSpawnableMonsters(GameData data)
        {
            if (data.SpawnTable == null)
            {
                return 0;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int s = 0; s < data.SpawnTable.Segments.Count; s++)
            {
                IReadOnlyList<SpawnEntryDefinition> entries = data.SpawnTable.Segments[s].Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    MonsterDefinition monster;
                    if (data.TryGetMonster(entries[e].MonsterId, out monster) && monster.Behavior != MonsterBehavior.Boss)
                    {
                        ids.Add(monster.Id);
                    }
                }
            }

            return ids.Count;
        }
    }
}
