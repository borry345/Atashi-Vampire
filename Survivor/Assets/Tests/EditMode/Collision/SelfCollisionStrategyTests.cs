using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;
using static Survivor.Tests.EditMode.Collision.CollisionEmWorld;

namespace Survivor.Tests.EditMode.Collision
{
    /// SelfCollisionStrategy: Enter/Exit diffing, R5 boundary, channel A/B, candidate pruning and contract errors (TAC-F18-1..8).
    public sealed class SelfCollisionStrategyTests
    {
        private static CollisionEvent Ev(long step, CollisionEventKind kind, CollisionChannel ch, CollisionBody a, CollisionBody b)
        {
            return new CollisionEvent(step, kind, ch, a.Id, b.Id);
        }

        private static void AssertEvents(IReadOnlyList<CollisionEvent> actual, params CollisionEvent[] expected)
        {
            Assert.AreEqual(CollisionEmUtil.Format(expected), CollisionEmUtil.Format(actual));
        }

        [Test]
        public void Kind_IsSelfSquaredDistance()
        {
            Assert.AreEqual(CollisionStrategyKind.SelfSquaredDistance, new CollisionEmWorld().Strategy.Kind);
        }

        [Test]
        public void EnterOnce_NothingWhilePersisting_ExitOnSeparation()
        {
            var world = new CollisionEmWorld();
            CollisionBody shot = world.Add(Body(1, CollisionLayer.PlayerProjectile, -5f, 0f, 0.2f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 0f, 0f, 0.5f));

            AssertEvents(world.Step(0));
            shot.Position = new Vector2(-0.6f, 0f);
            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, monster));
            shot.Position = new Vector2(-0.3f, 0f);
            AssertEvents(world.Step(2));
            AssertEvents(world.Step(3));
            shot.Position = new Vector2(5f, 0f);
            AssertEvents(world.Step(4), Ev(4, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, shot, monster));
            AssertEvents(world.Step(5));
        }

        [Test]
        public void ExactTouch_IsOverlap_JustApart_IsNot()
        {
            var world = new CollisionEmWorld();
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody gem = world.Add(Body(2, CollisionLayer.Gem, 0.75f, 0f, 0.25f));

            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsGem, player, gem));
            gem.Position = new Vector2(0.75f + 0.001f, 0f);
            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Exit, CollisionChannel.PlayerBodyVsGem, player, gem));
        }

        [Test]
        public void RegisterWhileOverlapping_EntersOnFirstCollect()
        {
            var world = new CollisionEmWorld();
            CollisionBody monster = world.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 0.5f));
            AssertEvents(world.Step(0));

            CollisionBody orb = world.Add(Body(2, CollisionLayer.PlayerOrb, 0.2f, 0f, 0.3f));

            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, orb, monster));
        }

        [Test]
        public void Unregister_WhileOverlapping_NoExit_ReRegisterNewGeneration_Enters()
        {
            var world = new CollisionEmWorld();
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 0.5f, 0f, 0.5f));
            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));

            world.Remove(monster);
            AssertEvents(world.Step(1));
            AssertEvents(world.Step(2));
            Assert.AreEqual(-1, monster.StrategySlot);

            monster.Id = new EntityHandle(2, 2);
            world.Add(monster);
            AssertEvents(world.Step(3), Ev(3, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));
        }

        [Test]
        public void EveryChannel_ReportsTheALayerBodyAsA()
        {
            var world = new CollisionEmWorld();
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

            List<CollisionEvent> events = world.Step(7);

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
            Assert.AreEqual("7,EnemyProjectileVsPlayerBody,5:1,20:1,Enter", events[4].ToString());
        }

        [Test]
        public void PairsWithoutChannel_ProduceNoEvents()
        {
            var world = new CollisionEmWorld();
            // Every body overlaps every other one, but none of these layer pairs is a channel.
            world.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 1f));
            world.Add(Body(2, CollisionLayer.Monster, 0.1f, 0f, 1f));
            world.Add(Body(3, CollisionLayer.Gem, 0f, 0.1f, 1f));
            world.Add(Body(4, CollisionLayer.Chest, 0.1f, 0.1f, 1f));
            world.Add(Body(5, CollisionLayer.PlayerMagnet, 0f, 0f, 1f));
            world.Add(Body(6, CollisionLayer.EnemyProjectile, 0f, 0f, 1f));
            world.Add(Body(7, CollisionLayer.PlayerProjectile, 0f, 0f, 1f));
            world.Add(Body(8, CollisionLayer.PlayerProjectile, 0f, 0f, 1f));

            List<CollisionEvent> events = world.Step(0);

            // Only PlayerMagnet-Gem and the two projectiles vs the monsters are channels.
            foreach (CollisionEvent e in events)
            {
                Assert.That(e.Channel == CollisionChannel.PlayerMagnetVsGem || e.Channel == CollisionChannel.PlayerProjectileVsMonster, e.ToString());
            }

            Assert.AreEqual(5, events.Count, CollisionEmUtil.Format(events));
        }

        [Test]
        public void LargeMonster_FarFromQueryCells_IsDetectedOnce()
        {
            var world = new CollisionEmWorld();
            CollisionBody boss = world.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 6f, isLarge: true));
            CollisionBody small = world.Add(Body(2, CollisionLayer.Monster, 40f, 40f, 0.3f));
            // 5.9 cells away from the boss center, inside its radius 6.
            CollisionBody shot = world.Add(Body(3, CollisionLayer.PlayerProjectile, 5.9f, 0f, 0.1f));

            AssertEvents(world.Step(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, boss));
            Assert.That(world.Strategy.CheckedPairCount, Is.EqualTo(1), "the far small monster is pruned by the hash");
            _ = small;
        }

        [Test]
        public void MonsterOutsideSnapshot_IsSkippedUntilItAppears()
        {
            var world = new CollisionEmWorld();
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 0.2f, 0f, 0.3f));
            world.HiddenFromSnapshot.Add(monster);

            AssertEvents(world.Step(0));

            world.HiddenFromSnapshot.Clear();
            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));
        }

        [Test]
        public void SnapshotEntryWithoutRegisteredBody_IsIgnored()
        {
            var world = new CollisionEmWorld();
            world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            world.Snapshot.Clear();
            world.Snapshot.Add(new MonsterSnapshot { Id = new EntityHandle(9, 1), Position = Vector2.zero, Radius = 0.5f });
            world.Index.OnPhase(SimulationPhase.BuildIndex, default);

            AssertEvents(world.Strategy.CollectEvents(0));
        }

        [Test]
        public void MonsterPosition_IsReadFromTheSnapshot()
        {
            var world = new CollisionEmWorld();
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 10f, 0f, 0.3f));
            world.Snapshot.Clear();
            world.Snapshot.Add(new MonsterSnapshot { Id = monster.Id, Position = new Vector2(0.1f, 0f), Radius = 0.3f });
            world.Index.OnPhase(SimulationPhase.BuildIndex, default);

            AssertEvents(world.Strategy.CollectEvents(0), Ev(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, player, monster));
        }

        [Test]
        public void RadiusGrowth_IsPickedUpWithoutNotification()
        {
            var world = new CollisionEmWorld();
            CollisionBody zone = world.Add(Body(1, CollisionLayer.PlayerZone, 0f, 0f, 0.5f));
            CollisionBody monster = world.Add(Body(2, CollisionLayer.Monster, 2f, 0f, 0.3f));
            AssertEvents(world.Step(0));

            zone.Radius = 1.8f;
            world.Strategy.NotifyRadiusChanged(zone);

            AssertEvents(world.Step(1), Ev(1, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, zone, monster));
        }

        [Test]
        public void CollectEvents_ReusesItsList_AndSortsByCompareTo()
        {
            var world = new CollisionEmWorld();
            world.Add(Body(30, CollisionLayer.PlayerBody, 0f, 0f, 1f));
            for (int i = 10; i >= 1; i--)
            {
                world.Add(Body(i, CollisionLayer.Monster, 0.1f * i, 0f, 0.2f));
                world.Add(Body(100 + i, CollisionLayer.Gem, 0f, 0.05f * i, 0.1f));
            }

            world.Add(Body(40, CollisionLayer.PlayerProjectile, 0.5f, 0f, 0.5f));

            // Before any BuildIndex the snapshot is empty: only the gem pairs enter here, the monster pairs at step 0.
            IReadOnlyList<CollisionEvent> first = world.Strategy.CollectEvents(-1);
            List<CollisionEvent> events = world.Step(0);
            IReadOnlyList<CollisionEvent> second = world.Strategy.CollectEvents(1);

            Assert.AreSame(first, second);
            Assert.That(events.Count, Is.GreaterThan(10));
            for (int i = 1; i < events.Count; i++)
            {
                Assert.That(events[i - 1].CompareTo(events[i]), Is.LessThan(0), events[i - 1] + " / " + events[i]);
            }
        }

        [Test]
        public void RandomScenario_MatchesBruteForceReference_EveryStep()
        {
            var world = new CollisionEmWorld();
            var rng = new DeterministicRng(20261107UL, 1UL);
            var velocities = new Dictionary<CollisionBody, Vector2>();
            CollisionLayer[] layers = (CollisionLayer[])Enum.GetValues(typeof(CollisionLayer));
            int nextIndex = 1;
            for (int i = 0; i < 160; i++)
            {
                CollisionBody body = RandomBody(rng, layers, nextIndex++);
                world.Add(body);
                velocities[body] = RandomVelocity(rng);
            }

            int compared = 0;
            for (long step = 0; step < 240; step++)
            {
                List<CollisionEvent> actual = world.Step(step);
                List<CollisionEvent> expected = world.Reference(step);
                Assert.AreEqual(CollisionEmUtil.Format(expected), CollisionEmUtil.Format(actual), "step " + step);
                compared += expected.Count;

                foreach (CollisionBody body in world.Bodies)
                {
                    body.Position += velocities[body];
                }

                // Churn: despawn one body and spawn a new one every 10 steps (silent unregister, Enter on register).
                if (step % 10 == 5)
                {
                    CollisionBody victim = world.Bodies[rng.NextInt(world.Bodies.Count)];
                    world.Remove(victim);
                    CollisionBody spawned = RandomBody(rng, layers, nextIndex++);
                    world.Add(spawned);
                    velocities[spawned] = RandomVelocity(rng);
                }
            }

            Assert.That(compared, Is.GreaterThan(100), "scenario must actually produce events");
        }

        [Test]
        public void RegistrationOrder_DoesNotChangeTheEvents()
        {
            List<string> forward = RunOrderScenario(reverse: false);
            List<string> backward = RunOrderScenario(reverse: true);

            Assert.That(forward.Count, Is.GreaterThan(20));
            CollectionAssert.AreEqual(forward, backward);
        }

        [Test]
        public void Clear_ForgetsBodiesAndPairs()
        {
            var world = new CollisionEmWorld();
            CollisionBody player = world.Add(Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
            CollisionBody gem = world.Add(Body(2, CollisionLayer.Gem, 0.1f, 0f, 0.2f));
            Assert.AreEqual(1, world.Step(0).Count);

            world.Strategy.Clear();

            Assert.AreEqual(-1, player.StrategySlot);
            Assert.AreEqual(-1, gem.StrategySlot);
            Assert.AreEqual(0, world.Strategy.CollectEvents(1).Count);
            world.Strategy.Register(player);
            world.Strategy.Register(gem);
            AssertEvents(world.Step(2), Ev(2, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsGem, player, gem));
        }

        [Test]
        public void CheckedPairCount_IsBelowBruteForceForASpreadLayout()
        {
            var world = new CollisionEmWorld();
            int index = 1;
            for (int x = 0; x < 20; x++)
            {
                for (int y = 0; y < 20; y++)
                {
                    world.Add(Body(index++, CollisionLayer.Monster, x * 3f, y * 3f, 0.3f));
                }
            }

            for (int i = 0; i < 50; i++)
            {
                world.Add(Body(index++, CollisionLayer.PlayerProjectile, i * 1.1f, i * 1.1f, 0.1f));
            }

            world.Step(0);

            Assert.That(world.Strategy.CheckedPairCount, Is.GreaterThan(0));
            Assert.That(world.Strategy.CheckedPairCount, Is.LessThan(400 * 50 / 10));
        }

        [Test]
        public void Constructor_RejectsNullAndForeignSnapshot()
        {
            var snapshot = new MonsterSnapshotBuffer();
            var index = new MonsterSpatialIndex(2f, snapshot);

            Assert.Throws<ArgumentNullException>(() => new SelfCollisionStrategy(null, snapshot));
            Assert.Throws<ArgumentNullException>(() => new SelfCollisionStrategy(index, null));
            Assert.Throws<ArgumentException>(() => new SelfCollisionStrategy(index, new MonsterSnapshotBuffer()));
        }

        [Test]
        public void ContractViolations_LogAndThrow()
        {
            var world = new CollisionEmWorld();
            CollisionBody body = world.Add(Body(1, CollisionLayer.Gem, 0f, 0f, 0.2f));
            CollisionBody stranger = Body(2, CollisionLayer.Gem, 0f, 0f, 0.2f);

            Assert.Throws<ArgumentNullException>(() => world.Strategy.Register(null));
            Assert.Throws<ArgumentNullException>(() => world.Strategy.Unregister(null));
            Assert.Throws<ArgumentNullException>(() => world.Strategy.NotifyRadiusChanged(null));

            CollisionEmUtil.ExpectCollisionError(@"SelfCollisionStrategy\.Register: body 1:1 is already registered");
            Assert.Throws<InvalidOperationException>(() => world.Strategy.Register(body));
            CollisionEmUtil.ExpectCollisionError(@"SelfCollisionStrategy\.Unregister: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => world.Strategy.Unregister(stranger));
            CollisionEmUtil.ExpectCollisionError(@"SelfCollisionStrategy\.NotifyRadiusChanged: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => world.Strategy.NotifyRadiusChanged(stranger));

            // A body registered with another strategy instance is rejected too (shared StrategySlot).
            var other = new CollisionEmWorld();
            CollisionEmUtil.ExpectCollisionError(@"SelfCollisionStrategy\.Register: body 1:1 is already registered");
            Assert.Throws<InvalidOperationException>(() => other.Strategy.Register(body));
            LogAssert.NoUnexpectedReceived();
        }

        private static List<string> RunOrderScenario(bool reverse)
        {
            var world = new CollisionEmWorld();
            var rng = new DeterministicRng(77UL, 3UL);
            CollisionLayer[] layers = (CollisionLayer[])Enum.GetValues(typeof(CollisionLayer));
            var bodies = new List<CollisionBody>();
            var velocities = new List<Vector2>();
            for (int i = 0; i < 80; i++)
            {
                bodies.Add(RandomBody(rng, layers, i + 1));
                velocities.Add(RandomVelocity(rng));
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                world.Add(bodies[reverse ? bodies.Count - 1 - i : i]);
            }

            var log = new List<string>();
            for (long step = 0; step < 120; step++)
            {
                foreach (CollisionEvent e in world.Step(step))
                {
                    log.Add(e.ToString());
                }

                for (int i = 0; i < bodies.Count; i++)
                {
                    bodies[i].Position += velocities[i];
                }
            }

            return log;
        }

        private static CollisionBody RandomBody(DeterministicRng rng, CollisionLayer[] layers, int index)
        {
            CollisionLayer layer = layers[rng.NextInt(layers.Length)];
            float radius = 0.1f + rng.NextFloat01() * 0.6f;
            bool large = false;
            if (layer == CollisionLayer.Monster && rng.NextInt(10) == 0)
            {
                radius = 1.2f + rng.NextFloat01() * 2f;
                large = true;
            }

            if (layer == CollisionLayer.PlayerMagnet || layer == CollisionLayer.PlayerZone)
            {
                radius += 1f;
            }

            return Body(index, layer, rng.NextFloat01() * 16f - 8f, rng.NextFloat01() * 16f - 8f, radius, large);
        }

        private static Vector2 RandomVelocity(DeterministicRng rng)
        {
            return new Vector2(rng.NextFloat01() * 0.2f - 0.1f, rng.NextFloat01() * 0.2f - 0.1f);
        }
    }
}
