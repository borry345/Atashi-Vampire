using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies.Tick;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Loop
{
    /// The three drivers in real FixedUpdates: phase order, frame-rate determinism (TAC-0-4), catch-up cap (TAC-0-3),
    /// in-step close (TAC-0-7), exception abort (5c.3 #7), both tick strategies (TAC-F17-1/2/3, TAC-0-5).
    public sealed class FixedStepLoopTests
    {
        private const int MaxFrames = 6000;
        private const long ScenarioSteps = 600;

        private LoopPmRig _rig;

        [SetUp]
        public void SetUp()
        {
            SimulationSettingsApplier.Apply();
            Time.captureDeltaTime = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            _rig?.Dispose();
            _rig = null;
            LoopPmWorld.DestroyTestObjects();
        }

        // Runs the open rig until its gate closes (or the frame budget runs out).
        private IEnumerator RunUntilClosed(LoopPmRig rig)
        {
            int frames = 0;
            while (rig.Clock.IsRunning && frames < MaxFrames)
            {
                frames++;
                yield return null;
            }

            Assert.IsFalse(rig.Clock.IsRunning, "gate still open after " + MaxFrames + " frames");
        }

        private IEnumerator RunScenario(TickStrategyKind tick, float frameSeconds, long steps, List<ulong> hashes)
        {
            Time.captureDeltaTime = frameSeconds;
            _rig = new LoopPmRig(tick);
            _rig.Rules.StopAtStep = steps;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            Assert.AreEqual(steps, _rig.Clock.Step);
            Assert.AreEqual(GateCloseReason.StopAtStep, _rig.Clock.CloseReason);
            ulong hash = _rig.Hash();
            TestContext.Out.WriteLine("tick=" + tick + " frame=1/" + Mathf.RoundToInt(1f / frameSeconds) + " steps=" + steps
                + " live=" + _rig.Lifecycle.ActiveCount + " liveIds=" + _rig.Ids.LiveCount + " hash=0x" + hash.ToString("X16"));
            hashes.Add(hash);
            _rig.Dispose();
            _rig = null;
            Time.captureDeltaTime = 0f;
        }

        [Test]
        public void SettingsApplier_SetsFixedStepAndCatchUpCap()
        {
            SimulationSettingsApplier.Apply();
            Assert.AreEqual(1f / 60f, Time.fixedDeltaTime, 1e-6f);
            Assert.AreEqual(0.05f, Time.maximumDeltaTime, 1e-6f);
        }

        [UnityTest]
        public IEnumerator OneStep_RunsPhasesInOrder_ContextFinalAfterPlayerMove()
        {
            _rig = new LoopPmRig(TickStrategyKind.CentralTick, withScenario: false);
            var log = new List<string>();
            var provider = new LoopPmTargetProvider(log, "slot");
            _rig.Targets.Replace(provider);
            _rig.Pipeline.AddHandler(SimulationPhase.PlayerMove, 0, _rig.Player);
            _rig.Player.Position = new Vector2(1f, 1f);
            foreach (SimulationPhase phase in Enum.GetValues(typeof(SimulationPhase)))
            {
                _rig.Pipeline.AddHandler(phase, 50, new LoopPmContextProbe(log, _rig.Clock));
            }

            _rig.Clock.RequestSingleStep();
            yield return RunUntilClosed(_rig);

            Assert.AreEqual(1, _rig.Clock.Step);
            Assert.AreEqual(GateCloseReason.SingleStepDone, _rig.Clock.CloseReason);
            CollectionAssert.AreEqual(new[]
            {
                "Begin#0 (1.00, 1.00)",
                "PlayerMove#0 (1.00, 1.00)",
                "MonsterTick#0 (1.04, 1.00) ctx",
                "BuildIndex#0 (1.04, 1.00) ctx",
                "slot.OnSnapshotBuilt",
                "WeaponTick#0 (1.04, 1.00) ctx",
                "ProjectileTick#0 (1.04, 1.00) ctx",
                "Collision#0 (1.04, 1.00) ctx",
                "Resolve#0 (1.04, 1.00) ctx",
                "Lifecycle#0 (1.04, 1.00) ctx",
            }, log, "End has no handlers; EndStep is called directly");
            Assert.AreSame(_rig.Snapshot, provider.LastSnapshot);
        }

        [UnityTest]
        public IEnumerator FrameRates_30_60_144_GiveTheSameHash_CentralTick()
        {
            var hashes = new List<ulong>();
            yield return RunScenario(TickStrategyKind.CentralTick, 1f / 30f, ScenarioSteps, hashes);
            yield return RunScenario(TickStrategyKind.CentralTick, 1f / 60f, ScenarioSteps, hashes);
            yield return RunScenario(TickStrategyKind.CentralTick, 1f / 144f, ScenarioSteps, hashes);
            Assert.AreEqual(hashes[0], hashes[1], "30 vs 60 FPS");
            Assert.AreEqual(hashes[0], hashes[2], "30 vs 144 FPS");
        }

        [UnityTest]
        public IEnumerator TickStrategies_GiveTheSameHash_AtEveryFrameRate()
        {
            var hashes = new List<ulong>();
            yield return RunScenario(TickStrategyKind.CentralTick, 1f / 60f, ScenarioSteps, hashes);
            yield return RunScenario(TickStrategyKind.PerObjectFixedUpdate, 1f / 30f, ScenarioSteps, hashes);
            yield return RunScenario(TickStrategyKind.PerObjectFixedUpdate, 1f / 60f, ScenarioSteps, hashes);
            yield return RunScenario(TickStrategyKind.PerObjectFixedUpdate, 1f / 144f, ScenarioSteps, hashes);
            for (int i = 1; i < hashes.Count; i++)
            {
                Assert.AreEqual(hashes[0], hashes[i], "run " + i + " differs from central 1/60");
            }
        }

        [UnityTest]
        public IEnumerator LongFrame_RunsAtMostThreeSteps()
        {
            _rig = new LoopPmRig(TickStrategyKind.CentralTick);
            _rig.Clock.Open();
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            int maxSteps = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // A real stalled frame: captureDeltaTime would bypass the maximumDeltaTime clamp (U2).
                Thread.Sleep(100);
                yield return null;
                int steps = _rig.FrameCounter.LastFrameSteps;
                TestContext.Out.WriteLine("steps after a 100 ms frame: " + steps);
                Assert.LessOrEqual(steps, StepMath.MaxCatchUpSteps);
                maxSteps = Math.Max(maxSteps, steps);
            }

            Assert.Greater(maxSteps, 0, "the stalled frames did run catch-up steps");
            Assert.IsTrue(_rig.Clock.IsRunning);
        }

        [UnityTest]
        public IEnumerator CloseInsideStep_StopsAtStepPlusOne_NoCatchUp([Values(30, 60, 144)] int fps)
        {
            Time.captureDeltaTime = 1f / fps;
            _rig = new LoopPmRig(TickStrategyKind.CentralTick);
            _rig.Pipeline.AddHandler(SimulationPhase.Lifecycle, 90, new LoopPmCloseAt(_rig.Clock, 36, GateCloseReason.LevelUp));
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            Assert.AreEqual(37, _rig.Clock.Step, "closed in step 36 -> Step == 37");
            Assert.AreEqual(GateCloseReason.LevelUp, _rig.Clock.CloseReason);
            ulong closedHash = _rig.Hash();
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.AreEqual(37, _rig.Clock.Step, "no step while closed");
            Assert.AreEqual(closedHash, _rig.Hash(), "no state change while closed");
        }

        [UnityTest]
        public IEnumerator PhaseException_AbortsStep_StepUnchanged_GateError(
            [Values(SimulationPhase.Begin, SimulationPhase.WeaponTick, SimulationPhase.Resolve, SimulationPhase.Lifecycle)] SimulationPhase phase)
        {
            Time.captureDeltaTime = 1f / 60f;
            _rig = new LoopPmRig(TickStrategyKind.CentralTick);
            var thrower = new LoopPmThrowOnce(20);
            var after = new LoopPmPhaseRecorder();
            _rig.Pipeline.AddHandler(phase, 5, thrower);
            _rig.Pipeline.AddHandler(SimulationPhase.Lifecycle, 50, after);
            LogAssert.Expect(LogType.Error, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[ERROR\] \[Loop\] phase=" + phase
                + @" step=20 InvalidOperationException: scripted failure$"));
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);

            Assert.IsTrue(thrower.Thrown);
            Assert.AreEqual(20, _rig.Clock.Step, "the aborted step is not counted");
            Assert.IsFalse(_rig.Clock.StepActive);
            Assert.AreEqual(GateCloseReason.Error, _rig.Clock.CloseReason);
            CollectionAssert.DoesNotContain(after.Log, "Lifecycle#20", "phases after the failure did not run");
            Assert.AreEqual("Lifecycle#19", after.Log[after.Log.Count - 1]);

            _rig.Rules.StopAtStep = 25;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            Assert.AreEqual(25, _rig.Clock.Step, "the loop resumes after the flow reopens the gate");
        }

        [UnityTest]
        public IEnumerator FirstTickIsTheStepAfterSpawn_OneTickPerStep([Values] bool selfTick)
        {
            Time.captureDeltaTime = 1f / 30f;
            _rig = new LoopPmRig(selfTick ? TickStrategyKind.PerObjectFixedUpdate : TickStrategyKind.CentralTick);
            _rig.Rules.StopAtStep = 40;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            Assert.Greater(_rig.Lifecycle.ActiveCount, 0);
            foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
            {
                var mover = (LoopPmMover)entity;
                Assert.AreEqual(_rig.Clock.Step - 1 - entity.SpawnStep, mover.TickCount, entity.Category + " " + entity.Id);
            }

            Assert.AreEqual(0, _rig.Ticks.RegisteredCount(TickGroup.Monsters) + _rig.Ticks.RegisteredCount(TickGroup.Projectiles)
                - _rig.Lifecycle.ActiveCount, "every live mover is registered once");
        }

        [UnityTest]
        public IEnumerator SelfTickers_DoNothingWhileGateClosed()
        {
            Time.captureDeltaTime = 1f / 60f;
            _rig = new LoopPmRig(TickStrategyKind.PerObjectFixedUpdate);
            _rig.Rules.StopAtStep = 20;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);

            var counts = new List<int>();
            foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
            {
                Behaviour ticker = entity.Category == EntityCategory.Monster
                    ? (Behaviour)entity.GetComponent<MonsterSelfTicker>()
                    : entity.GetComponent<ProjectileSelfTicker>();
                Assert.IsTrue(ticker.enabled, "registered tickers stay enabled while paused");
                counts.Add(((LoopPmMover)entity).TickCount);
            }

            Assert.Greater(counts.Count, 0);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            int index = 0;
            foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
            {
                Assert.AreEqual(counts[index++], ((LoopPmMover)entity).TickCount, "no Tick while StepActive is false");
            }

            _rig.Lifecycle.DespawnAll(DespawnReason.RunReset);
            Assert.AreEqual(0, _rig.Ticks.RegisteredCount(TickGroup.Monsters));
        }

        [UnityTest]
        public IEnumerator PresentationSync_CopiesPosition_UnlessEngineDrivesTransform()
        {
            Time.captureDeltaTime = 1f / 60f;
            _rig = new LoopPmRig(TickStrategyKind.CentralTick);
            _rig.Rules.StopAtStep = 30;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            yield return null;
            SimEntity driven = null;
            foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
            {
                Assert.AreEqual(entity.Position, (Vector2)entity.transform.position);
                if (entity.Category == EntityCategory.Monster && driven == null)
                {
                    driven = entity;
                }
            }

            driven.Body.DrivesTransform = true;
            Vector3 frozen = driven.transform.position;
            _rig.Rules.StopAtStep = 40;
            _rig.Clock.Open();
            yield return RunUntilClosed(_rig);
            yield return null;
            Assert.AreNotEqual(driven.Position, (Vector2)frozen, "the monster kept moving");
            Assert.AreEqual(frozen, driven.transform.position, "transform left to the engine");
        }
    }
}
