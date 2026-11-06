using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Despawn distance and the kill pipeline (TAC-F3-5, F4-1, F4-2, F4-8, F8-8 flag; R18, R24, R26; 5c.5 "RNG distribution").
    public sealed class DespawnAndKillTests
    {
        private MonEmRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [Test]
        public void Despawn_BeyondD_OutOfRange_NoDropNoKill_BossAndPickupsStay()
        {
            _rig = new MonEmRig();
            _rig.SpawnMonster("M1", new Vector2(45.2f, 0f));
            EntityHandle farId = _rig.LastSpawnedId;
            Monster near = _rig.SpawnMonster("M1", new Vector2(0f, 44.9f));
            Monster boss = _rig.SpawnMonster("M4", new Vector2(-100f, 0f));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None, position: new Vector2(1000f, 0f), count: 1));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None, position: new Vector2(0f, -1000f)));

            Assert.IsTrue(_rig.DespawnedLog.Contains((farId, DespawnReason.OutOfRange)),
                "TAC-F3-5: Lifecycle 20 despawned it already in its spawn step (spawned at Lifecycle 0)");
            _rig.Step(3);
            Assert.IsTrue(near.IsAlive && _rig.Lifecycle.TryGet(near.Id, out _), "inside D stays");
            Assert.IsTrue(_rig.Lifecycle.TryGet(boss.Id, out _), "the boss is never despawned");
            Assert.AreEqual(1, _rig.PickupCounts.ActiveGemCount, "TAC-F4-8: gems have no despawn rule");
            Assert.AreEqual(1, _rig.PickupCounts.ActiveChestCount, "chests have no despawn rule");
            Assert.AreEqual(0, _rig.Kills.KillCount, "R24: OutOfRange is not a kill");
        }

        [Test]
        public void DespawnDistance_DevOverride_AndHotReloadedTableValue()
        {
            _rig = new MonEmRig();
            Assert.IsTrue(_rig.Despawner.TryGetDespawnDistance(_rig.Rules, out float d));
            Assert.AreEqual(45f, d, "spawnTable.json despawnDistance");

            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.SpawnTable, "\"despawnDistance\": 45", "\"despawnDistance\": 30"));
            Assert.IsTrue(_rig.Data.TrySwap(data, report));
            Assert.IsTrue(_rig.Despawner.TryGetDespawnDistance(_rig.Rules, out d));
            Assert.AreEqual(30f, d, "read from Current on every use, so a hot reload applies at once");
            _rig.SpawnMonster("M1", new Vector2(31f, 0f));
            Assert.IsTrue(_rig.DespawnedLog.Contains((_rig.LastSpawnedId, DespawnReason.OutOfRange)));
            _rig.SpawnMonster("M1", new Vector2(29f, 0f));
            Assert.IsTrue(_rig.Lifecycle.TryGet(_rig.LastSpawnedId, out _), "inside the new D");

            _rig.Rules.DespawnDistance = 10f;
            _rig.Rules.DespawnDistanceOverridden = true;
            Assert.IsTrue(_rig.Despawner.TryGetDespawnDistance(_rig.Rules, out d));
            Assert.AreEqual(10f, d, "R26 / dev override wins");
            _rig.SpawnMonster("M1", new Vector2(0f, 10.5f));
            Assert.IsTrue(_rig.DespawnedLog.Contains((_rig.LastSpawnedId, DespawnReason.OutOfRange)));
        }

        [Test]
        public void Kill_NormalMonster_GemAtKillPosition_DespawnKilled_CountedOnce()
        {
            _rig = new MonEmRig();
            Monster m = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            EntityHandle id = m.Id;
            Vector2 killPosition = default;
            _rig.Resolve10.Action = context =>
            {
                killPosition = m.Position;
                _rig.LookUpDamageable(id).ApplyDamage(10f, context.Step);
                _rig.LookUpDamageable(id).ApplyDamage(10f, context.Step);
                _rig.Resolve10.Action = null;
            };
            _rig.Step();

            Assert.AreEqual(1, _rig.Kills.KillCount, "R24: one kill even with extra damage in the same step");
            Assert.IsTrue(_rig.DespawnedLog.Contains((id, DespawnReason.Killed)));
            List<ExperienceGem> gems = _rig.Live<ExperienceGem>();
            Assert.AreEqual(1, gems.Count, "TAC-F4-1: one gem per normal kill");
            Assert.AreEqual(killPosition, gems[0].Position);
            Assert.AreEqual(1, gems[0].XpValue, "gemXpValue from playerStats.json pickups");
            Assert.AreEqual(0.2f, gems[0].Body.Radius);
            Assert.AreEqual(CollisionLayer.Gem, gems[0].Body.Layer);

            _rig.Step(2);
            Assert.AreEqual(1, _rig.Kills.KillCount, "no second confirmation");
        }

        [Test]
        public void Kill_SeveralInOneStep_ProcessedInAscendingId()
        {
            _rig = new MonEmRig();
            Monster a = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            Monster b = _rig.SpawnMonster("M1", new Vector2(-5f, 0f));
            Monster c = _rig.SpawnMonster("M1", new Vector2(0f, 5f));
            EntityHandle[] ids = { a.Id, b.Id, c.Id };
            int before = _rig.DespawnedLog.Count;
            _rig.DamageNextStep(100f, c, a, b);
            _rig.Step();

            var killed = new List<EntityHandle>();
            for (int i = before; i < _rig.DespawnedLog.Count; i++)
            {
                killed.Add(_rig.DespawnedLog[i].id);
            }

            CollectionAssert.AreEqual(ids, killed, "R10: kills applied in ascending EntityHandle order (damage order was c, a, b)");
            List<ExperienceGem> gems = _rig.Live<ExperienceGem>();
            Assert.AreEqual(3, gems.Count);
            Assert.AreEqual(3, _rig.Kills.KillCount);
        }

        [Test]
        public void Kill_Boss_RaisesFlagForThisStepOnly_NoDropNoChestDraw()
        {
            _rig = new MonEmRig();
            Monster boss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            EntityHandle id = boss.Id;
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Drop);
            bool flagAtGate = false;
            _rig.Lifecycle90.Action = context => flagAtGate = _rig.Kills.BossKilledThisStep;
            _rig.DamageNextStep(5000f, boss);
            _rig.Step();

            Assert.IsTrue(flagAtGate, "TAC-F4-1: the step outcome gate (Lifecycle 90) sees the boss kill in the same step");
            Assert.AreEqual(1, _rig.Kills.KillCount, "R24: the boss counts as a kill");
            Assert.IsTrue(_rig.DespawnedLog.Contains((id, DespawnReason.Killed)));
            Assert.AreEqual(0, _rig.PickupCounts.ActiveGemCount, "no drop for the boss");
            Assert.AreEqual(0, _rig.PickupCounts.ActiveChestCount);
            Assert.AreEqual(reference.NextUInt(), _rig.Rng.Get(RngStream.Drop).NextUInt(), "no chest draw for the boss");

            _rig.Step();
            Assert.IsFalse(flagAtGate, "cleared at the next Resolve");
            _rig.Kills.Reset();
            Assert.AreEqual(0, _rig.Kills.KillCount);
        }

        [Test]
        public void Kill_ChestDrawAlwaysConsumesTheDropStream_EvenAtChanceZero()
        {
            _rig = new MonEmRig();
            _rig.Rules.ChestDropChance = 0f;
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Drop);
            var monsters = new List<Monster>();
            for (int i = 0; i < 7; i++)
            {
                monsters.Add(_rig.SpawnMonster("M1", new Vector2(5f, i)));
            }

            _rig.DamageNextStep(100f, monsters.ToArray());
            _rig.Step();
            for (int i = 0; i < 7; i++)
            {
                reference.NextFloat01();
            }

            Assert.AreEqual(0, _rig.PickupCounts.ActiveChestCount, "chance 0: never a chest");
            Assert.AreEqual(7, _rig.PickupCounts.ActiveGemCount);
            Assert.AreEqual(reference.NextUInt(), _rig.Rng.Get(RngStream.Drop).NextUInt(), "TAC-F4-2: exactly one Drop draw per normal kill");
        }

        [Test]
        public void Kill_ChestDropRate_10000SeededKills_IsTwoPercentWithin04pp()
        {
            _rig = new MonEmRig(countDropsOnly: true);
            const int perRound = 500;
            const int rounds = 20;
            for (int r = 0; r < rounds; r++)
            {
                _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(3f, 0f),
                    definitionId: "M1", count: perRound, ignoreCap: true));
                _rig.Step();
                _rig.DamageNextStep(100f, _rig.Live<Monster>().ToArray());
                _rig.Step();
                Assert.AreEqual(0, _rig.Registry.Count);
            }

            Assert.AreEqual(perRound * rounds, _rig.Kills.KillCount);
            Assert.AreEqual(perRound * rounds, _rig.DropCounter.Gems);
            double rate = _rig.DropCounter.Chests / (double)(perRound * rounds);
            Debug.Log("[MonEm] chest drops: " + _rig.DropCounter.Chests + " / " + (perRound * rounds) + " = " + rate.ToString("P2"));
            Assert.AreEqual(0.02, rate, 0.004, "TAC-F4-2: 2% +-0.4 percentage points");
        }

        [Test]
        public void RunReset_DespawnAll_ZeroesCounters()
        {
            _rig = new MonEmRig();
            _rig.Rules.MonsterCap = 1;
            Monster m = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            _rig.Step();
            Assert.AreEqual(1, _rig.Spawner.LiveNonBossCount, "the boss's first summon is refused by cap 1; the boss is not counted");
            _rig.DamageNextStep(100f, m);
            _rig.Step();
            Assert.AreEqual(1, _rig.PickupCounts.ActiveGemCount);
            Assert.AreEqual(0, _rig.Spawner.LiveNonBossCount);

            _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            Assert.AreEqual(1, _rig.Spawner.LiveNonBossCount);
            _rig.Lifecycle.DespawnAll(DespawnReason.RunReset);
            Assert.AreEqual(0, _rig.Spawner.LiveNonBossCount);
            Assert.AreEqual(0, _rig.PickupCounts.ActiveGemCount);
            Assert.AreEqual(0, _rig.Registry.Count);
            Assert.AreEqual(1, _rig.Kills.KillCount, "RunReset is not a kill");
        }
    }
}
