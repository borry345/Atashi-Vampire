using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Tick;
using Survivor.Tests.EditMode.Monsters;
using UnityEngine;

namespace Survivor.Tests.EditMode.Progression
{
    // Logs every call that matters for the reset order, then forwards to the real strategy.
    internal sealed class FlowLoggedCollisions : ICollisionStrategy
    {
        private readonly ICollisionStrategy _inner;
        private readonly List<string> _log;
        private readonly string _name;

        internal FlowLoggedCollisions(ICollisionStrategy inner, List<string> log, string name)
        {
            _inner = inner;
            _log = log;
            _name = name;
        }

        public CollisionStrategyKind Kind => _inner.Kind;

        public void Register(CollisionBody body)
        {
            _log.Add(_name + ".collision.Register " + body.Id);
            _inner.Register(body);
        }

        public void Unregister(CollisionBody body)
        {
            _log.Add(_name + ".collision.Unregister " + body.Id);
            _inner.Unregister(body);
        }

        public void NotifyRadiusChanged(CollisionBody body)
        {
            _inner.NotifyRadiusChanged(body);
        }

        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            return _inner.CollectEvents(step);
        }

        public void Clear()
        {
            _log.Add(_name + ".collision.Clear");
            _inner.Clear();
        }
    }

    internal sealed class FlowLoggedTicks : ITickStrategy
    {
        private readonly ITickStrategy _inner;
        private readonly List<string> _log;
        private readonly string _name;

        internal FlowLoggedTicks(ITickStrategy inner, List<string> log, string name)
        {
            _inner = inner;
            _log = log;
            _name = name;
        }

        public TickStrategyKind Kind => _inner.Kind;

        public void Register(IUpdatable updatable, GameObject host)
        {
            _inner.Register(updatable, host);
        }

        public void Unregister(IUpdatable updatable)
        {
            _log.Add(_name + ".tick.Unregister " + updatable.Id);
            _inner.Unregister(updatable);
        }

        public void RunGroup(TickGroup group, in StepContext context)
        {
            _inner.RunGroup(group, context);
        }

        public int RegisteredCount(TickGroup group)
        {
            return _inner.RegisteredCount(group);
        }

        public void Clear()
        {
            _log.Add(_name + ".tick.Clear");
            _inner.Clear();
        }
    }

    internal sealed class FlowLoggedPool : IPoolStrategy
    {
        private readonly IPoolStrategy _inner;
        private readonly List<string> _log;

        internal FlowLoggedPool(IPoolStrategy inner, List<string> log)
        {
            _inner = inner;
            _log = log;
        }

        public PoolStrategyKind Kind => _inner.Kind;

        public int ActiveCount => _inner.ActiveCount;

        public GameObject Acquire(GameObject template, Vector2 position)
        {
            return _inner.Acquire(template, position);
        }

        public void Release(GameObject instance)
        {
            _log.Add("pool.Release");
            _inner.Release(instance);
        }

        public void Prewarm(GameObject template, int count)
        {
            _inner.Prewarm(template, count);
        }

        public void ReleaseAll()
        {
            _log.Add("pool.ReleaseAll");
            _inner.ReleaseAll();
        }

        public void Clear()
        {
            _log.Add("pool.Clear");
            _inner.Clear();
        }
    }

    /// RunSession.Reset (TAC-F8-1, TAC-F8-2, 5.14 rev 17 order 0..9) on the full rig, plus the second-run determinism check
    /// (5c.4 wave 4: the reset is complete when run 2 reproduces run 1 step for step).
    public sealed class RunSessionTests
    {
        private const int BusySteps = 900;

        private readonly List<FlowEmRig> _rigs = new List<FlowEmRig>();

        [TearDown]
        public void TearDown()
        {
            foreach (FlowEmRig rig in _rigs)
            {
                rig.Dispose();
            }

            _rigs.Clear();
        }

        private FlowEmRig NewRig(bool start = true)
        {
            var rig = new FlowEmRig(start: start);
            _rigs.Add(rig);
            return rig;
        }

        // Pushes the run into every per-run record the reset must clear.
        private static void MakeBusy(FlowEmRig rig)
        {
            rig.Rules.PlayerTakesDamage = false;
            List<ulong> hashes = rig.Play(BusySteps);
            Assert.AreEqual(BusySteps, hashes.Count, "the busy run did not end early");
            // The boss on top of the player: it survives the weapons, so the player-side overlap record is non-empty.
            rig.Enqueue(PendingCommand.SpawnBossNow(rig.Data.Current.SpawnTable.BossMonsterId, rig.Player.Position));
            rig.Play(2);
            rig.Enqueue(PendingCommand.AddXp(3));
            if (!rig.Clock.IsRunning)
            {
                rig.ResolveLevelUps();
            }

            rig.Step();
            rig.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None, position: new Vector2(40f, 0f)));
            rig.Enqueue(PendingCommand.AddXp(1));
            rig.LevelUp.Open(new List<UpgradeOption> { default, default, default }, 0);
            rig.Clock.Close(GateCloseReason.Paused);
        }

        [Test]
        public void BusyRun_ReachesEveryRecordTheResetClears()
        {
            FlowEmRig rig = NewRig();
            MakeBusy(rig);
            Assert.Greater(rig.Lifecycle.ActiveCount, 0);
            Assert.Greater(rig.Kills.KillCount, 0, "weapons killed monsters");
            Assert.Greater(rig.Experience.Level, 1, "gems were collected and level-ups resolved");
            Assert.Greater(rig.Clock.Step, 0);
            Assert.IsTrue(rig.Spawner.BossSpawnedThisRun);
            Assert.Greater(rig.Receiver.OverlapCount, 0, "monsters overlap the player");
            Assert.Greater(rig.Pickups.AttractedGemCount + rig.PickupCounts.ActiveGemCount, 0);
            Assert.Greater(rig.Pending.PendingCount, 0);
            Assert.Greater(rig.Commands.PendingCount, 0);
            Assert.Greater(rig.LevelUp.RoundId, 0);
            Assert.IsTrue(rig.LevelUp.IsOpen);
        }

        [Test]
        public void Reset_ClearsEveryRunRecord_AndRestartsAtStepZero()
        {
            FlowEmRig rig = NewRig();
            MakeBusy(rig);
            rig.Seed = 99UL;
            RunStrategies next = rig.StrategyFactory();
            rig.Session.Reset(next);

            Assert.AreEqual(0, rig.Lifecycle.ActiveCount);
            Assert.AreEqual(0, rig.Pool.ActiveCount, "every pooled object went back");
            Assert.AreEqual(new EntityHandle(1, 1), rig.Player.Body.Id, "player body 1:1");
            Assert.AreEqual(new EntityHandle(2, 1), rig.Player.MagnetBody.Id, "player magnet 2:1");
            Assert.IsTrue(rig.Player.IsRegistered);
            Assert.AreEqual(Vector2.zero, rig.Player.Position);
            Assert.AreEqual(0, rig.Clock.Step);
            Assert.IsFalse(rig.Clock.IsRunning, "the App opens the clock");
            Assert.AreEqual(GateCloseReason.RunReset, rig.Clock.CloseReason);
            Assert.AreEqual(1, rig.Weapons.Count);
            Assert.AreEqual(rig.Data.Current.PlayerStats.StartingWeaponId, rig.Weapons.Slots[0].Id);
            Assert.AreEqual(1, rig.Weapons.Slots[0].Level);
            Assert.AreEqual(0, rig.Weapons.Slots[0].NextFireStep, "ResetForRun after clock.Reset (first shot at step 0)");
            Assert.AreEqual(0, rig.Weapons.ConsumedByEvolution.Count);
            Assert.AreEqual(0, rig.Passives.Count);
            Assert.AreEqual(rig.Health.MaxHp, rig.Health.Hp);
            Assert.AreEqual(rig.Data.Current.PlayerStats.MaxHealth, rig.Health.MaxHp);
            Assert.AreEqual(0, rig.Receiver.OverlapCount);
            Assert.AreEqual(0, rig.Pickups.AttractedGemCount);
            Assert.AreEqual(0, rig.PickupCounts.ActiveGemCount);
            Assert.AreEqual(0, rig.PickupCounts.ActiveChestCount);
            Assert.AreEqual(0, rig.Kills.KillCount);
            Assert.IsFalse(rig.Kills.BossKilledThisStep);
            Assert.IsFalse(rig.Spawner.BossSpawnedThisRun);
            Assert.AreEqual(0, rig.Spawner.LiveNonBossCount);
            Assert.AreEqual(0, rig.LevelUp.RoundId);
            Assert.IsFalse(rig.LevelUp.IsOpen);
            Assert.AreEqual(1, rig.Experience.Level);
            Assert.AreEqual(0, rig.Experience.Xp);
            Assert.AreEqual(0, rig.Pending.PendingCount, "pending queue empty");
            Assert.AreEqual(0, rig.Commands.PendingCount, "command buffer empty");
            Assert.AreEqual(RunOutcomeKind.None, rig.Gate.PendingOutcome.Kind);
            Assert.AreEqual(99UL, rig.Rng.MasterSeed);
            Assert.AreEqual(new RngStreams(99UL).Get(RngStream.Spawn).NextUInt(), rig.Rng.Get(RngStream.Spawn).NextUInt(), "streams restarted");
        }

        [Test]
        public void Reset_HandsEachStrategyToItsOwner_AndBindsPolicyAndInput()
        {
            FlowEmRig rig = NewRig();
            rig.Play(30);
            var policy = new MonEmNoSpawnPolicy();
            rig.PolicyFactory = () => policy;
            var next = new RunStrategies(rig.Pool, new CentralTickStrategy(), new SelfCollisionStrategy(rig.Index, rig.Snapshot),
                new BruteForceTargetProvider());
            rig.Session.Reset(next);

            Assert.AreSame(next.Pool, rig.Lifecycle.Pool);
            Assert.AreSame(next.Tick, rig.Lifecycle.Ticks);
            Assert.AreSame(next.Collision, rig.Lifecycle.Collisions);
            Assert.AreSame(next.Collision, rig.Router.Strategy);
            Assert.AreSame(next.Target, rig.Targets.Current);
            Assert.AreSame(policy, rig.Spawner.Policy);
            Assert.AreSame(rig.Input, rig.Movement.Input);
            Assert.AreEqual(0, rig.Router.PendingEventCount);
        }

        [Test]
        public void Reset_RunsInTheDocumentedOrder()
        {
            FlowEmRig rig = NewRig(start: false);
            var log = new List<string>();
            int run = 0;
            var pool = new FlowLoggedPool(rig.Pool, log);
            rig.StrategyFactory = () =>
            {
                run++;
                string name = "run" + run;
                return new RunStrategies(pool,
                    new FlowLoggedTicks(new CentralTickStrategy(), log, name),
                    new FlowLoggedCollisions(new SelfCollisionStrategy(rig.Index, rig.Snapshot), log, name),
                    new BruteForceTargetProvider());
            };
            rig.StartRun();
            rig.Rules.PlayerTakesDamage = false;
            rig.Play(240);
            Assert.Greater(rig.Lifecycle.ActiveCount, 3, "entities to despawn");
            rig.Clock.Close(GateCloseReason.Paused);
            log.Clear();

            rig.Seed = 7UL;
            RunStrategies next = rig.StrategyFactory();
            rig.Session.Reset(next);

            List<string> run1 = log.Where(l => !l.StartsWith("run2")).ToList();
            List<string> run2 = log.Where(l => l.StartsWith("run2")).ToList();
            int firstRun2 = log.FindIndex(l => l.StartsWith("run2"));
            Assert.IsTrue(log.Skip(firstRun2).All(l => !l.StartsWith("run1.")), "the outgoing strategies are done before the next ones are used (1-2 before 3, 5)");
            Assert.AreEqual("run1.collision.Unregister 2:1", log[0], "0. the player (magnet, then body) before any entity");
            Assert.AreEqual("run1.collision.Unregister 1:1", log[1]);
            int releaseAll = log.IndexOf("pool.ReleaseAll");
            int collisionClear = log.IndexOf("run1.collision.Clear");
            int tickClear = log.IndexOf("run1.tick.Clear");
            Assert.Greater(releaseAll, log.LastIndexOf("pool.Release"), "2. ReleaseAll after every despawn release (1)");
            Assert.Greater(collisionClear, log.FindLastIndex(l => l.StartsWith("run1.collision.Unregister")));
            Assert.Greater(tickClear, collisionClear);
            Assert.Less(tickClear, firstRun2);
            Assert.AreEqual(-1, log.IndexOf("pool.Clear"), "the same pool object is never cleared");
            CollectionAssert.AreEqual(new[] { "run2.collision.Register 1:1", "run2.collision.Register 2:1" }, run2,
                "5. the player is the first registration of the new run, with ids 1:1 and 2:1");
            Assert.Greater(run1.Count, 6);
        }

        [Test]
        public void Reset_DuringAStep_Throws_AndBadStrategies_ChangeNothing()
        {
            FlowEmRig rig = NewRig();
            rig.Play(60);
            Exception thrown = null;
            rig.ResolveHook.Pending.Add(context =>
            {
                try
                {
                    rig.Session.Reset(rig.StrategyFactory());
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            long step = rig.Clock.Step;
            rig.Step();
            Assert.IsInstanceOf<InvalidOperationException>(thrown);
            Assert.AreEqual(step + 1, rig.Clock.Step, "the step completed normally");

            rig.Clock.Close(GateCloseReason.Paused);
            int active = rig.Lifecycle.ActiveCount;
            ulong hash = rig.Hash;
            RunStrategies good = rig.StrategyFactory();
            Assert.Throws<ArgumentException>(() => rig.Session.Reset(new RunStrategies(null, good.Tick, good.Collision, good.Target)));
            Assert.Throws<ArgumentException>(() => rig.Session.Reset(new RunStrategies(good.Pool, null, good.Collision, good.Target)));
            Assert.Throws<ArgumentException>(() => rig.Session.Reset(new RunStrategies(good.Pool, good.Tick, null, good.Target)));
            Assert.Throws<ArgumentException>(() => rig.Session.Reset(new RunStrategies(good.Pool, good.Tick, good.Collision, null)));
            Assert.AreEqual(active, rig.Lifecycle.ActiveCount);
            Assert.AreEqual(hash, rig.Hash, "a rejected reset changes nothing");
            Assert.IsTrue(rig.Player.IsRegistered);
        }

        [Test]
        public void SecondRun_AfterReset_ReproducesTheFirstRun_StepForStep()
        {
            FlowEmRig rig = NewRig();
            rig.Rules.PlayerTakesDamage = false;
            List<ulong> first = rig.Play(BusySteps);
            Assert.AreEqual(BusySteps, first.Count);
            Assert.Greater(rig.RoundLog.Count, 0, "level-ups happened in the run");
            Assert.Greater(rig.Kills.KillCount, 0);

            rig.Clock.Close(GateCloseReason.Paused);
            rig.StartRun();
            List<ulong> second = rig.Play(BusySteps);
            CollectionAssert.AreEqual(first, second, "same seed after Reset -> same hash at every step");

            FlowEmRig fresh = NewRig();
            fresh.Rules.PlayerTakesDamage = false;
            CollectionAssert.AreEqual(first, fresh.Play(BusySteps), "a fresh assembly gives the same run");
        }

        [Test]
        public void ResetIntoAnotherSeed_DiffersFromTheFirstRun()
        {
            FlowEmRig rig = NewRig();
            rig.Rules.PlayerTakesDamage = false;
            List<ulong> first = rig.Play(300);
            rig.Clock.Close(GateCloseReason.Paused);
            rig.Seed = FlowEmRig.DefaultSeed + 1;
            rig.StartRun();
            List<ulong> second = rig.Play(300);
            Assert.AreNotEqual(first[first.Count - 1], second[second.Count - 1]);
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            FlowEmRig r = NewRig();
            var args = new object[]
            {
                r.Clock, r.Lifecycle, r.Router, r.Targets, r.Ids, r.Rng, r.Commands, r.Pending, r.Player, r.Movement, r.Health, r.Weapons,
                r.Passives, r.Receiver, r.Applier, r.Pickups, r.Kills, r.Spawner, r.Experience, r.LevelUp, r.Gate,
                (Func<ulong>)(() => 1UL), (Func<ISpawnPolicy>)(() => null), (Func<IInputSource>)(() => null),
            };
            System.Reflection.ConstructorInfo ctor = typeof(RunSession).GetConstructors().Single();
            Assert.AreEqual(args.Length, ctor.GetParameters().Length);
            Assert.DoesNotThrow(() => ctor.Invoke(args));
            for (int i = 0; i < args.Length; i++)
            {
                object[] copy = (object[])args.Clone();
                copy[i] = null;
                var e = Assert.Throws<System.Reflection.TargetInvocationException>(() => ctor.Invoke(copy));
                Assert.IsInstanceOf<ArgumentNullException>(e.InnerException, "argument " + i);
            }
        }

        [Test]
        public void ProgressionCode_UsesNoStrategiesTimeOrRandom()
        {
            var banned = new Regex(@"\bTime\.|UnityEngine\.Random|System\.Random|\bRandom\.|DateTime|Keyboard|Survivor\.Strategies|IDevModeHost|Survivor\.App");
            string folder = Path.Combine(Application.dataPath, "Scripts", "Simulation", "Progression");
            var files = new List<string>(Directory.GetFiles(folder, "*.cs"))
            {
                Path.Combine(Application.dataPath, "Scripts", "Simulation", "Loop", "SimulationStateHash.cs"),
            };
            Assert.AreEqual(10, files.Count, "9 Progression files + SimulationStateHash");
            foreach (string path in files)
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = Regex.Replace(lines[i], @"//.*$", string.Empty);
                    Assert.IsFalse(banned.IsMatch(code), Path.GetFileName(path) + ":" + (i + 1) + " " + lines[i].Trim());
                }
            }
        }
    }
}
