using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Tests.EditMode.Data
{
    /// Normal files: loaded values match the file field by field, seconds become steps (R1), speeds become per-step (R3).
    public sealed class GameDataLoaderValidTests
    {
        private GameData _data;
        private DataValidationReport _report;

        [SetUp]
        public void SetUp()
        {
            (_data, _report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
        }

        [Test]
        public void ValidTexts_HaveNoIssues_AndArePlayable()
        {
            Assert.AreEqual(0, _report.Issues.Count, DataTestFixtures.Dump(_report));
            Assert.IsTrue(_report.IsPlayable);
            Assert.AreEqual(0, _report.BlockingIssues.Count());
        }

        [Test]
        public void DefinitionCounts_AndFileOrder()
        {
            CollectionAssert.AreEqual(new[] { "W1", "W2", "W3", "W4", "E1", "E2" }, _data.Weapons.Select(w => w.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "M1", "M2", "M3", "M4" }, _data.Monsters.Select(m => m.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "evolve_W1", "evolve_W2" }, _data.Combinations.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "P1", "P2", "P3" }, _data.Passives.Select(p => p.Id).ToArray());
            Assert.AreEqual(4, _data.SpawnTable.Segments.Count);
            Assert.AreEqual(2, _data.LevelUpPool.Rules.Count);
            Assert.IsNotNull(_data.PlayerStats);
        }

        [Test]
        public void Weapon_ProjectileFields()
        {
            Assert.IsTrue(_data.TryGetWeapon("W1", out WeaponDefinition w1));
            Assert.AreEqual("투사체", w1.DisplayName);
            Assert.AreEqual(WeaponBehavior.Projectile, w1.Behavior);
            Assert.AreEqual("shape.projectile.white", w1.AssetKey);
            Assert.AreEqual("shape.projectile.white", w1.IconKey, "iconKey defaults to assetKey");
            Assert.IsFalse(w1.EvolvedOnly);
            Assert.AreEqual(5, w1.Levels.Count);

            WeaponLevelDefinition l1 = w1.Levels[0];
            Assert.AreEqual(10f, l1.Damage);
            Assert.AreEqual(1.0f, l1.CooldownSeconds);
            Assert.AreEqual(60, l1.CooldownSteps);
            Assert.AreEqual(12f, l1.ProjectileSpeed);
            Assert.AreEqual(12f / 60f, l1.ProjectileSpeedPerStep);
            Assert.AreEqual(0.25f, l1.ProjectileRadius);
            Assert.AreEqual(120, l1.LifetimeSteps);
            Assert.AreEqual(1, l1.ProjectileCount, "default projectileCount");
            Assert.AreEqual(0f, l1.SpreadDegrees, "default spreadDegrees");
            Assert.AreEqual(0, l1.PierceCount, "default pierceCount");

            WeaponLevelDefinition l4 = w1.Levels[3];
            Assert.AreEqual(2, l4.ProjectileCount);
            Assert.AreEqual(10f, l4.SpreadDegrees);
            Assert.AreEqual(42, l4.CooldownSteps, "0.7 s");
            Assert.AreEqual(36, w1.Levels[4].CooldownSteps, "0.6 s");
        }

        [Test]
        public void Weapon_OrbitFields_HitIntervalIsSteps()
        {
            Assert.IsTrue(_data.TryGetWeapon("W2", out WeaponDefinition w2));
            Assert.AreEqual(WeaponBehavior.Orbit, w2.Behavior);
            WeaponLevelDefinition l3 = w2.Levels[2];
            Assert.AreEqual(3, l3.OrbCount);
            Assert.AreEqual(0.45f, l3.OrbRadius);
            Assert.AreEqual(2.2f, l3.OrbitRadius);
            Assert.AreEqual(180f, l3.AngularSpeedDegPerSec);
            Assert.AreEqual(StepMath.DegPerSecondToRadPerStep(180f), l3.AngularSpeedRadPerStep);
            Assert.AreEqual(0.45f, l3.HitIntervalSeconds);
            Assert.AreEqual(27, l3.HitIntervalSteps);
            Assert.AreEqual(0, l3.CooldownSteps, "orbit has no cooldown");
        }

        [Test]
        public void Weapon_ZoneFields()
        {
            Assert.IsTrue(_data.TryGetWeapon("W3", out WeaponDefinition w3));
            WeaponLevelDefinition l4 = w3.Levels[3];
            Assert.AreEqual(144, l4.CooldownSteps, "2.4 s");
            Assert.AreEqual(1.75f, l4.ZoneRadius);
            Assert.AreEqual(2.5f, l4.DurationSeconds);
            Assert.AreEqual(150, l4.DurationSteps);
            Assert.AreEqual(30, l4.HitIntervalSteps);
            Assert.AreEqual(ZonePlacement.NearestEnemy, l4.Placement, "default placement");
        }

        [Test]
        public void Weapon_HomingAndEvolved()
        {
            Assert.IsTrue(_data.TryGetWeapon("W4", out WeaponDefinition w4));
            Assert.AreEqual(WeaponBehavior.Homing, w4.Behavior);
            Assert.AreEqual(15f, w4.Levels[4].SpreadDegrees);

            Assert.IsTrue(_data.TryGetWeapon("E1", out WeaponDefinition e1));
            Assert.IsTrue(e1.EvolvedOnly);
            Assert.AreEqual(1, e1.Levels.Count);
            Assert.AreEqual(12, e1.Levels[0].CooldownSteps, "0.2 s");
            Assert.AreEqual(3, e1.Levels[0].ProjectileCount);

            Assert.IsTrue(_data.TryGetWeapon("E2", out WeaponDefinition e2));
            Assert.AreEqual(3.0f, e2.Levels[0].OrbitRadius);
            Assert.AreEqual(24, e2.Levels[0].HitIntervalSteps);
            Assert.IsFalse(_data.TryGetWeapon("w1", out _), "ids are case-sensitive");
            Assert.IsFalse(_data.TryGetWeapon(null, out _));
        }

        [Test]
        public void Monster_Fields()
        {
            Assert.IsTrue(_data.TryGetMonster("M1", out MonsterDefinition m1));
            Assert.AreEqual("좀비", m1.DisplayName);
            Assert.AreEqual(MonsterBehavior.Chaser, m1.Behavior);
            Assert.AreEqual(10f, m1.MaxHealth);
            Assert.AreEqual(2.0f, m1.MoveSpeed);
            Assert.AreEqual(2.0f / 60f, m1.MoveSpeedPerStep);
            Assert.AreEqual(0.45f, m1.Radius);
            Assert.AreEqual(5f, m1.ContactDamage);
            Assert.IsFalse(m1.IsLarge);
            Assert.IsNull(m1.Charge);
            Assert.IsNull(m1.Ranged);
            Assert.AreEqual(0, m1.BossPatterns.Count);

            Assert.IsTrue(_data.TryGetMonster("M2", out MonsterDefinition m2));
            Assert.AreEqual(6f, m2.Charge.TriggerDistance);
            Assert.AreEqual(18f, m2.Charge.ChargeSpeed);
            Assert.AreEqual(0.3f, m2.Charge.ChargeSpeedPerStep, 1e-6f);
            Assert.AreEqual(30, m2.Charge.DurationSteps);
            Assert.AreEqual(180, m2.Charge.CooldownSteps);

            Assert.IsTrue(_data.TryGetMonster("M3", out MonsterDefinition m3));
            Assert.AreEqual(8f, m3.Ranged.AttackRange);
            Assert.AreEqual(120, m3.Ranged.FireCooldownSteps);
            Assert.AreEqual("shape.enemyshot.red", m3.Ranged.Projectile.AssetKey);
            Assert.AreEqual(0.1f, m3.Ranged.Projectile.SpeedPerStep, 1e-6f);
            Assert.AreEqual(0.2f, m3.Ranged.Projectile.Radius);
            Assert.AreEqual(6f, m3.Ranged.Projectile.Damage);
            Assert.AreEqual(180, m3.Ranged.Projectile.LifetimeSteps);
        }

        [Test]
        public void Boss_Patterns()
        {
            Assert.IsTrue(_data.TryGetMonster("M4", out MonsterDefinition m4));
            Assert.AreEqual(MonsterBehavior.Boss, m4.Behavior);
            Assert.IsTrue(m4.IsLarge, "boss is always large (R25)");
            Assert.AreEqual(2, m4.BossPatterns.Count);

            BossPatternDefinition hazard = m4.BossPatterns[0];
            Assert.AreEqual(BossPatternKind.HazardZone, hazard.Kind);
            Assert.AreEqual(360, hazard.CooldownSteps);
            Assert.AreEqual(3f, hazard.ZoneRadius);
            Assert.AreEqual(120, hazard.DurationSteps);
            Assert.AreEqual(15f, hazard.Damage);
            Assert.AreEqual("shape.hazard.red", hazard.AssetKey);
            Assert.AreEqual(ZonePlacement.Player, hazard.Placement, "default boss placement is player");

            BossPatternDefinition summon = m4.BossPatterns[1];
            Assert.AreEqual(BossPatternKind.Summon, summon.Kind);
            Assert.AreEqual(480, summon.CooldownSteps);
            Assert.AreEqual("M1", summon.MonsterId);
            Assert.AreEqual(6, summon.Count);
            Assert.AreEqual(3f, summon.SummonRadius);
        }

        [Test]
        public void SpawnTable_Fields()
        {
            SpawnTableDefinition table = _data.SpawnTable;
            Assert.AreEqual(45f, table.DespawnDistance);
            Assert.AreEqual("M4", table.BossMonsterId);
            SpawnSegmentDefinition s0 = table.Segments[0];
            Assert.AreEqual(0, s0.StartStep);
            Assert.AreEqual(7200, s0.EndStep);
            Assert.AreEqual(2f, s0.SpawnsPerSecond);
            Assert.AreEqual(1, s0.Entries.Count);
            SpawnSegmentDefinition s3 = table.Segments[3];
            Assert.AreEqual(28800, s3.StartStep);
            Assert.AreEqual(36000, s3.EndStep);
            Assert.AreEqual(480f, s3.StartSeconds);
            CollectionAssert.AreEqual(new[] { "M1", "M2", "M3" }, s3.Entries.Select(e => e.MonsterId).ToArray());
            CollectionAssert.AreEqual(new[] { 4f, 2f, 2f }, s3.Entries.Select(e => e.Weight).ToArray());
        }

        [Test]
        public void Passives_Combinations_LevelUpPool()
        {
            Assert.IsTrue(_data.TryGetPassive("P1", out PassiveDefinition p1));
            Assert.AreEqual("공격 속도", p1.DisplayName);
            Assert.AreEqual("icon.p1", p1.IconKey);
            Assert.AreEqual(PassiveStat.AttackSpeed, p1.Stat);
            CollectionAssert.AreEqual(new[] { 0.10f, 0.20f, 0.30f, 0.40f, 0.50f }, p1.Levels.Select(l => l.Bonus).ToArray());
            Assert.IsTrue(_data.TryGetPassive("P2", out PassiveDefinition p2));
            Assert.AreEqual(PassiveStat.MoveSpeed, p2.Stat);

            CombinationRule rule = _data.Combinations[1];
            Assert.AreEqual("W2", rule.WeaponId);
            Assert.AreEqual("P3", rule.PassiveId);
            Assert.AreEqual("E2", rule.ResultWeaponId);

            LevelUpPoolDefinition pool = _data.LevelUpPool;
            Assert.IsFalse(pool.IsFallback);
            Assert.AreEqual(10f, pool.DefaultWeight);
            Assert.AreEqual(30f, pool.HealBaseWeight);
            Assert.AreEqual(UpgradeKind.WeaponNew, pool.Rules[0].Kind);
            Assert.AreEqual("*", pool.Rules[0].TargetId);
            Assert.AreEqual(12f, pool.Rules[1].Weight);
        }

        [Test]
        public void PlayerStats_Fields()
        {
            PlayerStatsDefinition stats = _data.PlayerStats;
            Assert.AreEqual(100f, stats.MaxHealth);
            Assert.AreEqual(5f, stats.MoveSpeed);
            Assert.AreEqual(5f / 60f, stats.MoveSpeedPerStep);
            Assert.AreEqual(0.5f, stats.Radius);
            Assert.AreEqual(2.0f, stats.PickupRadius);
            Assert.AreEqual(0.5f, stats.InvulnerabilitySeconds);
            Assert.AreEqual(30, stats.InvulnerabilitySteps);
            Assert.AreEqual("W1", stats.StartingWeaponId);
            CollectionAssert.AreEqual(new[] { 5, 8, 12, 16, 20, 25, 30 }, stats.XpCurve.Thresholds.ToArray());
            Assert.AreEqual(5, stats.XpCurve.GrowthAfterTable);
            Assert.AreEqual("shape.gem.blue", stats.Pickups.GemAssetKey);
            Assert.AreEqual(0.2f, stats.Pickups.GemRadius);
            Assert.AreEqual(1, stats.Pickups.GemXpValue);
            Assert.AreEqual(10f, stats.Pickups.GemAttractSpeed);
            Assert.AreEqual(10f / 60f, stats.Pickups.GemAttractSpeedPerStep);
            Assert.AreEqual("shape.chest.yellow", stats.Pickups.ChestAssetKey);
            Assert.AreEqual(0.5f, stats.Pickups.ChestRadius);
        }

        [Test]
        public void InvulnerabilityZero_IsAllowed_HitIntervalIsAtLeastOneStep()
        {
            var texts = DataTestFixtures.With(DataFileKind.PlayerStats, "\"invulnerabilitySeconds\": 0.5", "\"invulnerabilitySeconds\": 0");
            texts[DataFileKind.Weapons] = DataTestFixtures.ReplaceOnce(texts[DataFileKind.Weapons],
                "\"angularSpeedDegPerSec\": 180, \"hitIntervalSeconds\": 0.4 } ] },", "\"angularSpeedDegPerSec\": 180, \"hitIntervalSeconds\": 0.001 } ] },");
            var (data, report) = DataTestFixtures.Load(texts);
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
            Assert.AreEqual(0, data.PlayerStats.InvulnerabilitySteps, "SecondsToSteps allows 0 (TAC-F11-8)");
            Assert.IsTrue(data.TryGetWeapon("W2", out WeaponDefinition w2));
            Assert.AreEqual(1, w2.Levels[4].HitIntervalSteps, "SecondsToIntervalSteps is at least 1 (TAC-F11-8)");
        }

        [Test]
        public void Utf8Bom_IsAccepted()
        {
            var texts = DataTestFixtures.ValidTexts();
            texts[DataFileKind.PlayerStats] = "﻿" + texts[DataFileKind.PlayerStats];
            var (data, report) = DataTestFixtures.Load(texts);
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
            Assert.IsNotNull(data.PlayerStats);
        }

        [Test]
        public void LoadFromTexts_IsDeterministic()
        {
            var (_, second) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Passives, "{ broken"));
            var (_, third) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Passives, "{ broken"));
            CollectionAssert.AreEqual(second.Issues.Select(i => i.ToString()).ToList(), third.Issues.Select(i => i.ToString()).ToList());
        }

        [Test]
        public void FileNameOf_MatchesSchemaTable()
        {
            var expected = new Dictionary<DataFileKind, string>
            {
                { DataFileKind.Weapons, "weapons.json" }, { DataFileKind.Monsters, "monsters.json" },
                { DataFileKind.Combinations, "combinations.json" }, { DataFileKind.SpawnTable, "spawnTable.json" },
                { DataFileKind.Passives, "passives.json" }, { DataFileKind.LevelUpPool, "levelUpPool.json" },
                { DataFileKind.PlayerStats, "playerStats.json" },
            };
            foreach (KeyValuePair<DataFileKind, string> pair in expected)
            {
                Assert.AreEqual(pair.Value, GameDataLoader.FileNameOf(pair.Key));
            }

            Assert.AreEqual("Data", GameDataLoader.DataFolder);
        }
    }
}
