using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Binds weapons.json (5.13.1): behavior-specific level fields, level count 5 (base) or 1 (evolvedOnly), seconds -> steps.
    internal static class WeaponsBinder
    {
        internal const int BaseLevelCount = 5;
        internal const int EvolvedLevelCount = 1;

        private static readonly string[] BehaviorNames = { "projectile", "homing", "orbit", "zone" };
        private static readonly WeaponBehavior[] BehaviorValues = { WeaponBehavior.Projectile, WeaponBehavior.Homing, WeaponBehavior.Orbit, WeaponBehavior.Zone };
        private static readonly string[] PlacementNames = { "nearestEnemy", "player" };
        private static readonly ZonePlacement[] PlacementValues = { ZonePlacement.NearestEnemy, ZonePlacement.Player };

        private static readonly HashSet<string> AllLevelKeys = new HashSet<string>
        {
            "damage", "cooldownSeconds", "projectileCount", "spreadDegrees", "projectileSpeed", "projectileRadius",
            "lifetimeSeconds", "pierceCount", "orbCount", "orbRadius", "orbitRadius", "angularSpeedDegPerSec",
            "hitIntervalSeconds", "zoneRadius", "durationSeconds", "placement",
        };

        /// Returns the valid weapons in file order, or null when the file-level structure is broken.
        internal static List<WeaponDefinition> Bind(JObject root, DataValidationReport report)
        {
            var weapons = new List<WeaponDefinition>();
            return BinderSupport.BindList(root, DataFileKind.Weapons, "weapons", report, weapons, BindOne) ? weapons : null;
        }

        private static WeaponDefinition BindOne(FieldReader r)
        {
            string displayName = r.String("displayName", true);
            WeaponBehavior behavior;
            bool behaviorValid = r.TryEnum("behavior", BehaviorNames, BehaviorValues, true, WeaponBehavior.Projectile, out behavior);
            string assetKey = r.String("assetKey", true);
            string iconKey = r.String("iconKey", false);
            bool evolvedOnly = r.Bool("evolvedOnly", false);
            JArray levelsArray = r.ArrayField("levels", true);

            var levels = new List<WeaponLevelDefinition>();
            if (levelsArray != null && behaviorValid)
            {
                int expected = evolvedOnly ? EvolvedLevelCount : BaseLevelCount;
                if (levelsArray.Count != expected)
                {
                    r.Error("levels", (evolvedOnly ? "진화 무기" : "기본 무기") + "의 단계 수는 정확히 " + expected.ToString(CultureInfo.InvariantCulture)
                        + "개여야 합니다(현재 " + levelsArray.Count.ToString(CultureInfo.InvariantCulture) + "개).", levelsArray);
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

                        levels.Add(BindLevel(r.Child(levelObj, path), behavior));
                    }
                }
            }

            return new WeaponDefinition
            {
                Id = r.DefinitionId,
                DisplayName = displayName,
                Behavior = behavior,
                AssetKey = assetKey,
                IconKey = iconKey ?? assetKey,
                EvolvedOnly = evolvedOnly,
                Levels = levels,
            };
        }

        private static WeaponLevelDefinition BindLevel(FieldReader c, WeaponBehavior behavior)
        {
            var level = new WeaponLevelDefinition
            {
                Damage = (float)c.Number("damage", NumberRange.Positive, true),
                ProjectileCount = 1,
                Placement = ZonePlacement.NearestEnemy,
            };

            switch (behavior)
            {
                case WeaponBehavior.Projectile:
                case WeaponBehavior.Homing:
                {
                    double cooldown = c.Number("cooldownSeconds", NumberRange.Positive, true);
                    double lifetime = c.Number("lifetimeSeconds", NumberRange.Positive, true);
                    level.CooldownSeconds = (float)cooldown;
                    level.CooldownSteps = c.IntervalSteps("cooldownSeconds", cooldown);
                    level.ProjectileCount = c.Int("projectileCount", 1, false, 1);
                    level.SpreadDegrees = (float)c.Number("spreadDegrees", NumberRange.NonNegative, false);
                    level.ProjectileSpeed = (float)c.Number("projectileSpeed", NumberRange.Positive, true);
                    level.ProjectileSpeedPerStep = StepMath.PerSecondToPerStep(level.ProjectileSpeed);
                    level.ProjectileRadius = (float)c.Number("projectileRadius", NumberRange.Positive, true);
                    level.LifetimeSeconds = (float)lifetime;
                    level.LifetimeSteps = c.IntervalSteps("lifetimeSeconds", lifetime);
                    level.PierceCount = c.Int("pierceCount", 0, false, 0);
                    break;
                }

                case WeaponBehavior.Orbit:
                {
                    level.OrbCount = c.Int("orbCount", 1, true, 1);
                    level.OrbRadius = (float)c.Number("orbRadius", NumberRange.Positive, true);
                    level.OrbitRadius = (float)c.Number("orbitRadius", NumberRange.Positive, true);
                    level.AngularSpeedDegPerSec = (float)c.Number("angularSpeedDegPerSec", NumberRange.Any, true);
                    level.AngularSpeedRadPerStep = StepMath.DegPerSecondToRadPerStep(level.AngularSpeedDegPerSec);
                    double interval = c.Number("hitIntervalSeconds", NumberRange.Positive, true);
                    level.HitIntervalSeconds = (float)interval;
                    level.HitIntervalSteps = c.IntervalSteps("hitIntervalSeconds", interval);
                    break;
                }

                case WeaponBehavior.Zone:
                {
                    double cooldown = c.Number("cooldownSeconds", NumberRange.Positive, true);
                    double interval = c.Number("hitIntervalSeconds", NumberRange.Positive, true);
                    double duration = c.Number("durationSeconds", NumberRange.Positive, true);
                    level.CooldownSeconds = (float)cooldown;
                    level.CooldownSteps = c.IntervalSteps("cooldownSeconds", cooldown);
                    level.HitIntervalSeconds = (float)interval;
                    level.HitIntervalSteps = c.IntervalSteps("hitIntervalSeconds", interval);
                    level.ZoneRadius = (float)c.Number("zoneRadius", NumberRange.Positive, true);
                    level.DurationSeconds = (float)duration;
                    level.DurationSteps = c.IntervalSteps("durationSeconds", duration);
                    ZonePlacement placement;
                    c.TryEnum("placement", PlacementNames, PlacementValues, false, ZonePlacement.NearestEnemy, out placement);
                    level.Placement = placement;
                    break;
                }
            }

            c.ReportUnknownKeys(AllLevelKeys, BehaviorNames[(int)behavior]);
            return level;
        }
    }
}
