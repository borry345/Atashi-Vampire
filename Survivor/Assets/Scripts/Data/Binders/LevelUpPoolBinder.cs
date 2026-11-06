using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Binds levelUpPool.json (5.13.6). Top-level errors fall back to UniformFallback; a broken rule is skipped.
    internal static class LevelUpPoolBinder
    {
        private static readonly string[] KindNames = { "weaponNew", "weaponUpgrade", "passiveNew", "passiveUpgrade" };
        private static readonly UpgradeKind[] KindValues = { UpgradeKind.WeaponNew, UpgradeKind.WeaponUpgrade, UpgradeKind.PassiveNew, UpgradeKind.PassiveUpgrade };

        /// Returns the pool (rule targets unchecked), or null when it must fall back.
        internal static LevelUpPoolDefinition Bind(JObject root, DataValidationReport report)
        {
            string fileName = GameDataLoader.FileNameOf(DataFileKind.LevelUpPool);
            var r = new FieldReader(root, fileName, null, null, report, IssueSeverity.Warning);
            r.Known(JsonTreeReader.SchemaVersionKey);
            double defaultWeight = r.Number("defaultWeight", NumberRange.Positive, true);
            double healBaseWeight = r.Number("healBaseWeight", NumberRange.NonNegative, true);
            JArray rulesArray = r.ArrayField("rules", false);
            r.ReportUnknownKeys();
            if (r.HasErrors)
            {
                report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, null,
                    "레벨업 선택지 풀에 오류가 있어 모든 후보를 같은 가중치로 대체합니다(회복은 체력 비율 가중 유지).",
                    JsonTreeReader.LineOf(root), JsonTreeReader.ColumnOf(root)));
                return null;
            }

            var rules = new List<LevelUpRuleDefinition>();
            if (rulesArray != null)
            {
                for (int i = 0; i < rulesArray.Count; i++)
                {
                    string path = "rules[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    var obj = rulesArray[i] as JObject;
                    if (obj == null)
                    {
                        report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, path,
                            "규칙은 객체여야 합니다(현재: " + FieldReader.TypeName(rulesArray[i]) + "). 이 규칙을 건너뜁니다.",
                            JsonTreeReader.LineOf(rulesArray[i]), JsonTreeReader.ColumnOf(rulesArray[i])));
                        continue;
                    }

                    var c = new FieldReader(obj, fileName, null, path, report, IssueSeverity.Warning);
                    UpgradeKind kind;
                    c.TryEnum("kind", KindNames, KindValues, true, UpgradeKind.WeaponNew, out kind);
                    string targetId = c.String("targetId", true);
                    if (targetId != null && targetId != LevelUpRuleDefinition.AnyTarget && !FieldReader.IsValidId(targetId))
                    {
                        c.Error("targetId", "무기·패시브 ID 또는 \"*\"여야 합니다(현재 값: \"" + targetId + "\").", obj["targetId"]);
                    }

                    double weight = c.Number("weight", NumberRange.NonNegative, true);
                    c.ReportUnknownKeys();
                    if (c.HasErrors)
                    {
                        BinderSupport.AddSkipped(report, fileName, null, path, c.ErrorCount, obj);
                        continue;
                    }

                    rules.Add(new LevelUpRuleDefinition { Kind = kind, TargetId = targetId, Weight = (float)weight });
                }
            }

            return new LevelUpPoolDefinition
            {
                DefaultWeight = (float)defaultWeight,
                HealBaseWeight = (float)healBaseWeight,
                Rules = rules,
                IsFallback = false,
            };
        }
    }
}
