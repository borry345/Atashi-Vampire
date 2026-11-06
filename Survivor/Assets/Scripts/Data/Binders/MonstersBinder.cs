using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Binds monsters.json (5.13.2): common fields plus charge / ranged / bossPatterns by behavior.
    internal static class MonstersBinder
    {
        private static readonly string[] BehaviorNames = { "chaser", "charger", "ranged", "boss" };
        private static readonly MonsterBehavior[] BehaviorValues = { MonsterBehavior.Chaser, MonsterBehavior.Charger, MonsterBehavior.Ranged, MonsterBehavior.Boss };
        private static readonly string[] PatternNames = { "hazardZone", "summon" };
        private static readonly BossPatternKind[] PatternValues = { BossPatternKind.HazardZone, BossPatternKind.Summon };
        private static readonly string[] PlacementNames = { "nearestEnemy", "player" };
        private static readonly ZonePlacement[] PlacementValues = { ZonePlacement.NearestEnemy, ZonePlacement.Player };

        private static readonly HashSet<string> BehaviorKeys = new HashSet<string> { "charge", "ranged", "bossPatterns" };
        private static readonly HashSet<string> PatternKeys = new HashSet<string>
        {
            "type", "cooldownSeconds", "zoneRadius", "durationSeconds", "damage", "assetKey", "placement", "monsterId", "count", "summonRadius",
        };

        /// Returns the valid monsters in file order, or null when the file-level structure is broken.
        internal static List<MonsterDefinition> Bind(JObject root, DataValidationReport report)
        {
            var monsters = new List<MonsterDefinition>();
            return BinderSupport.BindList(root, DataFileKind.Monsters, "monsters", report, monsters, BindOne) ? monsters : null;
        }

        private static MonsterDefinition BindOne(FieldReader r)
        {
            var def = new MonsterDefinition
            {
                Id = r.DefinitionId,
                DisplayName = r.String("displayName", true),
                AssetKey = r.String("assetKey", true),
                BossPatterns = new BossPatternDefinition[0],
            };

            MonsterBehavior behavior;
            bool behaviorValid = r.TryEnum("behavior", BehaviorNames, BehaviorValues, true, MonsterBehavior.Chaser, out behavior);
            def.Behavior = behavior;
            def.MaxHealth = (float)r.Number("maxHealth", NumberRange.Positive, true);
            def.MoveSpeed = (float)r.Number("moveSpeed", NumberRange.NonNegative, true);
            def.MoveSpeedPerStep = StepMath.PerSecondToPerStep(def.MoveSpeed);
            def.Radius = (float)r.Number("radius", NumberRange.Positive, true);
            def.ContactDamage = (float)r.Number("contactDamage", NumberRange.NonNegative, true);
            def.IsLarge = MonsterSizeRules.IsLarge(def.Radius, behavior == MonsterBehavior.Boss);

            if (!behaviorValid)
            {
                // Behavior-specific objects cannot be judged without a valid behavior; mark them known to avoid noise.
                r.Known("charge");
                r.Known("ranged");
                r.Known("bossPatterns");
                return def;
            }

            switch (behavior)
            {
                case MonsterBehavior.Charger:
                    def.Charge = BindCharge(r);
                    break;
                case MonsterBehavior.Ranged:
                    def.Ranged = BindRanged(r);
                    break;
                case MonsterBehavior.Boss:
                    def.BossPatterns = BindBossPatterns(r);
                    break;
            }

            r.ReportUnknownKeys(BehaviorKeys, BehaviorNames[(int)behavior]);
            return def;
        }

        private static ChargeDefinition BindCharge(FieldReader r)
        {
            JObject obj = r.ObjectField("charge", true);
            if (obj == null)
            {
                return null;
            }

            FieldReader c = r.Child(obj, "charge");
            var charge = new ChargeDefinition
            {
                TriggerDistance = (float)c.Number("triggerDistance", NumberRange.Positive, true),
                ChargeSpeed = (float)c.Number("chargeSpeed", NumberRange.Positive, true),
            };
            charge.ChargeSpeedPerStep = StepMath.PerSecondToPerStep(charge.ChargeSpeed);
            double duration = c.Number("durationSeconds", NumberRange.Positive, true);
            double cooldown = c.Number("cooldownSeconds", NumberRange.Positive, true);
            charge.DurationSeconds = (float)duration;
            charge.DurationSteps = c.IntervalSteps("durationSeconds", duration);
            charge.CooldownSeconds = (float)cooldown;
            charge.CooldownSteps = c.IntervalSteps("cooldownSeconds", cooldown);
            c.ReportUnknownKeys();
            return charge;
        }

        private static RangedDefinition BindRanged(FieldReader r)
        {
            JObject obj = r.ObjectField("ranged", true);
            if (obj == null)
            {
                return null;
            }

            FieldReader c = r.Child(obj, "ranged");
            var ranged = new RangedDefinition { AttackRange = (float)c.Number("attackRange", NumberRange.Positive, true) };
            double fireCooldown = c.Number("fireCooldownSeconds", NumberRange.Positive, true);
            ranged.FireCooldownSeconds = (float)fireCooldown;
            ranged.FireCooldownSteps = c.IntervalSteps("fireCooldownSeconds", fireCooldown);

            JObject projectileObj = c.ObjectField("projectile", true);
            if (projectileObj != null)
            {
                FieldReader p = c.Child(projectileObj, "projectile");
                var projectile = new EnemyProjectileDefinition
                {
                    AssetKey = p.String("assetKey", true),
                    Speed = (float)p.Number("speed", NumberRange.Positive, true),
                    Radius = (float)p.Number("radius", NumberRange.Positive, true),
                    Damage = (float)p.Number("damage", NumberRange.NonNegative, true),
                };
                projectile.SpeedPerStep = StepMath.PerSecondToPerStep(projectile.Speed);
                double lifetime = p.Number("lifetimeSeconds", NumberRange.Positive, true);
                projectile.LifetimeSeconds = (float)lifetime;
                projectile.LifetimeSteps = p.IntervalSteps("lifetimeSeconds", lifetime);
                p.ReportUnknownKeys();
                ranged.Projectile = projectile;
            }

            c.ReportUnknownKeys();
            return ranged;
        }

        private static IReadOnlyList<BossPatternDefinition> BindBossPatterns(FieldReader r)
        {
            var patterns = new List<BossPatternDefinition>();
            JArray array = r.ArrayField("bossPatterns", true);
            if (array == null)
            {
                return patterns;
            }

            if (array.Count == 0)
            {
                r.Error("bossPatterns", "보스 패턴이 1개 이상 있어야 합니다.", array);
                return patterns;
            }

            for (int i = 0; i < array.Count; i++)
            {
                string path = "bossPatterns[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                var obj = array[i] as JObject;
                if (obj == null)
                {
                    r.Error(path, "패턴은 객체여야 합니다(현재: " + FieldReader.TypeName(array[i]) + ").", array[i]);
                    continue;
                }

                FieldReader c = r.Child(obj, path);
                BossPatternKind kind;
                if (!c.TryEnum("type", PatternNames, PatternValues, true, BossPatternKind.HazardZone, out kind))
                {
                    continue;
                }

                var pattern = new BossPatternDefinition { Kind = kind, Placement = ZonePlacement.Player };
                double cooldown = c.Number("cooldownSeconds", NumberRange.Positive, true);
                pattern.CooldownSeconds = (float)cooldown;
                pattern.CooldownSteps = c.IntervalSteps("cooldownSeconds", cooldown);
                if (kind == BossPatternKind.HazardZone)
                {
                    pattern.ZoneRadius = (float)c.Number("zoneRadius", NumberRange.Positive, true);
                    double duration = c.Number("durationSeconds", NumberRange.Positive, true);
                    pattern.DurationSeconds = (float)duration;
                    pattern.DurationSteps = c.IntervalSteps("durationSeconds", duration);
                    pattern.Damage = (float)c.Number("damage", NumberRange.NonNegative, true);
                    pattern.AssetKey = c.String("assetKey", true);
                    ZonePlacement placement;
                    c.TryEnum("placement", PlacementNames, PlacementValues, false, ZonePlacement.Player, out placement);
                    pattern.Placement = placement;
                }
                else
                {
                    pattern.MonsterId = c.String("monsterId", true, true);
                    pattern.Count = c.Int("count", 1, true, 1);
                    pattern.SummonRadius = (float)c.Number("summonRadius", NumberRange.Positive, true);
                }

                c.ReportUnknownKeys(PatternKeys, PatternNames[(int)kind]);
                patterns.Add(pattern);
            }

            return patterns;
        }
    }
}
