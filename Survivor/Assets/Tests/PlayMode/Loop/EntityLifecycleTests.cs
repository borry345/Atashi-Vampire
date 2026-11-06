using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    /// Spawn/despawn order, phase restriction, lifetimes, run reset and strategy replacement (5.10, TAC-0-8, TAC-0-9, L4, L5).
    public sealed class EntityLifecycleTests
    {
        [TearDown]
        public void TearDown()
        {
            LoopPmWorld.DestroyTestObjects();
        }

        [Test]
        public void Spawn_RunsTheDocumentedOrder()
        {
            var world = new LoopPmWorld();
            world.Lifecycle.Spawned += e => world.Log.Add("Spawned " + e.Id);
            world.AdvanceSteps(4);
            LoopPmTickEntity spawned = null;
            world.InStep(SimulationPhase.Lifecycle, () =>
                spawned = world.Lifecycle.Spawn<LoopPmTickEntity>(EntityCategory.Monster, new Vector2(2f, 3f), 0, e =>
                {
                    world.Log.Add("initialize id=" + e.Id + " spawnStep=" + e.SpawnStep + " pos=" + e.Body.Position + " active=" + e.gameObject.activeSelf);
                }));

            CollectionAssert.AreEqual(new[]
            {
                "pool.Acquire",
                "initialize id=1:1 spawnStep=4 pos=(2.00, 3.00) active=True",
                "ticks.Register",
                "physics.Register",
                "collisions.Register",
                "Spawned 1:1",
            }, world.Log);
            Assert.AreEqual(new Vector2(2f, 3f), spawned.Position);
            Assert.AreEqual(CollisionLayer.Monster, spawned.Body.Layer);
            Assert.AreSame(spawned.gameObject, spawned.Body.Host);
            Assert.AreEqual(EntityCategory.Monster, spawned.Category);
            Assert.IsTrue(world.Lifecycle.TryGet(spawned.Id, out SimEntity found));
            Assert.AreSame(spawned, found);
            Assert.AreEqual(1, world.Lifecycle.ActiveCount);
        }

        [Test]
        public void Despawn_RunsTheExactReverse_IdReadableInDespawned()
        {
            var world = new LoopPmWorld();
            LoopPmTickEntity entity = null;
            world.InStep(SimulationPhase.Lifecycle, () => entity = world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, 5));
            EntityHandle id = entity.Id;
            world.Log.Clear();
            world.Lifecycle.Despawning += e => world.Log.Add("Despawning " + e.Id);
            world.Lifecycle.Despawned += (e, reason) => world.Log.Add("Despawned " + e.Id + " " + reason + " live=" + world.Ids.IsLive(e.Id));

            bool result = false;
            world.InStep(SimulationPhase.Lifecycle, () => result = world.Lifecycle.Despawn(id, DespawnReason.Consumed));

            Assert.IsTrue(result);
            CollectionAssert.AreEqual(new[]
            {
                "Despawning 1:1",
                "collisions.Unregister",
                "physics.Unregister",
                "ticks.Unregister",
                "Despawned 1:1 Consumed live=True",
                "OnReleased live=True",
                "pool.Release",
            }, world.Log);
            Assert.IsFalse(world.Ids.IsLive(id), "id freed before pool.Release");
            Assert.IsFalse(entity.gameObject.activeSelf);
            Assert.AreEqual(EntityHandle.None, entity.Id);
            Assert.IsFalse(world.Lifecycle.TryGet(id, out _));
            Assert.AreEqual(0, world.Lifecycle.ActiveCount);
        }

        [Test]
        public void Despawn_NotLive_ReturnsFalseWithoutException()
        {
            var world = new LoopPmWorld();
            LoopPmTickEntity entity = null;
            world.InStep(SimulationPhase.Lifecycle, () => entity = world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            EntityHandle id = entity.Id;
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                Assert.IsTrue(world.Lifecycle.Despawn(id, DespawnReason.Killed));
                Assert.IsFalse(world.Lifecycle.Despawn(id, DespawnReason.Killed));
                Assert.IsFalse(world.Lifecycle.Despawn(new EntityHandle(99, 1), DespawnReason.Killed));
                Assert.IsFalse(world.Lifecycle.Despawn(EntityHandle.None, DespawnReason.Killed));
            });
        }

        [TestCase(SimulationPhase.Begin)]
        [TestCase(SimulationPhase.WeaponTick)]
        [TestCase(SimulationPhase.Resolve)]
        [TestCase(SimulationPhase.End)]
        public void SpawnAndDespawn_OutsideLifecycle_Throw(SimulationPhase phase)
        {
            var world = new LoopPmWorld();
            LoopPmTickEntity entity = null;
            world.InStep(SimulationPhase.Lifecycle, () => entity = world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            world.InStep(phase, () =>
            {
                Assert.Throws<InvalidOperationException>(() => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
                Assert.Throws<InvalidOperationException>(() => world.Lifecycle.Despawn(entity.Id, DespawnReason.Killed));
            });
            Assert.AreEqual(1, world.Lifecycle.ActiveCount);
        }

        [Test]
        public void SpawnAndDespawn_OutsideAnyStep_Throw()
        {
            var world = new LoopPmWorld();
            Assert.Throws<InvalidOperationException>(() => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            Assert.Throws<InvalidOperationException>(() => world.Lifecycle.Despawn(new EntityHandle(1, 1), DespawnReason.Killed));
            Assert.AreEqual(0, world.Pool.ActiveCount);
        }

        [Test]
        public void Lifetime_ZeroMeansNone_PositiveAddsToSpawnStep_NegativeThrows()
        {
            var world = new LoopPmWorld();
            world.AdvanceSteps(10);
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                Assert.AreEqual(long.MaxValue, world.SpawnTick(EntityCategory.PlayerOrb, Vector2.zero, 0).ExpireStep);
                Assert.AreEqual(17, world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, 7).ExpireStep);
                Assert.Throws<ArgumentOutOfRangeException>(() => world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, -1));
            });
            Assert.AreEqual(2, world.Pool.ActiveCount, "a rejected lifetime acquires nothing");
        }

        [Test]
        public void NonUpdatableEntity_IsNotRegisteredForTicks()
        {
            var world = new LoopPmWorld();
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                LoopPmPlainEntity gem = world.Lifecycle.Spawn<LoopPmPlainEntity>(EntityCategory.Gem, Vector2.one, 0, null);
                Assert.AreEqual(CollisionLayer.Gem, gem.Body.Layer);
                world.Lifecycle.Despawn(gem.Id, DespawnReason.Consumed);
                Assert.AreEqual(1, gem.ReleaseCount);
            });
            CollectionAssert.DoesNotContain(world.Log, "ticks.Register");
            CollectionAssert.DoesNotContain(world.Log, "ticks.Unregister");
        }

        [Test]
        public void Spawn_WrongComponentType_Throws()
        {
            var world = new LoopPmWorld();
            world.InStep(SimulationPhase.Lifecycle, () =>
                Assert.Throws<InvalidOperationException>(() => world.Lifecycle.Spawn<LoopPmPlainEntity>(EntityCategory.Monster, Vector2.zero, 0, null)));
            Assert.AreEqual(0, world.Pool.ActiveCount);
            Assert.AreEqual(0, world.Ids.LiveCount);
        }

        [Test]
        public void ReusedIndex_GetsNextGeneration()
        {
            var world = new LoopPmWorld();
            EntityHandle first = default;
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                first = world.SpawnTick(EntityCategory.Monster, Vector2.zero).Id;
                world.Lifecycle.Despawn(first, DespawnReason.Killed);
            });
            EntityHandle second = default;
            world.InStep(SimulationPhase.Lifecycle, () => second = world.SpawnTick(EntityCategory.Monster, Vector2.zero).Id);
            Assert.AreEqual(first.Index, second.Index);
            Assert.AreEqual(first.Generation + 1, second.Generation);
        }

        [Test]
        public void DespawnAll_OutsideStep_DespawnsInAscendingIdOrder()
        {
            var world = new LoopPmWorld();
            var spawned = new List<EntityHandle>();
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    spawned.Add(world.SpawnTick(EntityCategory.Monster, Vector2.zero).Id);
                }

                // Free 2 and 4 so the next spawns reuse low indices (LIFO) and the active list is not in Id order.
                world.Lifecycle.Despawn(spawned[1], DespawnReason.Killed);
                world.Lifecycle.Despawn(spawned[3], DespawnReason.Killed);
                world.SpawnTick(EntityCategory.Monster, Vector2.zero);
                world.SpawnTick(EntityCategory.Monster, Vector2.zero);
            });

            var order = new List<string>();
            world.Lifecycle.Despawned += (e, reason) => order.Add(e.Id + " " + reason);
            world.Lifecycle.DespawnAll(DespawnReason.RunReset);

            CollectionAssert.AreEqual(new[] { "1:1 RunReset", "2:2 RunReset", "3:1 RunReset", "4:2 RunReset", "5:1 RunReset" }, order);
            Assert.AreEqual(0, world.Lifecycle.ActiveCount);
            Assert.AreEqual(0, world.Pool.ActiveCount);
            Assert.AreEqual(0, world.Ids.LiveCount);
            Assert.Throws<InvalidOperationException>(() => world.SpawnTick(EntityCategory.Monster, Vector2.zero),
                "the run-reset allowance ends with DespawnAll");
        }

        [Test]
        public void DespawnAll_InsideStep_Throws()
        {
            var world = new LoopPmWorld();
            world.InStep(SimulationPhase.Lifecycle, () =>
                Assert.Throws<InvalidOperationException>(() => world.Lifecycle.DespawnAll(DespawnReason.RunReset)));
        }

        [Test]
        public void ReplaceStrategies_WithLiveEntities_Throws()
        {
            var world = new LoopPmWorld();
            world.InStep(SimulationPhase.Lifecycle, () => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            Assert.Throws<InvalidOperationException>(() =>
                world.Lifecycle.ReplaceStrategies(new LoopPmPool(null), new LoopPmTicks(null), new LoopPmCollisions(null)));
        }

        [Test]
        public void ReplaceStrategies_NewPool_ClearsOldPool_AndNewStrategiesAreUsed()
        {
            var world = new LoopPmWorld();
            world.InStep(SimulationPhase.Lifecycle, () => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            world.Lifecycle.DespawnAll(DespawnReason.RunReset);

            var log = new List<string>();
            var pool = new LoopPmPool(log, "pool2", PoolStrategyKind.UnityObjectPool);
            var ticks = new LoopPmTicks(log, TickStrategyKind.PerObjectFixedUpdate);
            var collisions = new LoopPmCollisions(log, CollisionStrategyKind.EngineCollider2D);
            world.Lifecycle.ReplaceStrategies(pool, ticks, collisions);

            Assert.AreEqual(1, world.Pool.ClearCount, "outgoing pool cleared once");
            Assert.AreEqual(0, pool.ClearCount);
            Assert.AreSame(pool, world.Lifecycle.Pool);
            Assert.AreSame(ticks, world.Lifecycle.Ticks);
            Assert.AreSame(collisions, world.Lifecycle.Collisions);

            world.InStep(SimulationPhase.Lifecycle, () => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            CollectionAssert.AreEqual(new[] { "pool2.Acquire", "ticks.Register", "collisions.Register" }, log);
        }

        [Test]
        public void ReplaceStrategies_SamePool_DoesNotClear()
        {
            var world = new LoopPmWorld();
            world.Lifecycle.ReplaceStrategies(world.Pool, new LoopPmTicks(null), new LoopPmCollisions(null));
            Assert.AreEqual(0, world.Pool.ClearCount);
            Assert.Throws<ArgumentNullException>(() => world.Lifecycle.ReplaceStrategies(null, world.Ticks, world.Collisions));
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            var world = new LoopPmWorld();
            Assert.Throws<ArgumentNullException>(() =>
                new EntityLifecycle(world.Pool, world.Ids, world.Ticks, world.Collisions, world.Physics, world.Catalog, null));
            Assert.Throws<ArgumentNullException>(() =>
                new EntityLifecycle(world.Pool, world.Ids, world.Ticks, world.Collisions, null, world.Catalog, world.Clock));
        }
    }
}
