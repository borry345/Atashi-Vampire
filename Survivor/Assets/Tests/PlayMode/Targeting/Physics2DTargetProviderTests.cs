using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using Survivor.Tests.PlayMode.Collision;
using UnityEngine;
using UnityEngine.TestTools;
using static Survivor.Tests.PlayMode.Targeting.TargetingPmWorld;

namespace Survivor.Tests.PlayMode.Targeting
{
    /// Physics2DTargetProvider (C, TAC-F16-3): contract, widening and re-query, R6 ties, large monsters, disabled colliders.
    public sealed class Physics2DTargetProviderTests : CollisionPmFixture
    {
        [Test]
        public void Ctor_NullBodies_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Physics2DTargetProvider(null, MonsterMask));
        }

        [Test]
        public void Ctor_EmptyMask_ThrowsArgumentOutOfRange()
        {
            var bodies = new PhysicsBodySynchronizer(CollisionPmUtil.PhysicsTargetRequirements());
            Assert.Throws<ArgumentOutOfRangeException>(() => new Physics2DTargetProvider(bodies, 0));
        }

        [Test]
        public void Kind_IsPhysics2DQuery()
        {
            var provider = new Physics2DTargetProvider(new PhysicsBodySynchronizer(CollisionPmUtil.PhysicsTargetRequirements()), MonsterMask);
            Assert.AreEqual(TargetProviderKind.Physics2DQuery, provider.Kind);
        }

        [Test]
        public void TryFindNearest_BeforeSnapshot_ReturnsFalse()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            var provider = new Physics2DTargetProvider(world.Bodies, MonsterMask);

            Assert.IsFalse(provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(default(EntityHandle), hit.Id);
        }

        [Test]
        public void TryFindNearest_NoMonstersNoBodies_ReturnsFalse()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(Array.Empty<MonsterSnapshot>());

            Assert.IsFalse(world.Physics.TryFindNearest(new Vector2(3f, -4f), out _));
        }

        [Test]
        public void OnSnapshotBuilt_Null_ForgetsSnapshot()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(1, 1, 1f, 1f, 0.4f) });
            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out _));

            Assert.DoesNotThrow(() => world.Physics.OnSnapshotBuilt(null));

            Assert.IsFalse(world.Physics.TryFindNearest(Vector2.zero, out _));
        }

        [Test]
        public void TryFindNearest_MonsterCollidersDisabled_ReturnsFalseWithoutThrowingOrLogging()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.SelfRequirements());
            world.Load(new[] { Monster(1, 1, 1f, 0f, 0.4f), Monster(2, 1, 30f, -20f, 0.4f) });

            bool found = true;
            Assert.DoesNotThrow(() => found = world.Physics.TryFindNearest(Vector2.zero, out _));

            Assert.IsFalse(found);
            Assert.IsTrue(world.BruteForce.TryFindNearest(Vector2.zero, out _));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryFindNearest_SingleFarMonster_WidensUpToFarthestCorner()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(7, 2, 500f, -300f, 0.3f) });

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit hit));

            Assert.AreEqual(new EntityHandle(7, 2), hit.Id);
            Assert.AreEqual(new Vector2(500f, -300f), hit.Position);
            Assert.AreEqual(new Vector2(500f, -300f).sqrMagnitude, hit.DistanceSquared);
            Assert.Greater(world.CountOverlapCalls(Vector2.zero), 2);
            Assert.AreEqual(string.Empty, world.Compare(Vector2.zero, out _));
        }

        [Test]
        public void TryFindNearest_EquidistantMonsters_PicksSmallestIdRegardlessOfOrder()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[]
            {
                Monster(2, 2, 0f, 3f, 0.4f),
                Monster(2, 3, -3f, 0f, 0.4f),
                Monster(5, 1, 3f, 0f, 0.4f),
                Monster(6, 1, 0f, -3f, 0.4f),
            });

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit hit));

            Assert.AreEqual(new EntityHandle(2, 2), hit.Id);
            Assert.AreEqual(9f, hit.DistanceSquared);
            Assert.AreEqual(string.Empty, world.Compare(Vector2.zero, out _));
        }

        [Test]
        public void TryFindNearest_LargeMonsterHitFirst_ReQueryFindsCloserCenter()
        {
            // The boss collider reaches the first radius-4 circle but its center (9) is farther than the small monster (7.5),
            // which only the re-query with R = 9 + 1e-3 sees.
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(1, 1, 9f, 0f, 6f, isLarge: true), Monster(3, 1, 0f, 7.5f, 0.3f) });

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit hit));

            Assert.AreEqual(new EntityHandle(3, 1), hit.Id);
            Assert.AreEqual(56.25f, hit.DistanceSquared);
            Assert.AreEqual(2, world.CountOverlapCalls(Vector2.zero));
        }

        [Test]
        public void TryFindNearest_LargeMonsterNearest_IsReturned()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(1, 1, 0f, 12f, 0.4f), Monster(4, 1, -10f, 0f, 5f, isLarge: true) });

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit hit));

            Assert.AreEqual(new EntityHandle(4, 1), hit.Id);
            Assert.AreEqual(100f, hit.DistanceSquared);
            Assert.AreEqual(string.Empty, world.Compare(Vector2.zero, out _));
        }

        [Test]
        public void TryFindNearest_FollowsSnapshotAfterMonstersMove()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(1, 1, 2f, 0f, 0.4f), Monster(2, 1, 20f, 0f, 0.4f) });
            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit before));
            Assert.AreEqual(new EntityHandle(1, 1), before.Id);

            world.Registered[0].Position = new Vector2(40f, 40f);
            world.Registered[1].Position = new Vector2(-1f, 1f);
            world.Step();

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit after));
            Assert.AreEqual(new EntityHandle(2, 1), after.Id);
            Assert.AreEqual(new Vector2(-1f, 1f), after.Position);
            Assert.AreEqual(string.Empty, world.Compare(Vector2.zero, out _));
        }

        [Test]
        public void TryFindNearest_IgnoresBodiesOutsideSnapshotAndOtherLayers()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.EngineRequirements());
            world.Load(new[] { Monster(4, 1, 6f, 0f, 0.4f) });
            CollisionBody stray = CollisionPmUtil.Body(2, CollisionLayer.Monster, 0.5f, 0f, 0.4f);
            CollisionBody player = CollisionPmUtil.Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.45f);
            world.Bodies.Register(stray);
            world.Bodies.Register(player);
            world.Bodies.SyncMonstersForQuery();

            Assert.IsTrue(world.Physics.TryFindNearest(Vector2.zero, out TargetHit hit));

            Assert.AreEqual(new EntityHandle(4, 1), hit.Id);
            Assert.AreEqual(36f, hit.DistanceSquared);
        }

        [Test]
        public void TryFindNearest_QueryOnTopOfMonsterAndNegativeCoordinates_MatchesBruteForce()
        {
            var world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            world.Load(new[] { Monster(1, 1, -1000.25f, -2000.5f, 0.4f), Monster(2, 1, -1003f, -1996f, 0.4f) });

            Assert.IsTrue(world.Physics.TryFindNearest(new Vector2(-1000.25f, -2000.5f), out TargetHit hit));

            Assert.AreEqual(new EntityHandle(1, 1), hit.Id);
            Assert.AreEqual(0f, hit.DistanceSquared);
            Assert.AreEqual(string.Empty, world.Compare(new Vector2(-1000.25f, -2000.5f), out _));
            Assert.AreEqual(string.Empty, world.Compare(new Vector2(-1e5f, -3e4f), out _));
        }
    }
}
