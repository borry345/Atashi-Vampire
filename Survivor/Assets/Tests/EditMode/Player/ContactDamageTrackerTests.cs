using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Tests.EditMode.Player
{
    // 5c.5 "damage tick" (TAC-F6-6, TAC-0-10, TAC-F2-3).
    public sealed class ContactDamageTrackerTests
    {
        private static readonly EntityHandle A = new EntityHandle(5, 1);
        private static readonly EntityHandle B = new EntityHandle(9, 1);

        private static List<EntityHandle> Collect(ContactDamageTracker tracker, long step)
        {
            var hits = new List<EntityHandle>();
            tracker.CollectHits(step, hits);
            return hits;
        }

        [Test]
        public void TACF6_6_EnterHitsImmediatelyThenOncePerInterval()
        {
            var tracker = new ContactDamageTracker(30);
            tracker.OnEnter(A, 10);
            var hitSteps = new List<long>();
            for (long step = 10; step < 100; step++)
            {
                if (Collect(tracker, step).Count > 0)
                {
                    hitSteps.Add(step);
                }
            }

            CollectionAssert.AreEqual(new long[] { 10, 40, 70 }, hitSteps);
        }

        [Test]
        public void TACF6_6_ExitKeepsRecordSoReentryBeforeAllowedStepDoesNotHit()
        {
            var tracker = new ContactDamageTracker(30);
            tracker.OnEnter(A, 0);
            Assert.AreEqual(1, Collect(tracker, 0).Count);
            tracker.OnExit(A);
            Assert.AreEqual(0, Collect(tracker, 5).Count);
            tracker.OnEnter(A, 10);
            Assert.AreEqual(0, Collect(tracker, 10).Count, "re-entry inside the cooldown must not hit");
            Assert.AreEqual(0, Collect(tracker, 29).Count);
            CollectionAssert.AreEqual(new[] { A }, Collect(tracker, 30));
        }

        [Test]
        public void TAC0_10_SameIndexWithAnotherGenerationIsANewTarget()
        {
            var tracker = new ContactDamageTracker(30);
            var old = new EntityHandle(7, 1);
            var reused = new EntityHandle(7, 2);
            tracker.OnEnter(old, 0);
            Collect(tracker, 0);
            tracker.OnEnter(reused, 1);
            CollectionAssert.AreEqual(new[] { reused }, Collect(tracker, 1), "the cooldown of 7:1 must not apply to 7:2");
        }

        [Test]
        public void R10_HitsComeInAscendingIdOrder()
        {
            var tracker = new ContactDamageTracker(10);
            tracker.OnEnter(B, 0);
            tracker.OnEnter(new EntityHandle(5, 3), 0);
            tracker.OnEnter(A, 0);
            CollectionAssert.AreEqual(new[] { A, new EntityHandle(5, 3), B }, Collect(tracker, 0));
        }

        [Test]
        public void TACF2_3_SetIntervalAppliesFromTheNextComputationOnly()
        {
            var tracker = new ContactDamageTracker(30);
            tracker.OnEnter(A, 0);
            Collect(tracker, 0);
            tracker.SetInterval(10);
            Assert.IsTrue(tracker.TryGetNextAllowed(A, out long next));
            Assert.AreEqual(30, next, "existing nextAllowed is not recomputed");
            Assert.AreEqual(0, Collect(tracker, 10).Count);
            Assert.AreEqual(1, Collect(tracker, 30).Count);
            Assert.IsTrue(tracker.TryGetNextAllowed(A, out next));
            Assert.AreEqual(40, next);
        }

        [Test]
        public void TACF6_6_TwoOverlapsOfOneWeaponShareOneRecord()
        {
            var tracker = new ContactDamageTracker(30);
            tracker.OnEnter(A, 0);
            tracker.OnEnter(A, 0);
            Assert.AreEqual(1, Collect(tracker, 0).Count);
            tracker.OnExit(A);
            Assert.AreEqual(1, Collect(tracker, 30).Count, "still overlapped through the second zone");
            tracker.OnExit(A);
            Assert.AreEqual(0, Collect(tracker, 60).Count);
        }

        [Test]
        public void TAC0_10_ForgetDropsTheRecord()
        {
            var tracker = new ContactDamageTracker(30);
            tracker.OnEnter(A, 0);
            Collect(tracker, 0);
            tracker.Forget(A);
            Assert.AreEqual(0, tracker.TrackedCount);
            Assert.AreEqual(0, Collect(tracker, 30).Count, "a forgotten (despawned) target has no overlap left");
        }

        [Test]
        public void IntervalBelowOneThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContactDamageTracker(0));
            Assert.Throws<ArgumentNullException>(() => new ContactDamageTracker(1).CollectHits(0, null));
        }
    }
}
