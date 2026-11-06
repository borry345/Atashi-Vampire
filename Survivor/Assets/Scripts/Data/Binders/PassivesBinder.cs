using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Survivor.Data
{
    /// Binds passives.json (5.13.5): exactly five cumulative bonus levels per passive.
    internal static class PassivesBinder
    {
        internal const int LevelCount = 5;

        private static readonly string[] StatNames = { "attackSpeed", "moveSpeed", "pickupRadius" };
        private static readonly PassiveStat[] StatValues = { PassiveStat.AttackSpeed, PassiveStat.MoveSpeed, PassiveStat.PickupRadius };

        /// Returns the valid passives in file order, or null when the file-level structure is broken.
        internal static List<PassiveDefinition> Bind(JObject root, DataValidationReport report)
        {
            var passives = new List<PassiveDefinition>();
            return BinderSupport.BindList(root, DataFileKind.Passives, "passives", report, passives, BindOne) ? passives : null;
        }

        private static PassiveDefinition BindOne(FieldReader r)
        {
            string displayName = r.String("displayName", true);
            string iconKey = r.String("iconKey", true);
            PassiveStat stat;
            r.TryEnum("stat", StatNames, StatValues, true, PassiveStat.AttackSpeed, out stat);
            JArray levelsArray = r.ArrayField("levels", true);

            var levels = new List<PassiveLevelDefinition>();
            if (levelsArray != null)
            {
                if (levelsArray.Count != LevelCount)
                {
                    r.Error("levels", "패시브 단계 수는 정확히 5개여야 합니다(현재 " + levelsArray.Count.ToString(CultureInfo.InvariantCulture) + "개).", levelsArray);
                }
                else
                {
                    for (int i = 0; i < levelsArray.Count; i++)
                    {
                        string path = "levels[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                        var levelObj = levelsArray[i] as JObject;
                        if (levelObj == null)
                        {
                            r.Error(path, "단계 값은 객체여야 합니다(현재: " + FieldReader.TypeName(levelsArray[i]) + ").", levelsArray[i]);
                            continue;
                        }

                        FieldReader c = r.Child(levelObj, path);
                        levels.Add(new PassiveLevelDefinition { Bonus = (float)c.Number("bonus", NumberRange.NonNegative, true) });
                        c.ReportUnknownKeys();
                    }
                }
            }

            return new PassiveDefinition
            {
                Id = r.DefinitionId,
                DisplayName = displayName,
                IconKey = iconKey,
                Stat = stat,
                Levels = levels,
            };
        }
    }
}
