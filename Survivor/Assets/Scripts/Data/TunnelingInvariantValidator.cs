using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Data
{
    /// Tunneling invariant of F18 (TAC-F11-4): stage 1 per object v/60 <= r; stage 2 only for stage-1 failures,
    /// (v_a + v_b)/60 <= r_a + r_b against every object on a layer that shares an R5 channel. Warnings only.
    public static class TunnelingInvariantValidator
    {
        /// Runs both stages and adds Warning issues; never blocks.
        public static void Validate(GameData data, DataValidationReport report)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var specs = new List<MovingObjectSpec>();
            var files = new List<string>();
            Enumerate(data, specs, files);
            for (int a = 0; a < specs.Count; a++)
            {
                MovingObjectSpec sa = specs[a];
                if (sa.SatisfiesInvariant)
                {
                    continue;
                }

                report.Add(new ValidationIssue(IssueSeverity.Warning, files[a], null, sa.Source,
                    "터널링 위험: 한 스텝 이동 거리 " + Num(sa.SpeedPerStep) + "가 반지름 " + Num(sa.Radius)
                    + "보다 큽니다(v/60 ≤ r 위반). 속도를 낮추거나 반지름을 키우세요."));

                for (int b = 0; b < specs.Count; b++)
                {
                    if (b == a)
                    {
                        continue;
                    }

                    MovingObjectSpec sb = specs[b];
                    CollisionChannel channel;
                    bool aIsA;
                    if (!CollisionChannelTable.TryGetChannel(sa.Layer, sb.Layer, out channel, out aIsA))
                    {
                        continue;
                    }

                    float combinedStep = StepMath.PerSecondToPerStep(sa.SpeedPerSecond + sb.SpeedPerSecond);
                    if (combinedStep <= sa.Radius + sb.Radius)
                    {
                        continue;
                    }

                    report.Add(new ValidationIssue(IssueSeverity.Warning, files[a], null, sa.Source,
                        "터널링 위험: " + sb.Source + "(" + files[b] + ")와 서로 통과할 수 있습니다(채널 " + channel
                        + ", 두 속도 합/60 = " + Num(combinedStep) + " > 반지름 합 " + Num(sa.Radius + sb.Radius) + ")."));
                }
            }
        }

        /// Every moving object kind of the data set, including the player (P2 max level applied) and attracted gems (D1).
        /// Orbit speed = angular speed * orbit radius + player max speed (D2). Zones are listed with speed 0.
        public static IReadOnlyList<MovingObjectSpec> EnumerateMovingObjects(GameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var specs = new List<MovingObjectSpec>();
            Enumerate(data, specs, new List<string>());
            return specs;
        }

        /// Player speed with the largest moveSpeed passive bonus applied (0 without player stats).
        public static float PlayerMaxSpeed(GameData data)
        {
            if (data.PlayerStats == null)
            {
                return 0f;
            }

            float bonus = 0f;
            for (int i = 0; i < data.Passives.Count; i++)
            {
                PassiveDefinition passive = data.Passives[i];
                if (passive.Stat == PassiveStat.MoveSpeed && passive.Levels.Count > 0)
                {
                    bonus = Math.Max(bonus, passive.Levels[passive.Levels.Count - 1].Bonus);
                }
            }

            return StatMath.ApplyMultiplier(data.PlayerStats.MoveSpeed, bonus);
        }

        private static void Enumerate(GameData data, List<MovingObjectSpec> specs, List<string> files)
        {
            string playerFile = GameDataLoader.FileNameOf(DataFileKind.PlayerStats);
            string weaponFile = GameDataLoader.FileNameOf(DataFileKind.Weapons);
            string monsterFile = GameDataLoader.FileNameOf(DataFileKind.Monsters);
            float playerMax = PlayerMaxSpeed(data);

            PlayerStatsDefinition stats = data.PlayerStats;
            if (stats != null)
            {
                Add(specs, files, playerFile, new MovingObjectSpec("playerStats", CollisionLayer.PlayerBody, playerMax, stats.Radius));
                Add(specs, files, playerFile, new MovingObjectSpec("playerStats.pickups.gem", CollisionLayer.Gem, stats.Pickups.GemAttractSpeed, stats.Pickups.GemRadius));
            }

            for (int w = 0; w < data.Weapons.Count; w++)
            {
                WeaponDefinition weapon = data.Weapons[w];
                for (int l = 0; l < weapon.Levels.Count; l++)
                {
                    WeaponLevelDefinition level = weapon.Levels[l];
                    string source = weapon.Id + ".levels[" + l.ToString(CultureInfo.InvariantCulture) + "]";
                    switch (weapon.Behavior)
                    {
                        case WeaponBehavior.Projectile:
                        case WeaponBehavior.Homing:
                            Add(specs, files, weaponFile, new MovingObjectSpec(source, CollisionLayer.PlayerProjectile, level.ProjectileSpeed, level.ProjectileRadius));
                            break;
                        case WeaponBehavior.Orbit:
                            float linear = Mathf.Abs(level.AngularSpeedDegPerSec) * Mathf.Deg2Rad * level.OrbitRadius;
                            Add(specs, files, weaponFile, new MovingObjectSpec(source, CollisionLayer.PlayerOrb, linear + playerMax, level.OrbRadius));
                            break;
                        case WeaponBehavior.Zone:
                            Add(specs, files, weaponFile, new MovingObjectSpec(source, CollisionLayer.PlayerZone, 0f, level.ZoneRadius));
                            break;
                    }
                }
            }

            for (int m = 0; m < data.Monsters.Count; m++)
            {
                MonsterDefinition monster = data.Monsters[m];
                Add(specs, files, monsterFile, new MovingObjectSpec(monster.Id, CollisionLayer.Monster, monster.MoveSpeed, monster.Radius));
                if (monster.Charge != null)
                {
                    Add(specs, files, monsterFile, new MovingObjectSpec(monster.Id + ".charge", CollisionLayer.Monster, monster.Charge.ChargeSpeed, monster.Radius));
                }

                if (monster.Ranged != null && monster.Ranged.Projectile != null)
                {
                    EnemyProjectileDefinition projectile = monster.Ranged.Projectile;
                    Add(specs, files, monsterFile, new MovingObjectSpec(monster.Id + ".ranged.projectile", CollisionLayer.EnemyProjectile, projectile.Speed, projectile.Radius));
                }

                for (int p = 0; p < monster.BossPatterns.Count; p++)
                {
                    BossPatternDefinition pattern = monster.BossPatterns[p];
                    if (pattern.Kind == BossPatternKind.HazardZone)
                    {
                        Add(specs, files, monsterFile, new MovingObjectSpec(monster.Id + ".bossPatterns[" + p.ToString(CultureInfo.InvariantCulture) + "]",
                            CollisionLayer.EnemyZone, 0f, pattern.ZoneRadius));
                    }
                }
            }
        }

        private static void Add(List<MovingObjectSpec> specs, List<string> files, string file, MovingObjectSpec spec)
        {
            specs.Add(spec);
            files.Add(file);
        }

        private static string Num(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
