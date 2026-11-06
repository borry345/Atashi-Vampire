using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    /// LifetimeSystem (Lifecycle 10) and MonsterRegistry (BuildIndex 0) over a recording lifecycle.
    public sealed class LifetimeAndRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            LoopPmWorld.DestroyTestObjects();
        }

        private static void RunLifetime(LoopPmWorld world, LifetimeSystem system)
        {
            world.InStep(SimulationPhase.Lifecycle, () => system.OnPhase(SimulationPhase.Lifecycle, world.Context()));
        }

        [Test]
        public void Lifetime_DespawnsAtExpireStep_SkipsNoLifetime()
        {
            var world = new LoopPmWorld();
            var system = new LifetimeSystem(world.Lifecycle);
            LoopPmTickEntity shortLived = null;
            LoopPmTickEntity orb = null;
            world.AdvanceSteps(2);
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                shortLived = world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, 3);
                orb = world.SpawnTick(EntityCategory.PlayerOrb, Vector2.zero, 0);
            });
            EntityHandle shortId = shortLived.Id;
            var reasons = new List<string>();
            world.Lifecycle.Despawned += (e, r) => reasons.Add(e.Id + " " + r + " @" + world.Clock.Step);

            RunLifetime(world, system); // step 3
            RunLifetime(world, system); // step 4
            Assert.IsTrue(world.Ids.IsLive(shortId), "alive through step SpawnStep + lifetime - 1");
            RunLifetime(world, system); // step 5 == 2 + 3
            Assert.IsFalse(world.Ids.IsLive(shortId));
            CollectionAssert.AreEqual(new[] { "1:1 LifetimeExpired @5" }, reasons);

            for (int i = 0; i < 20; i++)
            {
                RunLifetime(world, system);
            }

            Assert.IsTrue(world.Ids.IsLive(orb.Id), "ExpireStep = long.MaxValue is skipped");
        }

        [Test]
        public void Lifetime_SeveralExpired_DespawnedInAscendingId()
        {
            var world = new LoopPmWorld();
            var system = new LifetimeSystem(world.Lifecycle);
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                var ids = new List<EntityHandle>();
                for (int i = 0; i < 4; i++)
                {
                    ids.Add(world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, 1).Id);
                }

                world.Lifecycle.Despawn(ids[0], DespawnReason.Consumed);
                world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero, 1); // reuses index 1 at generation 2
            });
            var order = new List<string>();
            world.Lifecycle.Despawned += (e, r) => order.Add(e.Id.ToString());
            RunLifetime(world, system);
            CollectionAssert.AreEqual(new[] { "1:2", "2:1", "3:1", "4:1" }, order);
        }

        [Test]
        public void Registry_SnapshotHoldsMonstersOnly_InAscendingId_WithR25SizeClass()
        {
            var world = new LoopPmWorld();
            var snapshot = new MonsterSnapshotBuffer();
            var registry = new MonsterRegistry(world.Lifecycle, snapshot);
            var monsters = new List<LoopPmTickEntity>();
            world.InStep(SimulationPhase.Lifecycle, () =>
            {
                for (int i = 0; i < 4; i++)
                {
                    LoopPmTickEntity m = world.SpawnTick(EntityCategory.Monster, new Vector2(i, -i));
                    m.Body.Radius = 0.5f;
                    monsters.Add(m);
                }

                world.SpawnTick(EntityCategory.PlayerProjectile, Vector2.zero); // 5:1, not a monster
                world.Lifecycle.Despawn(monsters[0].Id, DespawnReason.Killed);
                world.Lifecycle.Despawn(monsters[2].Id, DespawnReason.Killed);
                LoopPmTickEntity big = world.SpawnTick(EntityCategory.Monster, new Vector2(9f, 9f)); // reuses 3 -> 3:2
                big.Body.Radius = 1.5f;
                LoopPmTickEntity boss = world.SpawnTick(EntityCategory.Monster, new Vector2(7f, 7f)); // reuses 1 -> 1:2
                boss.Body.Radius = 0.8f;
                boss.Body.IsLarge = true;
            });

            registry.OnPhase(SimulationPhase.BuildIndex, world.Context());

            Assert.AreEqual(4, registry.Count);
            Assert.AreEqual(4, snapshot.Count);
            var rows = new List<string>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                MonsterSnapshot s = snapshot[i];
                rows.Add(s.Id + " " + s.Position + " r=" + s.Radius + " large=" + s.IsLarge);
            }

            CollectionAssert.AreEqual(new[]
            {
                "1:2 (7.00, 7.00) r=0.8 large=True",
                "2:1 (1.00, -1.00) r=0.5 large=False",
                "3:2 (9.00, 9.00) r=1.5 large=True",
                "4:1 (3.00, -3.00) r=0.5 large=False",
            }, rows);
        }

        [Test]
        public void Registry_OtherPhases_LeaveSnapshotAlone()
        {
            var world = new LoopPmWorld();
            var snapshot = new MonsterSnapshotBuffer();
            var registry = new MonsterRegistry(world.Lifecycle, snapshot);
            world.InStep(SimulationPhase.Lifecycle, () => world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            registry.OnPhase(SimulationPhase.WeaponTick, world.Context());
            Assert.AreEqual(0, snapshot.Count);
            registry.OnPhase(SimulationPhase.BuildIndex, world.Context());
            Assert.AreEqual(1, snapshot.Count);
            world.Lifecycle.DespawnAll(DespawnReason.RunReset);
            registry.OnPhase(SimulationPhase.BuildIndex, world.Context());
            Assert.AreEqual(0, snapshot.Count, "rebuilt from scratch each BuildIndex");
            Assert.AreEqual(0, registry.Count);
        }
    }
}
