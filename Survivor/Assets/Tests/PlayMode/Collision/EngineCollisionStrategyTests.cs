using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;
using static Survivor.Tests.PlayMode.Collision.CollisionPmUtil;

namespace Survivor.Tests.PlayMode.Collision
{
    /// EngineCollisionStrategy: trigger callbacks -> sorted Enter/Exit (TAC-F18-4..6, 8), layer matrix filtering (U12), errors.
    public sealed class EngineCollisionStrategyTests : CollisionPmFixture
    {
        private static CollisionEvent Ev(long step, CollisionEventKind kind, CollisionChannel ch, CollisionBody a, CollisionBody b)
        {
            return new CollisionEvent(step, kind, ch, a.Id, b.Id);
        }

        private static void AssertEvents(IReadOnlyList<CollisionEvent> actual, params CollisionEvent[] expected)
        {
            Assert.AreEqual(Format(expected), Format(actual));
        }

        [Test]
        public void Kind_AndDetectionMode()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            var strategy = new EngineCollisionStrategy(bodies, CollisionDetectionMode2D.Continuous);

            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, strategy.Kind);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, strategy.DetectionMode);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, bodies.DetectionMode);
        }

        [Test]
        public void EnterOnce_NothingWhilePersisting_ExitOnSeparation()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 3f, 0f, 0.5f));

            AssertEvents(world.Step(0));
            monster.Position = new Vector2(0.8f, 0f);
            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));
            monster.Position = new Vector2(0.6f, 0.1f);
            AssertEvents(world.Step(2));
            AssertEvents(world.Step(3));
            monster.Position = new Vector2(3f, 0f);
            AssertEvents(world.Step(4), Ev(4, CollisionEventKind.Exit, CollisionChannel.PlayerBodyVsMonster, player, monster));
            AssertEvents(world.Step(5));
        }

        [Test]
        public void ExactTouch_Enters_JustApart_Exits_U9()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody gem = world.Add(Body(2, CollisionLayer.Gem, 0.75f, 0f, 0.25f));

            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsGem, player, gem));
            gem.Position = new Vector2(0.751f, 0f);
            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Exit, CollisionChannel.PlayerBodyVsGem, player, gem));
        }

        [Test]
        public void RegisterWhileOverlapping_EntersOnFirstCollect()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody monster = world.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 0.5f));
            AssertEvents(world.Step(0));

            CollisionBody orb = world.Add(Body(2, CollisionLayer.PlayerOrb, 0.2f, 0f, 0.3f));

            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, orb, monster));
        }

        [Test]
        public void Unregister_WhileOverlapping_NoExit_ReusedHostWithNewId_Enters()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 0.5f, 0f, 0.5f));
            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));

            world.Remove(monster);
            AssertEvents(world.Step(1));
            AssertEvents(world.Step(2));

            // Pool reuse: same host and body object, new generation, spawned at another overlapping spot.
            monster.Id = new EntityHandle(2, 2);
            monster.Position = new Vector2(-0.4f, 0f);
            world.Add(monster);
            AssertEvents(world.Step(3), Ev(3, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));
        }

        [Test]
        public void BodyRegisteredOnAMovingBodysPreviousSpot_ReportsNothing_LikeTheSelfStrategy()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            var strategy = (EngineCollisionStrategy)world.Strategy;
            CollisionBody monster = world.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f));
            AssertEvents(world.Step(0));

            // The monster moves away this step; a projectile spawns where the monster was (distance 1 > radius sum 0.4).
            monster.Position = new Vector2(1f, 0f);
            world.Add(Body(2, CollisionLayer.PlayerProjectile, 0f, 0f, 0.1f));

            AssertEvents(world.Step(1));
            Assert.AreEqual(1, strategy.CancelledTransitionCount, "Box2D reported Enter then Exit within the step");
            Assert.AreEqual(4, strategy.CallbackCount, "Enter and Exit, each to both hosts");
        }

        [Test]
        public void EveryChannel_ReportsTheALayerBodyAsA_AndNonChannelPairsNeverCallBack_U12()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            var strategy = (EngineCollisionStrategy)world.Strategy;
            CollisionBody player = world.Add(Body(20, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody magnet = world.Add(Body(21, CollisionLayer.PlayerMagnet, 0f, 0f, 2f));
            CollisionBody monster = world.Add(Body(1, CollisionLayer.Monster, 0.4f, 0f, 0.3f));
            CollisionBody shot = world.Add(Body(2, CollisionLayer.PlayerProjectile, 0.5f, 0.1f, 0.1f));
            CollisionBody orb = world.Add(Body(3, CollisionLayer.PlayerOrb, 0.5f, -0.1f, 0.1f));
            CollisionBody zone = world.Add(Body(4, CollisionLayer.PlayerZone, 0f, 0f, 3f));
            CollisionBody enemyShot = world.Add(Body(5, CollisionLayer.EnemyProjectile, -0.3f, 0f, 0.1f));
            CollisionBody enemyZone = world.Add(Body(6, CollisionLayer.EnemyZone, 0f, -0.5f, 0.2f));
            CollisionBody gem = world.Add(Body(7, CollisionLayer.Gem, 0f, 0.4f, 0.2f));
            CollisionBody chest = world.Add(Body(8, CollisionLayer.Chest, 0f, -0.3f, 0.3f));
            world.Add(Body(9, CollisionLayer.Monster, 0.45f, 0f, 0.3f));

            List<CollisionEvent> events = world.Step(7);

            // Monster 9 overlaps the same channel partners as monster 1; filter it out of the explicit list below.
            events.RemoveAll(e => e.B.Index == 9);
            AssertEvents(events,
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, monster),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, orb, monster),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, zone, monster),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.EnemyProjectileVsPlayerBody, enemyShot, player),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.EnemyZoneVsPlayerBody, enemyZone, player),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerMagnetVsGem, magnet, gem),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsGem, player, gem),
                Ev(7, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsChest, player, chest));
            // Many non-channel pairs overlap here (monster-monster, magnet-chest, zone-gem, ...): the matrix filters them all.
            Assert.AreEqual(0, strategy.DroppedCallbackCount, "U12: no callback for a layer pair without a channel");
            // 13 channel pairs (9 above + 4 with monster 9), each reported to both hosts.
            Assert.AreEqual(26, strategy.CallbackCount);
        }

        [Test]
        public void RadiusGrowth_UpdatesTheCollider()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody zone = world.Add(Body(1, CollisionLayer.PlayerZone, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 2f, 0f, 0.3f));
            AssertEvents(world.Step(0));

            zone.Radius = 1.8f;
            world.Strategy.NotifyRadiusChanged(zone);

            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, zone, monster));
            Assert.AreEqual(1.8f, zone.Host.GetComponent<CircleCollider2D>().radius);
        }

        [Test]
        public void RegistrationOrder_DoesNotChangeTheEvents()
        {
            List<string> forward = RunOrder(reverse: false);
            DestroyAll();
            List<string> backward = RunOrder(reverse: true);

            Assert.That(forward.Count, Is.GreaterThan(5));
            CollectionAssert.AreEqual(forward, backward);
        }

        [Test]
        public void Clear_WithRegisteredBodies_WarnsAndDropsThem()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            world.Add(Body(2, CollisionLayer.Gem, 0.1f, 0f, 0.2f));
            Assert.AreEqual(1, world.Step(0).Count);

            ExpectCollisionLog(LogType.Warning, "WARN", @"EngineCollisionStrategy\.Clear: 2 body\(ies\) still registered; dropping them\.");
            world.Strategy.Clear();

            Assert.AreEqual(-1, player.StrategySlot);
            Assert.AreEqual(0, world.Strategy.CollectEvents(1).Count, "callbacks of dropped bodies are filtered");
            world.Strategy.Clear();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ContractViolations_LogAndThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new EngineCollisionStrategy(null, CollisionDetectionMode2D.Discrete));
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody body = world.Add(Body(1, CollisionLayer.Gem, 0f, 0f, 0.2f));
            CollisionBody notSynced = Body(2, CollisionLayer.Gem, 0f, 0f, 0.2f);
            ICollisionStrategy strategy = world.Strategy;

            Assert.Throws<ArgumentNullException>(() => strategy.Register(null));
            Assert.Throws<ArgumentNullException>(() => strategy.Unregister(null));
            Assert.Throws<ArgumentNullException>(() => strategy.NotifyRadiusChanged(null));
            ExpectCollisionLog(LogType.Error, "ERROR", @"EngineCollisionStrategy\.Register: body 1:1 is already registered");
            Assert.Throws<InvalidOperationException>(() => strategy.Register(body));
            ExpectCollisionLog(LogType.Error, "ERROR", @"EngineCollisionStrategy\.Register: body 2:1 is not registered with the PhysicsBodySynchronizer\.");
            Assert.Throws<InvalidOperationException>(() => strategy.Register(notSynced));
            ExpectCollisionLog(LogType.Error, "ERROR", @"EngineCollisionStrategy\.Unregister: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => strategy.Unregister(notSynced));
            ExpectCollisionLog(LogType.Error, "ERROR", @"EngineCollisionStrategy\.NotifyRadiusChanged: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => strategy.NotifyRadiusChanged(notSynced));

            // The same body cannot be registered with a self strategy at the same time (shared StrategySlot).
            var snapshot = new MonsterSnapshotBuffer();
            var self = new SelfCollisionStrategy(new MonsterSpatialIndex(2f, snapshot), snapshot);
            ExpectCollisionLog(LogType.Error, "ERROR", @"SelfCollisionStrategy\.Register: body 1:1 is already registered");
            Assert.Throws<InvalidOperationException>(() => self.Register(body));
            LogAssert.NoUnexpectedReceived();
        }

        // TAC-F18-9 1-3: projectile A (0,0) r 0.2, monster B on the x axis r 0.5. Just outside the radius sum, R5 is false while
        // Box2D's overlap tolerance still reports contact; the reported state follows R5 only.
        [Test]
        public void R5Gate_BoundaryContact_NoEvent_ThenEnterAtR5_ThenExitWithoutBox2DExit_TAC_F18_9()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            var strategy = (EngineCollisionStrategy)world.Strategy;
            CollisionBody shot = world.Add(Body(1, CollisionLayer.PlayerProjectile, 0f, 0f, 0.2f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 3f, 0f, 0.5f));
            AssertEvents(world.Step(0));

            float inside = 0.7f;
            Assert.IsTrue(CircleMath.Overlaps(shot.Position, shot.Radius, new Vector2(inside, 0f), monster.Radius), "0.7f is inside R5");
            float boundary = FirstNonOverlappingX(shot, monster.Radius, inside);

            // 1. Boundary band: Box2D touches (precondition: the deferred counter grows), R5 is false -> no event.
            monster.Position = new Vector2(boundary, 0f);
            int deferredBefore = strategy.BoundaryDeferredCount;
            AssertEvents(world.Step(1));
            Assert.AreEqual(deferredBefore + 1, strategy.BoundaryDeferredCount, $"precondition: Box2D reports contact at x = {boundary:R}");
            Assert.That(strategy.CallbackCount, Is.EqualTo(2), "Box2D Enter to both hosts");

            // 2. R5 becomes true while Box2D stays in contact (no callback): Enter exactly once, channel 0, A = projectile.
            monster.Position = new Vector2(inside, 0f);
            List<CollisionEvent> entered = world.Step(2);
            AssertEvents(entered, Ev(2, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, monster));
            Assert.AreEqual(0, (byte)entered[0].Channel);
            Assert.AreEqual(0, strategy.CallbackCount, "no Box2D callback in this step");
            AssertEvents(world.Step(3));

            // 3. Back into the band: Box2D still touches (no Exit callback) but R5 is false -> Exit in this step.
            monster.Position = new Vector2(boundary, 0f);
            deferredBefore = strategy.BoundaryDeferredCount;
            AssertEvents(world.Step(4), Ev(4, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, shot, monster));
            Assert.AreEqual(0, strategy.CallbackCount, "no Box2D Exit callback");
            Assert.AreEqual(deferredBefore + 1, strategy.BoundaryDeferredCount);

            world.Remove(shot);
            world.Remove(monster);
            strategy.Clear();
            Assert.AreEqual(0, strategy.BoundaryDeferredCount, "Clear resets the counter");
        }

        // TAC-F18-9 4: a reported pair stays a candidate after Box2D reports Exit, and Exit is reported only when R5 turns false.
        // No natural placement makes Box2D narrower than R5, so the test shrinks the monster's collider behind the strategy's back
        // (the body radius, which R5 reads, is unchanged, so the synchronizer does not restore the collider).
        [Test]
        public void R5Gate_Box2DExitWhileR5StillTrue_NoExitUntilR5False_TAC_F18_9()
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            var strategy = (EngineCollisionStrategy)world.Strategy;
            CollisionBody shot = world.Add(Body(1, CollisionLayer.PlayerProjectile, 0f, 0f, 0.2f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 0.6f, 0f, 0.5f));
            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, monster));

            monster.Host.GetComponent<CircleCollider2D>().radius = 0.3f;
            AssertEvents(world.Step(1));
            Assert.AreEqual(2, strategy.CallbackCount, "Box2D Exit to both hosts while R5 (0.6 <= 0.7) is still true");
            AssertEvents(world.Step(2));

            monster.Position = new Vector2(0.75f, 0f);
            AssertEvents(world.Step(3), Ev(3, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, shot, monster));
            Assert.AreEqual(0, strategy.CallbackCount, "Exit comes from R5, not from a Box2D callback");
        }

        private static float FirstNonOverlappingX(CollisionBody a, float radius, float from)
        {
            float x = from;
            for (int i = 0; i < 64; i++)
            {
                x = NextUp(x);
                if (!CircleMath.Overlaps(a.Position, a.Radius, new Vector2(x, 0f), radius))
                {
                    return x;
                }
            }

            Assert.Fail($"no non-overlapping x within 64 ulps above {from:R}");
            return x;
        }

        private static float NextUp(float x)
        {
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(x), 0);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
        }

        private static List<string> RunOrder(bool reverse)
        {
            CollisionPmWorld world = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            var bodies = new List<CollisionBody>
            {
                Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f),
                Body(2, CollisionLayer.PlayerMagnet, 0f, 0f, 1.5f),
            };
            for (int i = 0; i < 12; i++)
            {
                CollisionLayer layer = (i % 3) switch { 0 => CollisionLayer.Monster, 1 => CollisionLayer.Gem, _ => CollisionLayer.EnemyProjectile };
                bodies.Add(Body(10 + i, layer, -3f + i * 0.5f, 0.2f * (i % 2), 0.2f));
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                world.Add(bodies[reverse ? bodies.Count - 1 - i : i]);
            }

            var log = new List<string>();
            for (long step = 0; step < 40; step++)
            {
                foreach (CollisionEvent e in world.Step(step))
                {
                    log.Add(e.ToString());
                }

                bodies[0].Position += new Vector2(0.2f, 0f);
                bodies[1].Position = bodies[0].Position;
                if (step == 20)
                {
                    bodies[0].Position = new Vector2(-4f, 0f);
                }
            }

            return log;
        }
    }
}
