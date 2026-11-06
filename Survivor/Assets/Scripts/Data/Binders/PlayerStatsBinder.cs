using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Binds playerStats.json (5.13.7). Every error is Blocking; there is no default substitution.
    internal static class PlayerStatsBinder
    {
        /// Returns the definition, or null after recording Blocking issues.
        internal static PlayerStatsDefinition Bind(JObject root, DataValidationReport report)
        {
            string fileName = GameDataLoader.FileNameOf(DataFileKind.PlayerStats);
            var r = new FieldReader(root, fileName, null, null, report, IssueSeverity.Blocking);
            r.Known(JsonTreeReader.SchemaVersionKey);

            double maxHealth = r.Number("maxHealth", NumberRange.Positive, true);
            double moveSpeed = r.Number("moveSpeed", NumberRange.Positive, true);
            double radius = r.Number("radius", NumberRange.Positive, true);
            double pickupRadius = r.Number("pickupRadius", NumberRange.Positive, true);
            double invulnerability = r.Number("invulnerabilitySeconds", NumberRange.NonNegative, true);
            int invulnerabilitySteps = r.Steps("invulnerabilitySeconds", invulnerability);
            string startingWeaponId = r.String("startingWeaponId", true, true);

            XpCurveDefinition xpCurve = BindXpCurve(r);
            PickupsDefinition pickups = BindPickups(r);
            r.ReportUnknownKeys();

            if (r.HasErrors)
            {
                report.Add(new ValidationIssue(IssueSeverity.Blocking, fileName, null, null,
                    "플레이어 기본 스탯에 오류 " + r.ErrorCount.ToString(CultureInfo.InvariantCulture)
                    + "건이 있어 게임을 시작할 수 없습니다. 기본값으로 대체하지 않습니다. 위 항목을 고친 뒤 다시 실행하세요.",
                    JsonTreeReader.LineOf(root), JsonTreeReader.ColumnOf(root)));
                return null;
            }

            return new PlayerStatsDefinition
            {
                MaxHealth = (float)maxHealth,
                MoveSpeed = (float)moveSpeed,
                MoveSpeedPerStep = StepMath.PerSecondToPerStep((float)moveSpeed),
                Radius = (float)radius,
                PickupRadius = (float)pickupRadius,
                InvulnerabilitySeconds = (float)invulnerability,
                InvulnerabilitySteps = invulnerabilitySteps,
                StartingWeaponId = startingWeaponId,
                XpCurve = xpCurve,
                Pickups = pickups,
            };
        }

        private static XpCurveDefinition BindXpCurve(FieldReader r)
        {
            JObject obj = r.ObjectField("xpCurve", true);
            if (obj == null)
            {
                return null;
            }

            FieldReader c = r.Child(obj, "xpCurve");
            int errorsBefore = c.ErrorCount;
            JArray array = c.ArrayField("thresholds", true);
            var thresholds = new List<int>();
            if (array != null)
            {
                if (array.Count == 0)
                {
                    c.Error("thresholds", "값이 1개 이상 있어야 합니다.", array);
                }

                for (int i = 0; i < array.Count; i++)
                {
                    thresholds.Add(c.ParseInt(array[i], "thresholds[" + i.ToString(CultureInfo.InvariantCulture) + "]", 1, 1));
                }
            }

            int growth = c.Int("growthAfterTable", 0, true);
            c.ReportUnknownKeys();
            return c.ErrorCount > errorsBefore ? null : new XpCurveDefinition(thresholds, growth);
        }

        private static PickupsDefinition BindPickups(FieldReader r)
        {
            JObject obj = r.ObjectField("pickups", true);
            if (obj == null)
            {
                return null;
            }

            FieldReader c = r.Child(obj, "pickups");
            var pickups = new PickupsDefinition
            {
                GemAssetKey = c.String("gemAssetKey", true),
                GemRadius = (float)c.Number("gemRadius", NumberRange.Positive, true),
                GemXpValue = c.Int("gemXpValue", 1, true),
                GemAttractSpeed = (float)c.Number("gemAttractSpeed", NumberRange.Positive, true),
                ChestAssetKey = c.String("chestAssetKey", true),
                ChestRadius = (float)c.Number("chestRadius", NumberRange.Positive, true),
            };
            pickups.GemAttractSpeedPerStep = StepMath.PerSecondToPerStep(pickups.GemAttractSpeed);
            c.ReportUnknownKeys();
            return pickups;
        }
    }
}
