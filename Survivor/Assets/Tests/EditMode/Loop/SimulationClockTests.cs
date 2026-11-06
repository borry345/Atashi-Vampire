using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Tests.EditMode.Loop
{
    /// Gate rules of SimulationClock (5.3, 5c.3 #11).
    public sealed class SimulationClockTests
    {
        private static void RunStep(SimulationClock clock, long stopAtStep = long.MaxValue)
        {
            Assert.IsTrue(clock.TryBeginStep(stopAtStep));
            clock.EndStep(stopAtStep);
        }

        [Test]
        public void NewClock_IsClosedAtStepZero()
        {
            var clock = new SimulationClock();
            Assert.AreEqual(0, clock.Step);
            Assert.IsFalse(clock.IsRunning);
            Assert.IsFalse(clock.StepActive);
            Assert.AreEqual(GateCloseReason.None, clock.CloseReason);
            Assert.AreEqual(0, clock.CloseSequence);
            Assert.IsFalse(clock.TryBeginStep(long.MaxValue), "closed gate runs no step");
        }

        [Test]
        public void OpenStep_RunsAndIncrementsStep()
        {
            var clock = new SimulationClock();
            clock.Open();
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            Assert.IsTrue(clock.StepActive);
            Assert.AreEqual(SimulationPhase.Begin, clock.CurrentPhase);
            Assert.AreEqual(0, clock.Step, "Step counts completed steps");
            clock.EndStep(long.MaxValue);
            Assert.IsFalse(clock.StepActive);
            Assert.AreEqual(1, clock.Step);
            Assert.IsTrue(clock.IsRunning);
            RunStep(clock);
            Assert.AreEqual(2, clock.Step);
            Assert.AreEqual(2, clock.StepsThisFrame);
            Assert.AreEqual(2d / 60d, clock.GameSeconds, 1e-12);
            clock.ResetStepsThisFrame();
            Assert.AreEqual(0, clock.StepsThisFrame);
        }

        [Test]
        public void CloseAfterCurrentStep_ClosesImmediately_StepStillCompletes()
        {
            var clock = new SimulationClock();
            clock.Open();
            RunStep(clock);
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            clock.SetPhase(SimulationPhase.Lifecycle);
            clock.CloseAfterCurrentStep(GateCloseReason.LevelUp);
            Assert.IsFalse(clock.IsRunning, "IsRunning drops on the call");
            Assert.IsTrue(clock.StepActive, "the step keeps running to End");
            clock.EndStep(long.MaxValue);
            Assert.AreEqual(2, clock.Step, "closed in step s=1 -> Step == s+1");
            Assert.AreEqual(GateCloseReason.LevelUp, clock.CloseReason);
            Assert.AreEqual(1, clock.CloseSequence);
            Assert.IsFalse(clock.TryBeginStep(long.MaxValue), "no catch-up step after the close");
        }

        [Test]
        public void FirstCloseInAStepWins_OverLaterCloseAndStopAtStep()
        {
            var clock = new SimulationClock();
            clock.Open();
            Assert.IsTrue(clock.TryBeginStep(1));
            clock.CloseAfterCurrentStep(GateCloseReason.Death);
            clock.CloseAfterCurrentStep(GateCloseReason.LevelUp);
            clock.EndStep(1);
            Assert.AreEqual(1, clock.Step);
            Assert.AreEqual(GateCloseReason.Death, clock.CloseReason);
            Assert.AreEqual(1, clock.CloseSequence, "one close per step");
        }

        [Test]
        public void CloseAfterCurrentStep_OutsideStep_Throws()
        {
            var clock = new SimulationClock();
            clock.Open();
            Assert.Throws<InvalidOperationException>(() => clock.CloseAfterCurrentStep(GateCloseReason.LevelUp));
        }

        [Test]
        public void CloseOpenResetSingleStep_InsideStep_Throw()
        {
            var clock = new SimulationClock();
            clock.Open();
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            Assert.Throws<InvalidOperationException>(() => clock.Close(GateCloseReason.Paused));
            Assert.Throws<InvalidOperationException>(() => clock.Open());
            Assert.Throws<InvalidOperationException>(() => clock.Reset());
            Assert.Throws<InvalidOperationException>(() => clock.RequestSingleStep());
            Assert.Throws<InvalidOperationException>(() => clock.TryBeginStep(long.MaxValue), "no nested step");
        }

        [Test]
        public void Close_OutsideStep_RecordsReasonAndSequence()
        {
            var clock = new SimulationClock();
            clock.Open();
            clock.Close(GateCloseReason.Paused);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.Paused, clock.CloseReason);
            Assert.AreEqual(1, clock.CloseSequence);
            clock.Open();
            Assert.IsTrue(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.Paused, clock.CloseReason, "Open keeps the latest close reason");
        }

        [Test]
        public void StopAtStep_ClosesAtEndOfTheStepReachingIt()
        {
            var clock = new SimulationClock();
            clock.Open();
            RunStep(clock, 3);
            RunStep(clock, 3);
            Assert.IsTrue(clock.IsRunning);
            RunStep(clock, 3);
            Assert.AreEqual(3, clock.Step);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.StopAtStep, clock.CloseReason);
            Assert.IsFalse(clock.TryBeginStep(3));
        }

        [Test]
        public void RequestSingleStep_RunsExactlyOneStep()
        {
            var clock = new SimulationClock();
            clock.RequestSingleStep();
            Assert.IsTrue(clock.IsRunning);
            RunStep(clock);
            Assert.AreEqual(1, clock.Step);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.SingleStepDone, clock.CloseReason);
            Assert.IsFalse(clock.TryBeginStep(long.MaxValue));

            clock.Open();
            RunStep(clock);
            Assert.IsTrue(clock.IsRunning, "single-step mode does not stick");
        }

        [Test]
        public void RequestJump_AppliedBeforeBeginOfNextStep_ForwardOnly()
        {
            var clock = new SimulationClock();
            clock.Open();
            RunStep(clock);
            clock.RequestJump(100);
            Assert.AreEqual(1, clock.Step, "not applied until the next step begins");
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            Assert.AreEqual(100, clock.Step);
            clock.EndStep(long.MaxValue);
            Assert.AreEqual(101, clock.Step);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.RequestJump(50));
            Assert.DoesNotThrow(() => clock.RequestJump(101));
        }

        [Test]
        public void RequestJump_OntoStopAtStep_ClosesWithoutRunning()
        {
            var clock = new SimulationClock();
            clock.Open();
            clock.RequestJump(10);
            Assert.IsFalse(clock.TryBeginStep(10));
            Assert.AreEqual(10, clock.Step);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.StopAtStep, clock.CloseReason);
        }

        [Test]
        public void AbortStep_KeepsStepAndClosesWithError()
        {
            var clock = new SimulationClock();
            clock.Open();
            RunStep(clock);
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            clock.SetPhase(SimulationPhase.Resolve);
            clock.AbortStep();
            Assert.AreEqual(1, clock.Step, "aborted step is not counted");
            Assert.IsFalse(clock.StepActive);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.Error, clock.CloseReason);
            Assert.IsFalse(clock.TryBeginStep(long.MaxValue));
        }

        [Test]
        public void Reset_ReturnsToStepZeroClosed()
        {
            var clock = new SimulationClock();
            clock.Open();
            RunStep(clock);
            RunStep(clock);
            clock.RequestJump(50);
            clock.Reset();
            Assert.AreEqual(0, clock.Step);
            Assert.IsFalse(clock.IsRunning);
            Assert.AreEqual(GateCloseReason.RunReset, clock.CloseReason);
            clock.Open();
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            Assert.AreEqual(0, clock.Step, "pending jump dropped by Reset");
        }

        [Test]
        public void EndStep_OutsideStep_Throws()
        {
            var clock = new SimulationClock();
            Assert.Throws<InvalidOperationException>(() => clock.EndStep(long.MaxValue));
        }

        [Test]
        public void CurrentContext_HoldsWhatTheDriverSet()
        {
            var clock = new SimulationClock();
            var rules = new SimulationRules();
            clock.Open();
            Assert.IsTrue(clock.TryBeginStep(long.MaxValue));
            clock.SetContext(new StepContext(0, new UnityEngine.Vector2(1f, 2f), rules, null, null));
            clock.SetPhase(SimulationPhase.MonsterTick);
            IStepClockReader reader = clock;
            Assert.AreEqual(new UnityEngine.Vector2(1f, 2f), reader.CurrentContext.PlayerPosition);
            Assert.AreSame(rules, reader.CurrentContext.Rules);
            Assert.AreEqual(SimulationPhase.MonsterTick, reader.CurrentPhase);
        }
    }
}
