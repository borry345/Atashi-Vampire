using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Loop
{
    /// Collision order 0 collects, Resolve order 0 dispatches per channel in CompareTo order (5.9, R10, 5-B #13).
    public sealed class CollisionEventRouterTests
    {
        private static StepContext At(long step)
        {
            return new StepContext(step, Vector2.zero, new SimulationRules(), null, null);
        }

        private static CollisionEvent Event(long step, CollisionChannel channel, int a, int b, CollisionEventKind kind = CollisionEventKind.Enter)
        {
            return new CollisionEvent(step, kind, channel, new EntityHandle(a, 1), new EntityHandle(b, 1));
        }

        [Test]
        public void Resolve_DispatchesCollectedEventsInCompareToOrder_ToTheirChannelHandler()
        {
            var log = new List<string>();
            var strategy = new LoopEmCollisions(null);
            var router = new CollisionEventRouter(strategy);
            router.SetHandler(CollisionChannel.PlayerProjectileVsMonster, new LoopEmEventHandler(log, "hit"));
            router.SetHandler(CollisionChannel.PlayerBodyVsGem, new LoopEmEventHandler(log, "gem"));
            strategy.NextEvents.Add(Event(4, CollisionChannel.PlayerBodyVsGem, 1, 9));
            strategy.NextEvents.Add(Event(4, CollisionChannel.PlayerProjectileVsMonster, 5, 3, CollisionEventKind.Exit));
            strategy.NextEvents.Add(Event(4, CollisionChannel.PlayerProjectileVsMonster, 5, 2));
            strategy.NextEvents.Add(Event(4, CollisionChannel.PlayerProjectileVsMonster, 5, 3));

            router.OnPhase(SimulationPhase.Collision, At(4));
            CollectionAssert.AreEqual(new long[] { 4 }, strategy.CollectedSteps);
            Assert.AreEqual(4, router.PendingEventCount);
            Assert.AreEqual(0, log.Count, "nothing dispatched in Collision");

            router.OnPhase(SimulationPhase.Resolve, At(4));
            CollectionAssert.AreEqual(new[]
            {
                "hit:4,PlayerProjectileVsMonster,5:1,2:1,Enter",
                "hit:4,PlayerProjectileVsMonster,5:1,3:1,Enter",
                "hit:4,PlayerProjectileVsMonster,5:1,3:1,Exit",
                "gem:4,PlayerBodyVsGem,1:1,9:1,Enter",
            }, log);
            Assert.AreEqual(0, router.PendingEventCount);
        }

        [Test]
        public void ChannelWithoutHandler_EventsAreDropped()
        {
            var log = new List<string>();
            var strategy = new LoopEmCollisions(null);
            var router = new CollisionEventRouter(strategy);
            router.SetHandler(CollisionChannel.PlayerBodyVsChest, new LoopEmEventHandler(log, "chest"));
            strategy.NextEvents.Add(Event(1, CollisionChannel.PlayerBodyVsMonster, 1, 2));
            strategy.NextEvents.Add(Event(1, CollisionChannel.PlayerBodyVsChest, 1, 3));
            router.OnPhase(SimulationPhase.Collision, At(1));
            Assert.DoesNotThrow(() => router.OnPhase(SimulationPhase.Resolve, At(1)));
            CollectionAssert.AreEqual(new[] { "chest:1,PlayerBodyVsChest,1:1,3:1,Enter" }, log);
        }

        [Test]
        public void SetHandler_TwiceOnChannel_Throws()
        {
            var router = new CollisionEventRouter(new LoopEmCollisions(null));
            router.SetHandler(CollisionChannel.PlayerOrbVsMonster, new LoopEmEventHandler(new List<string>(), "a"));
            Assert.Throws<InvalidOperationException>(() =>
                router.SetHandler(CollisionChannel.PlayerOrbVsMonster, new LoopEmEventHandler(new List<string>(), "b")));
            Assert.Throws<ArgumentNullException>(() => router.SetHandler(CollisionChannel.PlayerZoneVsMonster, null));
            Assert.Throws<ArgumentNullException>(() => new CollisionEventRouter(null));
        }

        [Test]
        public void ReplaceStrategy_CollectsFromTheNewStrategy()
        {
            var log = new List<string>();
            var oldStrategy = new LoopEmCollisions(null);
            var newStrategy = new LoopEmCollisions(null);
            var router = new CollisionEventRouter(oldStrategy);
            router.SetHandler(CollisionChannel.PlayerMagnetVsGem, new LoopEmEventHandler(log, "magnet"));
            newStrategy.NextEvents.Add(Event(0, CollisionChannel.PlayerMagnetVsGem, 2, 7));
            router.ReplaceStrategy(newStrategy);
            Assert.AreSame(newStrategy, router.Strategy);
            router.OnPhase(SimulationPhase.Collision, At(0));
            router.OnPhase(SimulationPhase.Resolve, At(0));
            Assert.AreEqual(0, oldStrategy.CollectedSteps.Count);
            Assert.AreEqual(1, log.Count);
            Assert.Throws<ArgumentNullException>(() => router.ReplaceStrategy(null));
        }
    }
}
