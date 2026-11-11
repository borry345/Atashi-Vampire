using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Progression;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Monster health multiplier consumption (R31 HpMultiplierAt at the spawn step; TAC-F3-15, F3-16; 5.12.1).
    public sealed class HpMultiplierTests
    {
        private const float Tolerance = 1e-5f;
        private const long Segment2Start = 7200;
        private MonEmRig _rig;
        private FlowEmRig _flow;
        private FlowEmRig _flowB;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            _flow?.Dispose();
            _flow = null;
            _flowB?.Dispose();
            _flowB = null;
        }

        [Test]
        public void ShippedTable_HasTheMultipliersTheseTestsAssume()
        {
            _rig = new MonEmRig();
            SpawnTableDefinition table = _rig.Data.Current.SpawnTable;
            Assert.AreEqual(1.0f, table.HpMultiplierAt(0));
            Assert.AreEqual(1.5f, table.HpMultiplierAt(Segment2Start));
            Assert.AreEqual(3.0f, table.HpMultiplierAt(36000), "after the last segment: the last segment's value");
        }

        [Test]
        public void Segment2_TableSpawnsAndDevSpawns_GetTimes1Point5_WithoutRounding()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.JumpTo(Segment2Start);
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(5f, 0f),
                definitionId: "M1", count: 2, ignoreCap: true));
            _rig.Step(600);

            List<Monster> monsters = _rig.Live<Monster>();
            Assert.Greater(monsters.Count, 2, "table spawns and the two dev spawns");
            var species = new HashSet<string>();
            foreach (Monster m in monsters)
            {
                Assert.GreaterOrEqual(m.SpawnStep, Segment2Start);
                Assert.AreEqual(m.Definition.MaxHealth * 1.5f, m.MaxHp, Tolerance, m.Definition.Id + ": x1.5 in segment 2");
                Assert.AreEqual(m.MaxHp, m.Hp, "spawns at full health");
                species.Add(m.Definition.Id);
            }

            Assert.IsTrue(species.Contains("M2"), "segment 2 also spawns M2 (maxHealth 20 -> 30)");
            Monster dev = monsters[0];
            Assert.AreEqual(Segment2Start, dev.SpawnStep, "the dev spawn ran in step 7200 (Source = None)");
            Assert.AreEqual(15f, dev.MaxHp, "M1 10 x 1.5 = 15");
        }

        [Test]
        public void NonIntegerProduct_IsNotRounded()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 5,"));
            _rig = new MonEmRig(data, report);
            _rig.JumpTo(Segment2Start);
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            Assert.AreEqual(7.5f, m1.MaxHp, Tolerance, "5 x 1.5 = 7.5");
            Assert.AreEqual(7.5f, m1.Hp, Tolerance);
        }

        [Test]
        public void Boss_ArrivalAndSpawnBossNow_AndItsSummons_StayAtTimes1()
        {
            _rig = new MonEmRig();
            _rig.JumpTo(Segment2Start);
            Monster devBoss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            Assert.IsTrue(devBoss.IsBoss);
            Assert.AreEqual(devBoss.Definition.MaxHealth, devBoss.MaxHp, "SpawnBossNow path: x1.0");

            int before = _rig.SpawnedLog.Count;
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(-5f, 0f),
                definitionId: "M1", count: 1, ignoreCap: true));
            _rig.Step();
            int summons = 0;
            int devSpawns = 0;
            for (int i = before; i < _rig.SpawnedLog.Count; i++)
            {
                var m = (Monster)_rig.SpawnedLog[i];
                if (m.Position == new Vector2(-5f, 0f))
                {
                    devSpawns++;
                    Assert.AreEqual(15f, m.MaxHp, "same step, dev spawn: x1.5");
                }
                else
                {
                    summons++;
                    Assert.AreEqual(m.Definition.MaxHealth, m.MaxHp, "boss summon: x1.0");
                }
            }

            Assert.AreEqual(1, devSpawns);
            Assert.AreEqual(devBoss.Definition.BossPatterns[0].Count, summons);

            _rig.Dispose();
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.JumpTo(36000);
            _rig.Step();
            List<Monster> arrived = _rig.Live<Monster>();
            Assert.AreEqual(1, arrived.Count);
            Assert.IsTrue(arrived[0].IsBoss);
            Assert.AreEqual(arrived[0].Definition.MaxHealth, arrived[0].MaxHp, "spawner boss arrival at 36000 (table x3.0): x1.0");
        }

        [Test]
        public void LiveMonster_KeepsItsMaxHp_WhenTheSegmentChanges()
        {
            _rig = new MonEmRig();
            _rig.JumpTo(Segment2Start - 2);
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(15f, 0f));
            Assert.AreEqual(Segment2Start - 2, m1.SpawnStep);
            Assert.AreEqual(10f, m1.MaxHp);

            _rig.Step(10);
            Assert.Greater(_rig.Clock.Step, Segment2Start);
            Assert.AreEqual(10f, m1.MaxHp, "fixed at spawn (SPEC F3 52)");
            Assert.AreEqual(10f, m1.Hp);
        }

        [Test]
        public void Initialize_NonBossWithBadMultiplier_Throws_BossIgnoresIt()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            MonsterDefinition definition = m1.Definition;
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => m1.Initialize(definition, false, bad, 0.5f), "multiplier " + bad);
            }

            Assert.AreEqual(10f, m1.MaxHp, "a refused Initialize changes nothing");
            Assert.AreSame(definition, m1.Definition);

            Monster boss = _rig.SpawnMonster("M4", new Vector2(-10f, 0f));
            boss.Initialize(boss.Definition, true, float.NaN, 0.5f);
            Assert.AreEqual(boss.Definition.MaxHealth, boss.MaxHp, "the boss does not read the multiplier");
        }

        [Test]
        public void SameSeedTwice_AcrossSegment2_SameHashEveryStep_WithMultipliedHealth()
        {
            _flow = new FlowEmRig(start: false);
            _flowB = new FlowEmRig(start: false);
            FlowEmRig[] runs = { _flow, _flowB };
            var hashes = new List<ulong>[2];
            bool sawMultiplied = false;
            for (int r = 0; r < 2; r++)
            {
                FlowEmRig rig = runs[r];
                rig.StartRun();
                rig.Clock.RequestJump(Segment2Start - 120);
                hashes[r] = rig.Play(360);
                foreach (Monster m in rig.Live<Monster>())
                {
                    if (m.SpawnStep >= Segment2Start && Mathf.Abs(m.MaxHp - m.Definition.MaxHealth * 1.5f) <= Tolerance)
                    {
                        sawMultiplied = true;
                    }
                }
            }

            Assert.IsTrue(sawMultiplied, "the run crossed into segment 2 and spawned x1.5 monsters");
            Assert.AreEqual(360, hashes[0].Count);
            CollectionAssert.AreEqual(hashes[0], hashes[1], "TAC-F3-16: the multiplier draws nothing, so the hash repeats");
        }
    }
}
