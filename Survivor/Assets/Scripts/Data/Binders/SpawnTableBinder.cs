using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Binds spawnTable.json (5.13.4). Table-level field errors are Blocking (no table); a broken segment or entry is skipped with a warning.
    /// Monster references (existence, non-boss entries, boss id) are checked later by the loader's reference step.
    internal static class SpawnTableBinder
    {
        /// Spawn segments end at the boss arrival (600 s).
        internal const int MaxEndStep = (int)GameRules.BossSpawnStep;

        /// Returns the table with unchecked references, or null after recording Blocking issues.
        internal static SpawnTableDefinition Bind(JObject root, DataValidationReport report)
        {
            string fileName = GameDataLoader.FileNameOf(DataFileKind.SpawnTable);
            var r = new FieldReader(root, fileName, null, null, report, IssueSeverity.Blocking);
            r.Known(JsonTreeReader.SchemaVersionKey);

            double despawnDistance = r.Number("despawnDistance", NumberRange.Positive, true);
            if (!r.HasErrors && !(despawnDistance > GameRules.SpawnRadius))
            {
                r.Error("despawnDistance", "스폰 반경(" + FieldReader.Num(GameRules.SpawnRadius) + ")보다 커야 합니다(현재 값: " + FieldReader.Num(despawnDistance) + ").", root["despawnDistance"]);
            }

            string bossMonsterId = ReadBossId(r, root, report, fileName);
            JArray segmentsArray = r.ArrayField("segments", true);
            if (segmentsArray != null && segmentsArray.Count == 0)
            {
                r.Error("segments", "구간이 1개 이상 있어야 합니다.", segmentsArray);
            }

            r.ReportUnknownKeys();
            if (r.HasErrors)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, fileName, null, null,
                    "스폰 테이블에 오류가 있어 스폰 테이블을 쓸 수 없습니다. 위 항목을 고친 뒤 다시 실행하세요.",
                    JsonTreeReader.LineOf(root), JsonTreeReader.ColumnOf(root)));
                return null;
            }

            var segments = new List<SpawnSegmentDefinition>();
            int previousEndStep = 0;
            for (int i = 0; i < segmentsArray.Count; i++)
            {
                SpawnSegmentDefinition segment = BindSegment(segmentsArray[i], i, fileName, report);
                if (segment == null)
                {
                    continue;
                }

                if (segments.Count > 0 && segment.StartStep < previousEndStep)
                {
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, SegmentPath(i),
                        "구간은 시간 오름차순이고 겹치지 않아야 합니다(이 구간의 시작이 앞 구간의 끝보다 이릅니다). 이 구간을 건너뜁니다.",
                        JsonTreeReader.LineOf(segmentsArray[i]), JsonTreeReader.ColumnOf(segmentsArray[i])));
                    continue;
                }

                segments.Add(segment);
                previousEndStep = segment.EndStep;
            }

            return new SpawnTableDefinition
            {
                DespawnDistance = (float)despawnDistance,
                BossMonsterId = bossMonsterId,
                Segments = segments,
            };
        }

        internal static string SegmentPath(int index)
        {
            return "segments[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }

        private static string ReadBossId(FieldReader r, JObject root, DataValidationReport report, string fileName)
        {
            // bossMonsterId problems are warnings only (D6 default): the run starts and the boss never appears.
            r.Known("bossMonsterId");
            JToken token;
            if (!root.TryGetValue("bossMonsterId", out token))
            {
                report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, "bossMonsterId",
                    "필수 필드가 없어 보스가 등장하지 않습니다(10분에 클리어할 수 없음). boss 행동 몬스터의 ID를 넣으세요.",
                    JsonTreeReader.LineOf(root), JsonTreeReader.ColumnOf(root)));
                return null;
            }

            if (token.Type != JTokenType.String || !FieldReader.IsValidId((string)token))
            {
                report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, "bossMonsterId",
                    "올바른 몬스터 ID 문자열이 아니라 보스가 등장하지 않습니다(10분에 클리어할 수 없음).",
                    JsonTreeReader.LineOf(token), JsonTreeReader.ColumnOf(token)));
                return null;
            }

            return (string)token;
        }

        private static SpawnSegmentDefinition BindSegment(JToken token, int index, string fileName, DataValidationReport report)
        {
            string path = SegmentPath(index);
            var obj = token as JObject;
            if (obj == null)
            {
                report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, path,
                    "구간은 객체여야 합니다(현재: " + FieldReader.TypeName(token) + "). 이 구간을 건너뜁니다.",
                    JsonTreeReader.LineOf(token), JsonTreeReader.ColumnOf(token)));
                return null;
            }

            var r = new FieldReader(obj, fileName, null, path, report, IssueSeverity.Warning);
            double start = r.Number("startSeconds", NumberRange.NonNegative, true);
            double end = r.Number("endSeconds", NumberRange.Any, true);
            double rate = r.Number("spawnsPerSecond", NumberRange.Positive, true);
            JArray entriesArray = r.ArrayField("entries", true);
            int startStep = r.Steps("startSeconds", start);
            int endStep = 0;
            bool endValid = !r.HasErrors;
            if (endValid && end > start)
            {
                double clampedEnd = end;
                if (end > GameRules.BossSpawnStep / (double)StepMath.StepsPerSecond)
                {
                    clampedEnd = GameRules.BossSpawnStep / (double)StepMath.StepsPerSecond;
                    r.Warn("endSeconds", "600초를 넘어 600초로 잘라냈습니다(현재 값: " + FieldReader.Num(end) + ").", obj["endSeconds"]);
                }

                end = clampedEnd;
                endStep = r.Steps("endSeconds", end);
            }

            if (endValid && endStep <= startStep)
            {
                r.Error("endSeconds", "끝 시각은 시작 시각보다 커야 하고 시작은 600초보다 작아야 합니다(시작 " + FieldReader.Num(start) + "초).", obj["endSeconds"]);
            }

            var entries = new List<SpawnEntryDefinition>();
            if (entriesArray != null)
            {
                for (int j = 0; j < entriesArray.Count; j++)
                {
                    SpawnEntryDefinition entry = BindEntry(entriesArray[j], path + ".entries[" + j.ToString(CultureInfo.InvariantCulture) + "]", fileName, report);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }

            // hpMultiplier is read by its own reader below so that a bad value never skips the segment.
            r.Known("hpMultiplier");
            r.ReportUnknownKeys();
            if (r.HasErrors)
            {
                BinderSupport.AddSkipped(report, fileName, null, path, r.ErrorCount, obj);
                return null;
            }

            float hpMultiplier = ReadHpMultiplier(obj, path, fileName, report);
            return new SpawnSegmentDefinition
            {
                StartSeconds = (float)start,
                StartStep = startStep,
                EndSeconds = (float)end,
                EndStep = endStep,
                SpawnsPerSecond = (float)rate,
                HpMultiplier = hpMultiplier,
                Entries = entries,
            };
        }

        /// Optional segments[s].hpMultiplier (5.13.4): finite and > 0, default 1.0. An invalid value is a warning and
        /// only this segment falls back to 1.0; the segment and the table stay valid.
        private static float ReadHpMultiplier(JObject obj, string path, string fileName, DataValidationReport report)
        {
            var hp = new FieldReader(obj, fileName, null, path, report, IssueSeverity.Warning);
            double value = hp.Number("hpMultiplier", NumberRange.Positive, false, 1d);
            if (hp.HasErrors)
            {
                hp.Warn("hpMultiplier", "이 구간의 체력 배율을 1.0으로 대체합니다.", obj["hpMultiplier"]);
                return 1f;
            }

            return (float)value;
        }

        private static SpawnEntryDefinition BindEntry(JToken token, string path, string fileName, DataValidationReport report)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, path,
                    "항목은 객체여야 합니다(현재: " + FieldReader.TypeName(token) + "). 이 항목을 건너뜁니다.",
                    JsonTreeReader.LineOf(token), JsonTreeReader.ColumnOf(token)));
                return null;
            }

            var r = new FieldReader(obj, fileName, null, path, report, IssueSeverity.Warning);
            string monsterId = r.String("monsterId", true, true);
            double weight = r.Number("weight", NumberRange.Positive, true);
            r.ReportUnknownKeys();
            if (r.HasErrors)
            {
                BinderSupport.AddSkipped(report, fileName, null, path, r.ErrorCount, obj);
                return null;
            }

            return new SpawnEntryDefinition { MonsterId = monsterId, Weight = (float)weight };
        }
    }
}
