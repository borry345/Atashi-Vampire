using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Progression;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Normal spawns on/off during a run (PendingCommand.SetSpawnsEnabled, MonsterSpawner.SpawnsEnabled; TAC-F3-20; 5.12.1).
    public sealed class SpawnToggleTests
    {
        private MonEmRig _rig;
        private MonEmRig _rigB;
        private FlowEmRig _flow;
        private FlowEmRig _flowB;
        private FlowEmRig _flowC;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            _rigB?.Dispose();
            _rigB = null;
            _flow?.Dispose();
            _flow = null;
            _flowB?.Dispose();
            _flowB = null;
            _flowC?.Dispose();
            _flowC = null;
        }

        [Test]
        public void Command_IsAnOwnerKind_CountCarriesTheValue()
        {
            Assert.AreEqual(10, (int)PendingCommandKind.SetSpawnsEnabled);
            Assert.IsTrue(PendingCommandQueue.IsOwnerHandled(PendingCommandKind.SetSpawnsEnabled));
            PendingCommand on = PendingCommand.SetSpawnsEnabled(true);
            PendingCommand off = PendingCommand.SetSpawnsEnabled(false);
            Assert.AreEqual(PendingCommandKind.SetSpawnsEnabled, on.Kind);
            Assert.AreEqual(1, on.Count);
            Assert.AreEqual(0, off.Count);

            _flow = new FlowEmRig(start: false);
            Assert.Throws<InvalidOperationException>(() => _flow.Enqueue(off), "no handler registered yet");
            _flow.Pending.RegisterHandler(PendingCommandKind.SetSpawnsEnabled, _flow.Spawner.HandleSetSpawnsEnabled);
            Assert.Throws<InvalidOperationException>(
                () => _flow.Pending.RegisterHandler(PendingCommandKind.SetSpawnsEnabled, _flow.Spawner.HandleSetSpawnsEnabled), "one handler per kind");
        }

        [Test]
        public void PendingOff_AppliesInTheNextBegin_NoTableSpawnsFromThatStep()
        {
            _flow = NewToggleRig();
            _flow.StartRun();
            Assert.IsTrue(_flow.Spawner.SpawnsEnabled);
            var spawnSteps = new List<long>();
            _flow.Lifecycle.Spawned += e =>
            {
                if (e.Category == EntityCategory.Monster)
                {
                    spawnSteps.Add(e.SpawnStep);
                }
            };
            _flow.Play(120);
            Assert.AreEqual(4, spawnSteps.Count, "2/s for 2 s while on");

            _flow.Enqueue(PendingCommand.SetSpawnsEnabled(false));
            Assert.IsTrue(_flow.Spawner.SpawnsEnabled, "not applied before the next step");
            long offStep = _flow.Clock.Step;
            _flow.Play(1);
            Assert.IsFalse(_flow.Spawner.SpawnsEnabled, "applied in Begin of the next step");
            _flow.Play(600);
            foreach (long step in spawnSteps)
            {
                Assert.Less(step, offStep, "no table spawn from the step the toggle applied");
            }

            _flow.Enqueue(PendingCommand.SetSpawnsEnabled(true));
            long onStep = _flow.Clock.Step;
            _flow.Play(120);
            Assert.IsTrue(_flow.Spawner.SpawnsEnabled);
            int resumed = 0;
            foreach (long step in spawnSteps)
            {
                if (step >= onStep)
                {
                    resumed++;
                }
            }

            int expected = 0;
            for (long step = onStep; step < onStep + 120; step++)
            {
                expected += SpawnScheduler.SpawnsDueAtStep(2f, step);
            }

            Assert.AreEqual(expected, resumed, "resumes with the schedule of those steps (no catch-up)");
        }

        [Test]
        public void Off_SkipsThePolicyEntirely_AndDrawsNoSpawnStream()
        {
            _rig = new MonEmRig();
            var policy = new CountingPolicy(new TableSpawnPolicy(_rig.Data));
            _rig.Spawner.Bind(policy);
            _rig.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rig.Context);

            _rig.JumpTo(28800);
            _rig.Step(600);
            Assert.AreEqual(0, policy.Calls, "CollectSpawns is not called while off (table and dev hold alike)");
            Assert.AreEqual(0, _rig.Live<Monster>().Count, "10/s segment: nothing spawned");
            Assert.AreEqual(new RngStreams(MonEmRig.Seed).Get(RngStream.Spawn).NextUInt(), _rig.Rng.Get(RngStream.Spawn).NextUInt(),
                "the Spawn stream was not consumed");
        }

        [Test]
        public void OnAgain_SpawnsWhatThatStepSchedules_SameAsNeverTurnedOff()
        {
            _rig = new MonEmRig();
            _rigB = new MonEmRig();
            var countsA = new Dictionary<long, int>();
            var countsB = new Dictionary<long, int>();
            Count(_rig, countsA);
            Count(_rigB, countsB);
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rigB.Spawner.Bind(new TableSpawnPolicy(_rigB.Data));
            _rigB.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rigB.Context);

            _rig.Step(400);
            _rigB.Step(400);
            Assert.AreEqual(0, _rigB.Live<Monster>().Count);
            _rigB.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(true), _rigB.Context);
            long onStep = _rigB.Clock.Step;
            _rig.Step(300);
            _rigB.Step(300);

            int resumed = 0;
            for (long step = onStep; step < onStep + 300; step++)
            {
                countsA.TryGetValue(step, out int a);
                countsB.TryGetValue(step, out int b);
                Assert.AreEqual(SpawnScheduler.SpawnsDueAtStep(2f, step), b, "step " + step + ": the schedule of that step");
                Assert.AreEqual(a, b, "step " + step + ": same count as the run that never turned spawns off");
                resumed += b;
            }

            Assert.AreEqual(10, resumed, "2/s x 5 s, no burst for the 400 skipped steps");
        }

        [Test]
        public void Off_BossArrivalSummonsAndDevSpawnsStillHappen()
        {
            _rig = new MonEmRig();
            _rig.Spawner.Bind(new TableSpawnPolicy(_rig.Data));
            _rig.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rig.Context);
            _rig.JumpTo(GameRules.BossSpawnStep - 10);
            _rig.Step(10);
            Assert.AreEqual(0, _rig.Live<Monster>().Count, "segment 4 spawns nothing while off");

            _rig.Step();
            List<Monster> monsters = _rig.Live<Monster>();
            Assert.AreEqual(1, monsters.Count);
            Assert.IsTrue(monsters[0].IsBoss, "the boss arrives at 36000 although spawns are off");
            Monster boss = monsters[0];

            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(-5f, 0f),
                definitionId: "M1", count: 3, ignoreCap: false));
            _rig.Step();
            Assert.AreEqual(1 + boss.Definition.BossPatterns[0].Count + 3, _rig.Live<Monster>().Count,
                "the boss summon and the dev SpawnMonstersNow both spawn while off");
            Assert.IsFalse(_rig.Spawner.SpawnsEnabled);

            _rigB = new MonEmRig();
            _rigB.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rigB.Context);
            _rigB.SpawnMonster("M4", new Vector2(5f, 0f));
            Assert.IsTrue(_rigB.Spawner.BossSpawnedThisRun, "SpawnBossNow path spawns while off");
        }

        [Test]
        public void Bind_TurnsSpawnsOn_AndTheHandlerLogsOnlyChanges()
        {
            _rig = new MonEmRig();
            int infos = 0;
            Application.LogCallback count = (message, trace, type) =>
            {
                if (type == LogType.Log && message.Contains("[INFO] [Loop] Normal spawns "))
                {
                    infos++;
                }
            };
            Application.logMessageReceived += count;
            try
            {
                _rig.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(true), _rig.Context);
                Assert.AreEqual(0, infos, "already on: no log");
                _rig.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rig.Context);
                _rig.Spawner.HandleSetSpawnsEnabled(PendingCommand.SetSpawnsEnabled(false), _rig.Context);
                Assert.AreEqual(1, infos, "one line per change");
                Assert.IsFalse(_rig.Spawner.SpawnsEnabled);
            }
            finally
            {
                Application.logMessageReceived -= count;
            }

            _rig.Spawner.Bind(new MonEmNoSpawnPolicy());
            Assert.IsTrue(_rig.Spawner.SpawnsEnabled, "every run starts with spawns on");
        }

        [Test]
        public void SameToggleSameStep_TwoRuns_SameHash_AndTheToggleChangesTheRun()
        {
            _flow = NewToggleRig();
            _flowB = NewToggleRig();
            _flowC = NewToggleRig();
            List<ulong> a = PlayToggled(_flow, true);
            List<ulong> b = PlayToggled(_flowB, true);
            List<ulong> untoggled = PlayToggled(_flowC, false);

            Assert.AreEqual(a.Count, b.Count);
            CollectionAssert.AreEqual(a, b, "TAC-F3-20: same toggle in the same step, same hash");
            Assert.AreNotEqual(untoggled[untoggled.Count - 1], a[a.Count - 1], "the toggle had an effect on the run");
        }

        private static FlowEmRig NewToggleRig()
        {
            var rig = new FlowEmRig(start: false);
            rig.Pending.RegisterHandler(PendingCommandKind.SetSpawnsEnabled, rig.Spawner.HandleSetSpawnsEnabled);
            return rig;
        }

        private static List<ulong> PlayToggled(FlowEmRig rig, bool toggle)
        {
            rig.StartRun();
            var hashes = new List<ulong>(rig.Play(200));
            if (toggle)
            {
                rig.Enqueue(PendingCommand.SetSpawnsEnabled(false));
            }

            hashes.AddRange(rig.Play(300));
            if (toggle)
            {
                rig.Enqueue(PendingCommand.SetSpawnsEnabled(true));
            }

            hashes.AddRange(rig.Play(200));
            return hashes;
        }

        private static void Count(MonEmRig rig, Dictionary<long, int> counts)
        {
            rig.Lifecycle.Spawned += e =>
            {
                if (e.Category == EntityCategory.Monster)
                {
                    counts[e.SpawnStep] = counts.TryGetValue(e.SpawnStep, out int c) ? c + 1 : 1;
                }
            };
        }

        private sealed class CountingPolicy : ISpawnPolicy
        {
            private readonly ISpawnPolicy _inner;

            internal CountingPolicy(ISpawnPolicy inner)
            {
                _inner = inner;
            }

            internal int Calls { get; private set; }

            public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
            {
                Calls++;
                _inner.CollectSpawns(context, liveMonsterCount, spawnRng, monsterIdsOut);
            }

            public bool BossSpawnDue(long step)
            {
                return _inner.BossSpawnDue(step);
            }
        }
    }
}
