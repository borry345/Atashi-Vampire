using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Spawn schedule, table policy, cap, placement, boss arrival and the SpawnMonsters handler
    /// (TAC-F3-2, F3-3, F3-4, F3-6, F3-9, F3-10, F8-10; R7, R9, R20; 5c.5 "spawn schedule").
    public sealed class SpawnTests
    {
        private MonEmRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [TestCase(2f)]
        [TestCase(4f)]
        [TestCase(7f)]
        [TestCase(10f)]
        public void Scheduler_IntegerRate_GivesExactlyRateTimes60PerMinute_AndRatePerAnySecond(float rate)
        {
            int minute = 0;
            for (long k = 0; k < 3600; k++)
            {
                minute += SpawnScheduler.SpawnsDueAtStep(rate, k);
            }

            Assert.AreEqual((int)rate * 60, minute, "TAC-F3-2: exactly rate x 60 in 60 seconds");
            foreach (long offset in new long[] { 0, 1, 17, 59, 1234, 7199 })
            {
                int second = 0;
                for (long k = offset; k < offset + 60; k++)
                {
                    second += SpawnScheduler.SpawnsDueAtStep(rate, k);
                }

                Assert.AreEqual((int)rate, second, "any 60 consecutive steps (offset " + offset + ")");
            }
        }

        [Test]
        public void Scheduler_NonIntegerRate_StaysWithinOneOfExact_AndRejectsBadInput()
        {
            int total = 0;
            for (long k = 0; k < 3600; k++)
            {
                total += SpawnScheduler.SpawnsDueAtStep(2.5f, k);
            }

            Assert.AreEqual(150, total);
            Assert.AreEqual(0, SpawnScheduler.SpawnsDueAtStep(0f, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnScheduler.SpawnsDueAtStep(2f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnScheduler.SpawnsDueAtStep(-1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnScheduler.SpawnsDueAtStep(float.NaN, 0));
        }

        [Test]
        public void TablePolicy_EachSegment_SpawnsRateTimes60InItsFirstMinute_OnlyItsSpecies()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load();
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            var policy = new TableSpawnPolicy(registry);
            DeterministicRng rng = new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn);
            var ids = new List<string>();
            var expected = new[] { (0L, 2, "M1"), (7200L, 4, "M1 M2"), (18000L, 7, "M1 M2 M3"), (28800L, 10, "M1 M2 M3") };
            foreach ((long start, int rate, string species) in expected)
            {
                ids.Clear();
                var counts = new Dictionary<string, int>();
                for (long step = start; step < start + 3600; step++)
                {
                    int before = ids.Count;
                    policy.CollectSpawns(new StepContext(step, default, new SimulationRules(), null, null), 9999, rng, ids);
                    Assert.LessOrEqual(ids.Count - before, 1, "at most rate/60 rounded up per step for rates <= 60");
                }

                Assert.AreEqual(rate * 60, ids.Count, "segment starting at " + start + ": rate x 60 in its first minute");
                foreach (string id in ids)
                {
                    StringAssert.Contains(id, species, "only the segment's species");
                    counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
                }

                Assert.AreEqual(species.Split(' ').Length, counts.Count, "every species of the segment appears in a minute");
                Debug.Log("[MonEm] segment " + start + " minute: " + ids.Count + " spawns, species " + string.Join(",", counts));
            }
        }

        [Test]
        public void TablePolicy_OutsideSegments_SpawnsNothing_AndBossDueFrom36000()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load();
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            var policy = new TableSpawnPolicy(registry);
            DeterministicRng rng = new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn);
            var ids = new List<string>();
            for (long step = 36000; step < 36600; step++)
            {
                policy.CollectSpawns(new StepContext(step, default, new SimulationRules(), null, null), 0, rng, ids);
            }

            Assert.AreEqual(0, ids.Count, "after 600 s the table has no segment");
            Assert.IsFalse(policy.BossSpawnDue(35999));
            Assert.IsTrue(policy.BossSpawnDue(36000));
            Assert.IsTrue(policy.BossSpawnDue(40000), "a time jump past 36000 still brings the boss");
            Assert.AreEqual(new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn).NextUInt(), rng.NextUInt(), "no draws outside segments");
        }

        [Test]
        public void TablePolicy_NoData_SpawnsNothing()
        {
            var policy = new TableSpawnPolicy(new GameDataRegistry());
            var ids = new List<string>();
            policy.CollectSpawns(new StepContext(10, default, new SimulationRules(), null, null), 0, new RngStreams(1).Get(RngStream.Spawn), ids);
            Assert.AreEqual(0, ids.Count);
        }

        [Test]
        public void Spawner_FirstMinute_Spawns120OnTheSpawnCircle()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            int spawned = 0;
            _rig.Lifecycle.Spawned += e =>
            {
                spawned++;
                Assert.AreEqual(GameRules.SpawnRadius, (e.Position - _rig.Player).magnitude, 1e-3f, "TAC-F3-4: on the r = 20 circle");
            };

            _rig.Step(3600);
            Assert.AreEqual(120, spawned, "0-2 min segment: 2/s x 60");
            Assert.AreEqual(120, _rig.Spawner.LiveNonBossCount);
        }

        [Test]
        public void Spawner_Placement_IsOneSpawnStreamDrawPerMonster()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new FixedPolicy("M1", "M1", "M1"));
            _rig.Player = new Vector2(3f, -2f);
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn);

            _rig.Step();
            List<Monster> monsters = _rig.Live<Monster>();
            Assert.AreEqual(3, monsters.Count);
            for (int i = 0; i < 3; i++)
            {
                Vector2 expected = SpawnGeometry.PointOnCircle(_rig.Player, GameRules.SpawnRadius, reference.NextFloat01());
                Assert.AreEqual(expected, monsters[i].Position, "R7: theta = u01 x 2pi, one draw, request order = id order");
            }
        }

        [Test]
        public void Spawner_Cap_DropsTheRestWithoutDrawing_NeverExceeds_AndResumesWithoutBurst()
        {
            _rig = new MonEmRig();
            _rig.Rules.MonsterCap = 5;
            _rig.Spawner.Bind(new FixedPolicy("M1", "M1", "M1", "M1", "M1", "M1", "M1", "M1"));
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn);
            _rig.Lifecycle.Spawned += e => Assert.LessOrEqual(_rig.Spawner.LiveNonBossCount, _rig.Rules.MonsterCap, "R20: never above the cap");

            _rig.Step();
            Assert.AreEqual(5, _rig.Live<Monster>().Count, "TAC-F3-3: 8 requested, 5 allowed, 3 dropped");
            for (int i = 0; i < 5; i++)
            {
                reference.NextFloat01();
            }

            Assert.AreEqual(reference.NextUInt(), _rig.Rng.Get(RngStream.Spawn).NextUInt(), "dropped requests drew no angle");

            _rig.Spawner.Bind(new FixedPolicy("M1"));
            List<Monster> live = _rig.Live<Monster>();
            _rig.DamageNextStep(1e6f, live[0], live[1]);
            _rig.Step();
            Assert.AreEqual(4, _rig.Live<Monster>().Count, "after two kills only this step's single request spawns (no catch-up)");
            _rig.Step(3);
            Assert.AreEqual(5, _rig.Live<Monster>().Count, "back at the cap");
        }

        [Test]
        public void Spawner_LastSegmentAtCap400_NeverExceedsNonBossCap()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.JumpTo(28800);
            int max = 0;
            _rig.Lifecycle.Spawned += e => max = Math.Max(max, _rig.Spawner.LiveNonBossCount);

            _rig.Step(2700);
            Assert.AreEqual(400, _rig.Spawner.LiveNonBossCount, "10/s for 45 s fills the cap");
            Assert.AreEqual(400, max, "SPEC F3 8-10 min: the non-boss count never exceeds 400");
        }

        [Test]
        public void Boss_SpawnsOnceAt36000_OnCircle_IgnoringCap_WithInfoLog()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.Rules.MonsterCap = 0;
            _rig.JumpTo(35999);
            _rig.Step();
            Assert.AreEqual(0, _rig.Live<Monster>().Count);

            LogAssert.Expect(LogType.Log, new Regex(@"\[INFO\] \[Loop\] Boss spawned: M4 id=\d+:\d+ step=36000\."));
            _rig.Step();
            List<Monster> monsters = _rig.Live<Monster>();
            Assert.AreEqual(1, monsters.Count, "TAC-F3-6: exactly one boss, cap ignored; no normal spawn at 36000");
            Assert.IsTrue(monsters[0].IsBoss);
            Assert.AreEqual(GameRules.SpawnRadius, (monsters[0].Position - _rig.Player).magnitude, 1e-3f, "same circle as normal spawns");
            Assert.IsTrue(_rig.Spawner.BossSpawnedThisRun);
            Assert.AreEqual(0, _rig.Spawner.LiveNonBossCount, "the boss is not counted toward the cap");

            _rig.Step(5);
            Assert.AreEqual(1, _rig.Live<Monster>().Count, "one boss per run");
        }

        [Test]
        public void Boss_WithFullCap_Makes401Monsters()
        {
            _rig = new MonEmRig();
            _rig.Rules.MonsterCap = 3;
            _rig.Spawner.Bind(new FixedPolicy("M1", "M1", "M1", "M1"));
            _rig.Step();
            _rig.Spawner.Bind(new BossNowPolicy());
            _rig.Step();
            Assert.AreEqual(4, _rig.Registry.Count, "R20: cap + boss");
            Assert.AreEqual(3, _rig.Spawner.LiveNonBossCount);
        }

        [Test]
        public void Boss_TimeJumpPast36000_StillArrives()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.JumpTo(40000);
            _rig.Step();
            Assert.AreEqual(1, _rig.Live<Monster>().Count);
            Assert.IsTrue(_rig.Live<Monster>()[0].IsBoss);
        }

        [Test]
        public void Boss_DevSpawnedEarlier_DoesNotReappearAt36000()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.Rules.MonsterCap = 0;
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(5f, 0f),
                definitionId: "M4", count: 1, ignoreCap: true));
            _rig.Step();
            Assert.IsTrue(_rig.Spawner.BossSpawnedThisRun, "the command handler raises the shared flag (SpawnBossNow path)");
            Assert.AreEqual(new Vector2(5f, 0f), _rig.Live<Monster>()[0].Position, "radius 0: at the given position");

            _rig.JumpTo(36000);
            _rig.Step(3);
            Assert.AreEqual(1, _rig.Live<Monster>().Count, "TAC-F3-6 / F8-8: no second boss");
        }

        [Test]
        public void Boss_InvalidBossId_WarnsOncePerRun_NoBoss_NormalSpawnsEnd()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.SpawnTable, "\"bossMonsterId\": \"M4\"", "\"bossMonsterId\": \"MX\""));
            Assert.IsNull(data.SpawnTable.BossMonsterId);
            _rig = new MonEmRig(data, report);
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            int warnings = 0;
            Application.LogCallback count = (message, trace, type) =>
            {
                if (type == LogType.Warning && message.Contains("[WARN] [Loop] Boss not spawned") && message.Contains("cannot be cleared"))
                {
                    warnings++;
                }
            };
            Application.logMessageReceived += count;
            try
            {
                _rig.JumpTo(35990);
                _rig.Step(20);
            }
            finally
            {
                Application.logMessageReceived -= count;
            }

            Assert.AreEqual(1, warnings, "TAC-F8-10: one warning per run");
            Assert.AreEqual(0, _rig.Registry.Count - CountSpawnedBefore36000(), "no boss and no normal spawn from 36000");
            Assert.IsFalse(_rig.Spawner.BossSpawnedThisRun);
        }

        [Test]
        public void Bind_Null_Throws_AndBindResetsTheBossFlag()
        {
            _rig = new MonEmRig();
            Assert.Throws<ArgumentNullException>(() => _rig.Spawner.Bind(null));
            _rig.SpawnMonster("M4", new Vector2(5f, 0f));
            Assert.IsTrue(_rig.Spawner.BossSpawnedThisRun);
            _rig.Spawner.Bind(new MonEmNoSpawnPolicy());
            Assert.IsFalse(_rig.Spawner.BossSpawnedThisRun, "run start clears the per-run flag");
        }

        [Test]
        public void SpawnMonsters_SummonPath_ChecksCapPerMonster_DrawsSummonAnglesOnlyForAccepted()
        {
            _rig = new MonEmRig();
            _rig.Rules.MonsterCap = 2;
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Summon);
            var center = new Vector2(1f, 1f);
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: center, radius: 3f,
                definitionId: "M1", count: 5));
            _rig.Step();

            List<Monster> monsters = _rig.Live<Monster>();
            Assert.AreEqual(2, monsters.Count, "cap 2: three refused");
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(SpawnGeometry.PointOnCircle(center, 3f, reference.NextFloat01()), monsters[i].Position,
                    "dev commands also use the Summon stream");
            }

            Assert.AreEqual(reference.NextUInt(), _rig.Rng.Get(RngStream.Summon).NextUInt(), "refused monsters drew nothing");

            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: center, radius: 0f,
                definitionId: "M1", count: 3, ignoreCap: true));
            _rig.Step();
            Assert.AreEqual(5, _rig.Live<Monster>().Count, "IgnoreCap lifts the cap");
        }

        [Test]
        public void SpawnMonsters_UnknownDefinition_IsDroppedAndCounted()
        {
            _rig = new MonEmRig();
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, definitionId: "MX", count: 2));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnEnemyProjectile, EntityHandle.None, definitionId: "M1"));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnEnemyZone, EntityHandle.None, definitionId: "M4", slotIndex: 1));
            _rig.Step();
            Assert.AreEqual(0, _rig.Lifecycle.ActiveCount);
            Assert.AreEqual(3, _rig.MonsterCommands.DroppedCommandCount, "unknown id, M1 has no projectile, pattern 1 is not a zone");
        }

        [Test]
        public void Handlers_NullArguments_Throw()
        {
            _rig = new MonEmRig();
            Assert.Throws<ArgumentNullException>(() => new MonsterSpawner(null, _rig.Data, _rig.Rng));
            Assert.Throws<ArgumentNullException>(() => new MonsterCommandHandler(_rig.Lifecycle, _rig.Data, _rig.Rng, null));
            Assert.Throws<ArgumentNullException>(() => new TableSpawnPolicy(null));
            Assert.Throws<ArgumentNullException>(() => new DespawnSystem(_rig.Lifecycle, null, _rig.Data));
            Assert.Throws<ArgumentNullException>(() => new KillPipeline(_rig.Registry, _rig.Data, null));
            Assert.Throws<ArgumentNullException>(() => new DropCommandHandler(null, _rig.Data));
            Assert.Throws<InvalidOperationException>(() =>
                _rig.MonsterCommands.Handle(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None), _rig.Context));
        }

        private int CountSpawnedBefore36000()
        {
            int count = 0;
            foreach (SimEntity entity in _rig.SpawnedLog)
            {
                if (entity.Category == EntityCategory.Monster && entity.SpawnStep < 36000)
                {
                    count++;
                }
            }

            return count;
        }

        // Requests the same ids every step.
        private sealed class FixedPolicy : ISpawnPolicy
        {
            private readonly string[] _ids;

            internal FixedPolicy(params string[] ids)
            {
                _ids = ids;
            }

            public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
            {
                monsterIdsOut.AddRange(_ids);
            }

            public bool BossSpawnDue(long step)
            {
                return false;
            }
        }

        private sealed class BossNowPolicy : ISpawnPolicy
        {
            public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
            {
            }

            public bool BossSpawnDue(long step)
            {
                return true;
            }
        }
    }
}
