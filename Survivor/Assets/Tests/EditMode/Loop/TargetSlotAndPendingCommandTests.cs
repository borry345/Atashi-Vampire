using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.EditMode.Loop
{
    /// TargetProviderSlot replacement rules (5.7) and PendingCommandQueue Begin application (TAC-F16-5, TAC-0-1, L2).
    public sealed class TargetSlotAndPendingCommandTests
    {
        private SimulationClock _clock;
        private TargetProviderSlot _slot;
        private GameDataRegistry _data;
        private SimulationRules _rules;
        private PendingCommandQueue _queue;
        private LoopEmCommandSink _sink;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _clock = new SimulationClock();
            _slot = new TargetProviderSlot(_clock);
            _data = new GameDataRegistry();
            _rules = new SimulationRules();
            _queue = new PendingCommandQueue(_slot, _data, _rules);
            _sink = new LoopEmCommandSink();
            _log = new List<string>();
        }

        private void RunBegin()
        {
            if (!_clock.IsRunning)
            {
                _clock.Open();
            }

            Assert.IsTrue(_clock.TryBeginStep(long.MaxValue));
            _clock.SetPhase(SimulationPhase.Begin);
            _queue.OnPhase(SimulationPhase.Begin, new StepContext(_clock.Step, Vector2.zero, _rules, _slot, _sink));
            _clock.EndStep(long.MaxValue);
        }

        [Test]
        public void EmptySlot_FindsNothing_AndForwardsNothing()
        {
            Assert.IsNull(_slot.Current);
            Assert.IsFalse(_slot.TryFindNearest(Vector2.zero, out _));
            Assert.DoesNotThrow(() => _slot.OnSnapshotBuilt(new MonsterSnapshotBuffer()));
            Assert.Throws<InvalidOperationException>(() => { TargetProviderKind unused = _slot.Kind; });
        }

        [Test]
        public void Slot_ForwardsToCurrentProvider()
        {
            var provider = new LoopEmTargetProvider(_log, "a", TargetProviderKind.SpatialHash);
            _slot.Replace(provider);
            var snapshot = new MonsterSnapshotBuffer();
            _slot.OnSnapshotBuilt(snapshot);
            Assert.AreSame(snapshot, provider.LastSnapshot);
            Assert.IsTrue(_slot.TryFindNearest(Vector2.one, out _));
            Assert.AreEqual(1, provider.QueryCount);
            Assert.AreEqual(TargetProviderKind.SpatialHash, _slot.Kind);
        }

        [TestCase(SimulationPhase.PlayerMove)]
        [TestCase(SimulationPhase.BuildIndex)]
        [TestCase(SimulationPhase.Resolve)]
        [TestCase(SimulationPhase.Lifecycle)]
        public void SlotReplace_InsideStepOutsideBegin_Throws(SimulationPhase phase)
        {
            _clock.Open();
            Assert.IsTrue(_clock.TryBeginStep(long.MaxValue));
            _clock.SetPhase(phase);
            Assert.Throws<InvalidOperationException>(() => _slot.Replace(new LoopEmTargetProvider(_log, "x")));
            _clock.SetPhase(SimulationPhase.Begin);
            Assert.DoesNotThrow(() => _slot.Replace(new LoopEmTargetProvider(_log, "y")));
            _clock.EndStep(long.MaxValue);
            Assert.DoesNotThrow(() => _slot.Replace(null), "run reset: outside a step");
        }

        [Test]
        public void SwitchTargetProvider_ReplacesThenCallsRequirementsOnce_InBegin()
        {
            var first = new LoopEmTargetProvider(_log, "first");
            var second = new LoopEmTargetProvider(_log, "second");
            _slot.Replace(first);
            int calls = 0;
            _queue.Enqueue(new SwitchTargetProviderCommand(second, () =>
            {
                calls++;
                _log.Add("requirements current=" + (_slot.Current == second));
            }));
            Assert.AreSame(first, _slot.Current, "nothing changes until Begin");

            _queue.OnPhase(SimulationPhase.Lifecycle, default);
            Assert.AreSame(first, _slot.Current, "only Begin applies");

            RunBegin();
            Assert.AreSame(second, _slot.Current);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(new[] { "requirements current=True" }, _log);
            RunBegin();
            Assert.AreEqual(1, calls, "applied once");
        }

        [Test]
        public void QueuedCommands_ApplyInQueueOrder()
        {
            var a = new LoopEmTargetProvider(_log, "a");
            var b = new LoopEmTargetProvider(_log, "b");
            _queue.Enqueue(new SwitchTargetProviderCommand(a, () => _log.Add("a")));
            _queue.Enqueue(PendingCommand.SetDespawnDistance(30f));
            _queue.Enqueue(new SwitchTargetProviderCommand(b, () => _log.Add("b")));
            Assert.AreEqual(3, _queue.PendingCount);
            RunBegin();
            CollectionAssert.AreEqual(new[] { "a", "b" }, _log);
            Assert.AreSame(b, _slot.Current);
            Assert.AreEqual(30f, _rules.DespawnDistance);
            Assert.IsTrue(_rules.DespawnDistanceOverridden);
            Assert.AreEqual(0, _queue.PendingCount);
        }

        [Test]
        public void SpawnRequests_BecomeSimulationCommandsWithSourceNone()
        {
            _queue.Enqueue(PendingCommand.SpawnMonstersNow("M1", 5, new Vector2(1f, 2f), 8f, true));
            _queue.Enqueue(PendingCommand.SpawnChestNow(new Vector2(3f, 4f)));
            _queue.Enqueue(PendingCommand.SpawnBossNow("BOSS", new Vector2(5f, 6f)));
            RunBegin();

            Assert.AreEqual(3, _sink.Commands.Count);
            SimulationCommand monsters = _sink.Commands[0];
            Assert.AreEqual(SimulationCommandKind.SpawnMonsters, monsters.Kind);
            Assert.AreEqual(EntityHandle.None, monsters.Source);
            Assert.AreEqual("M1", monsters.DefinitionId);
            Assert.AreEqual(5, monsters.Count);
            Assert.AreEqual(new Vector2(1f, 2f), monsters.Position);
            Assert.AreEqual(8f, monsters.Radius);
            Assert.IsTrue(monsters.IgnoreCap);
            Assert.AreEqual(SimulationCommandKind.SpawnChest, _sink.Commands[1].Kind);
            Assert.AreEqual(new Vector2(3f, 4f), _sink.Commands[1].Position);
            Assert.AreEqual(SimulationCommandKind.SpawnMonsters, _sink.Commands[2].Kind);
            Assert.AreEqual("BOSS", _sink.Commands[2].DefinitionId);
            Assert.AreEqual(1, _sink.Commands[2].Count);
            Assert.IsTrue(_sink.Commands[2].IgnoreCap);
        }

        [Test]
        public void SpawnBossNow_RefusedWhenBossAlreadySpawned()
        {
            _queue.BossSpawnedThisRun = () => true;
            _queue.Enqueue(PendingCommand.SpawnBossNow("BOSS", Vector2.zero));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[INFO\] \[Loop\] SpawnBossNow refused"));
            RunBegin();
            Assert.AreEqual(0, _sink.Commands.Count);
            Assert.AreEqual(0, _queue.PendingCount, "the refused command is consumed");
        }

        [Test]
        public void SwapData_PlayableReplaces_UnplayableKeepsCurrentAndWarns()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            _data.Initialize(data, report);
            var (next, nextReport) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            _queue.Enqueue(PendingCommand.SwapData(next, nextReport));
            Assert.AreSame(data, _data.Current, "not swapped before Begin");
            RunBegin();
            Assert.AreSame(next, _data.Current);

            var (broken, brokenReport) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Weapons, "{ broken"));
            _queue.Enqueue(PendingCommand.SwapData(broken, brokenReport));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[WARN\] \[Data\] "));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[WARN\] \[Loop\] SwapData refused"));
            RunBegin();
            Assert.AreSame(next, _data.Current, "unplayable data keeps the existing set");
        }

        [Test]
        public void OwnerKinds_RequireRegisteredHandler()
        {
            Assert.Throws<InvalidOperationException>(() => _queue.Enqueue(PendingCommand.SetHealth(10f)));
            Assert.Throws<InvalidOperationException>(() => _queue.Enqueue(PendingCommand.AddXp(5)));
            Assert.Throws<InvalidOperationException>(() => _queue.Enqueue(PendingCommand.LevelUpNow()));
            Assert.Throws<InvalidOperationException>(() => _queue.Enqueue(PendingCommand.SetLoadout("W1")));
            Assert.Throws<ArgumentException>(() => _queue.RegisterHandler(PendingCommandKind.SwapData, (c, ctx) => { }));
            Assert.Throws<ArgumentNullException>(() => _queue.RegisterHandler(PendingCommandKind.AddXp, null));

            var applied = new List<string>();
            _queue.RegisterHandler(PendingCommandKind.AddXp, (c, ctx) => applied.Add("xp " + c.Count + " @" + ctx.Step));
            _queue.RegisterHandler(PendingCommandKind.SetLoadout, (c, ctx) => applied.Add("loadout " + c.Payload));
            Assert.Throws<InvalidOperationException>(() => _queue.RegisterHandler(PendingCommandKind.AddXp, (c, ctx) => { }));

            _queue.Enqueue(PendingCommand.AddXp(7));
            _queue.Enqueue(PendingCommand.SetLoadout("W1,W2"));
            RunBegin();
            CollectionAssert.AreEqual(new[] { "xp 7 @0", "loadout W1,W2" }, applied);
        }

        [Test]
        public void Factories_ValidateArguments_AndClearDropsQueue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PendingCommand.SpawnMonstersNow("M1", 0, Vector2.zero, 1f, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => PendingCommand.SetDespawnDistance(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PendingCommand.SetDespawnDistance(float.NaN));
            Assert.Throws<ArgumentNullException>(() => PendingCommand.SwapData(null, null));
            Assert.Throws<ArgumentNullException>(() => new SwitchTargetProviderCommand(null, () => { }));
            Assert.Throws<ArgumentNullException>(() => new SwitchTargetProviderCommand(new LoopEmTargetProvider(_log, "a"), null));

            _queue.Enqueue(PendingCommand.SpawnChestNow(Vector2.zero));
            _queue.Clear();
            RunBegin();
            Assert.AreEqual(0, _sink.Commands.Count);
        }
    }
}
