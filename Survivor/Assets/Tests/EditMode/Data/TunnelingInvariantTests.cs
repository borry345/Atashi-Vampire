using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Tests.EditMode.Data
{
    /// TAC-F11-4 tunneling invariant and E3 over the real StreamingAssets files.
    public sealed class TunnelingInvariantTests
    {
        [Test]
        public void E3_ShippedData_EveryMovingObjectSatisfiesInvariant()
        {
            var (data, report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.IsTrue(report.IsPlayable, DataTestFixtures.Dump(report));
            Assert.AreEqual(0, report.Issues.Count, "shipped data loads without warnings\n" + DataTestFixtures.Dump(report));

            IReadOnlyList<MovingObjectSpec> specs = TunnelingInvariantValidator.EnumerateMovingObjects(data);
            Assert.Greater(specs.Count, 0);
            foreach (MovingObjectSpec spec in specs)
            {
                Assert.LessOrEqual(spec.SpeedPerSecond / 60f, spec.Radius, spec.Source + " (" + spec.Layer + ") violates v/60 <= r");
            }
        }

        [Test]
        public void ShippedData_DefinitionCounts()
        {
            var (data, _) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.AreEqual(6, data.Weapons.Count);
            Assert.AreEqual(4, data.Monsters.Count);
            Assert.AreEqual(2, data.Combinations.Count);
            Assert.AreEqual(4, data.SpawnTable.Segments.Count);
            Assert.AreEqual(3, data.Passives.Count);
            Assert.AreEqual(2, data.LevelUpPool.Rules.Count);
            Assert.IsNotNull(data.PlayerStats);
            CollectionAssert.AreEqual(new[] { 1.0f, 1.5f, 2.2f, 3.0f }, data.SpawnTable.Segments.Select(s => s.HpMultiplier).ToArray(), "5.13.4 hpMultiplier per segment");
        }

        [Test]
        public void TACF3_21_ShippedData_BossHasOnlySummon_AndNoEnemyZone()
        {
            var (data, report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.IsTrue(report.IsPlayable, DataTestFixtures.Dump(report));
            Assert.AreEqual(0, report.Issues.Count, "shipped data loads without warnings\n" + DataTestFixtures.Dump(report));

            Assert.IsTrue(data.TryGetMonster("M4", out MonsterDefinition boss));
            Assert.AreEqual(1, boss.BossPatterns.Count, "pattern 1 (hazardZone) was removed from the shipped data");
            Assert.AreEqual(BossPatternKind.Summon, boss.BossPatterns[0].Kind);
            Assert.IsFalse(data.Monsters.SelectMany(m => m.BossPatterns).Any(p => p.Kind == BossPatternKind.HazardZone));

            IReadOnlyList<MovingObjectSpec> specs = TunnelingInvariantValidator.EnumerateMovingObjects(data);
            Assert.IsFalse(specs.Any(s => s.Layer == CollisionLayer.EnemyZone), "no enemy zone in the shipped data");
        }

        [Test]
        public void TACF3_21_HazardZoneFixture_StillBindsWithoutWarnings()
        {
            // The fixture keeps the 5.13.2 hazardZone entry: schema, binder and enumeration stay valid.
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
            Assert.IsTrue(data.TryGetMonster("M4", out MonsterDefinition boss));
            Assert.AreEqual(2, boss.BossPatterns.Count);
            BossPatternDefinition zone = boss.BossPatterns[0];
            Assert.AreEqual(BossPatternKind.HazardZone, zone.Kind);
            Assert.AreEqual(3f, zone.ZoneRadius);
            Assert.AreEqual(120, zone.DurationSteps);
            Assert.AreEqual(360, zone.CooldownSteps);
            Assert.AreEqual(15f, zone.Damage);
            Assert.AreEqual("shape.hazard.red", zone.AssetKey);
            Assert.AreEqual(BossPatternKind.Summon, boss.BossPatterns[1].Kind);

            MovingObjectSpec spec = TunnelingInvariantValidator.EnumerateMovingObjects(data).Single(s => s.Layer == CollisionLayer.EnemyZone);
            Assert.AreEqual("M4.bossPatterns[0]", spec.Source);
            Assert.AreEqual(0f, spec.SpeedPerSecond);
            Assert.AreEqual(3f, spec.Radius);
        }

        [Test]
        public void Enumerate_CoversPlayerGemWeaponsMonsters()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            IReadOnlyList<MovingObjectSpec> specs = TunnelingInvariantValidator.EnumerateMovingObjects(data);
            var layers = new HashSet<CollisionLayer>(specs.Select(s => s.Layer));
            foreach (CollisionLayer layer in new[] { CollisionLayer.PlayerBody, CollisionLayer.Gem, CollisionLayer.PlayerProjectile, CollisionLayer.PlayerOrb,
                         CollisionLayer.PlayerZone, CollisionLayer.Monster, CollisionLayer.EnemyProjectile, CollisionLayer.EnemyZone })
            {
                Assert.IsTrue(layers.Contains(layer), layer + " missing");
            }

            // Fixture data (keeps the boss hazardZone, TAC-F3-21): 4 base weapons x 5 levels + 2 evolved, player, gem, 4 monsters + charge + ranged projectile + boss hazard.
            Assert.AreEqual(22 + 2 + 4 + 3, specs.Count);

            MovingObjectSpec player = specs.First(s => s.Source == "playerStats");
            Assert.AreEqual(7.5f, player.SpeedPerSecond, 1e-5f, "D1: player speed with P2 max level 0.50");
            MovingObjectSpec charge = specs.First(s => s.Source == "M2.charge");
            Assert.AreEqual(18f, charge.SpeedPerSecond);
            Assert.AreEqual(0.5f, charge.Radius);
        }

        [Test]
        public void OrbitSpeed_IsOmegaRPlusPlayerMax()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            MovingObjectSpec e2 = TunnelingInvariantValidator.EnumerateMovingObjects(data).First(s => s.Source == "E2.levels[0]");
            Assert.AreEqual(CollisionLayer.PlayerOrb, e2.Layer);
            Assert.AreEqual(Mathf.PI * 3.0f + 7.5f, e2.SpeedPerSecond, 1e-4f, "D2");
            Assert.AreEqual(0.6f, e2.Radius);

            var (noP2, _) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Passives, null));
            Assert.AreEqual(5f, TunnelingInvariantValidator.PlayerMaxSpeed(noP2), "no P2 -> base speed");
        }

        [Test]
        public void Violation_WarnsWithPairs_AndDoesNotBlock()
        {
            var texts = DataTestFixtures.With(DataFileKind.Weapons, "\"damage\": 10, \"cooldownSeconds\": 1.0, \"projectileSpeed\": 12", "\"damage\": 10, \"cooldownSeconds\": 1.0, \"projectileSpeed\": 60");
            var (data, report) = DataTestFixtures.Load(texts);
            Assert.IsTrue(report.IsPlayable, "tunneling warnings never block");
            Assert.IsTrue(data.TryGetWeapon("W1", out _), "the definition is kept");

            List<ValidationIssue> issues = report.Issues.Where(i => i.FieldPath == "W1.levels[0]").ToList();
            Assert.IsTrue(issues.All(i => i.Severity == IssueSeverity.Warning));
            Assert.AreEqual(1, issues.Count(i => i.Message.Contains("v/60")), "one stage-1 warning");
            Assert.IsTrue(issues.Any(i => i.Message.Contains("M1") && i.Message.Contains(CollisionChannel.PlayerProjectileVsMonster.ToString())), DataTestFixtures.Dump(report));
            Assert.IsFalse(issues.Any(i => i.Message.Contains("(monsters.json)") && i.Message.StartsWith("터널링 위험: M4(")), "M4 pair: (60 + 1.2)/60 <= 0.25 + 1.5");
            Assert.AreEqual(report.Issues.Count, issues.Count, "only W1.levels[0] is reported\n" + DataTestFixtures.Dump(report));
        }

        [Test]
        public void StageTwo_OnlyForStageOneFailures_AndOnlyChannelPartners()
        {
            // Gem attract speed 30 -> 0.5/step > 0.2; partners are PlayerMagnet (not enumerated) and PlayerBody.
            var texts = DataTestFixtures.With(DataFileKind.PlayerStats, "\"gemAttractSpeed\": 10", "\"gemAttractSpeed\": 30");
            var (_, report) = DataTestFixtures.Load(texts);
            List<ValidationIssue> issues = report.Issues.Where(i => i.FieldPath == "playerStats.pickups.gem").ToList();
            Assert.AreEqual(1, issues.Count(i => i.Message.Contains("v/60")));
            // (30 + 7.5)/60 = 0.625 <= 0.2 + 0.5 -> the pair with the player passes, so no pair warning.
            Assert.AreEqual(1, issues.Count, DataTestFixtures.Dump(report));
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void MovingObjectSpec_Invariant()
        {
            var ok = new MovingObjectSpec("a", CollisionLayer.Monster, 30f, 0.5f);
            var bad = new MovingObjectSpec("b", CollisionLayer.Monster, 30.1f, 0.5f);
            Assert.IsTrue(ok.SatisfiesInvariant, "boundary v/60 == r passes");
            Assert.IsFalse(bad.SatisfiesInvariant);
            Assert.AreEqual(0.5f, ok.SpeedPerStep);
        }
    }
}
