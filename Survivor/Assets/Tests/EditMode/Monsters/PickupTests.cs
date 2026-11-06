using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Gem attraction and gem / chest pickup through the collision router (TAC-F4-3, F4-4 ordering, F4-8; D16; 5.10 consumed rule).
    public sealed class PickupTests
    {
        private const float Eps = 1e-4f;
        private MonEmRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [Test]
        public void MagnetEnter_StartsAttraction_GemMovesAtAttractSpeed_LandsOnPlayer_ExitIgnored()
        {
            _rig = new MonEmRig();
            ExperienceGem gem = SpawnGem(new Vector2(1f, 0f), 1);
            float speed = 10f / 60f;

            _rig.AddEvent(CollisionChannel.PlayerMagnetVsGem, gem.Id);
            _rig.Step();
            Assert.IsTrue(gem.IsAttracted);
            Assert.AreEqual(1f, gem.Position.x, Eps, "attraction starts in Resolve; the first move is the next WeaponTick");
            Assert.AreEqual(1, _rig.Pickups.AttractedGemCount);

            _rig.AddEvent(CollisionChannel.PlayerMagnetVsGem, gem.Id, CollisionEventKind.Exit);
            _rig.Step();
            Assert.IsTrue(gem.IsAttracted, "Exit does not revert attraction");
            Assert.AreEqual(1f - speed, gem.Position.x, Eps, "moved GemAttractSpeedPerStep toward the player");

            _rig.Step(10);
            Assert.AreEqual(_rig.Player, gem.Position, "lands exactly on the player instead of overshooting");
            Assert.AreEqual(1, _rig.PickupCounts.ActiveGemCount, "still a gem until the body touches it");
        }

        [Test]
        public void BodyEnter_CollectsGemOnce_AddXp_ThenDespawnConsumed()
        {
            _rig = new MonEmRig();
            ExperienceGem gem = SpawnGem(new Vector2(0.1f, 0f), 3);
            EntityHandle id = gem.Id;

            _rig.AddEvent(CollisionChannel.PlayerMagnetVsGem, id);
            _rig.AddEvent(CollisionChannel.PlayerBodyVsGem, id);
            _rig.AddEvent(CollisionChannel.PlayerBodyVsGem, id);
            _rig.Step();

            CollectionAssert.AreEqual(new[] { 3 }, _rig.XpLog, "XpValue granted exactly once in the step it was touched");
            Assert.IsTrue(_rig.DespawnedLog.Contains((id, DespawnReason.Consumed)));
            Assert.AreEqual(0, _rig.PickupCounts.ActiveGemCount);
            Assert.AreEqual(0, _rig.Pickups.AttractedGemCount, "the attracted list forgets despawned gems");

            _rig.AddEvent(CollisionChannel.PlayerBodyVsGem, id);
            _rig.Step();
            Assert.AreEqual(1, _rig.XpLog.Count, "a stale id is ignored");
        }

        [Test]
        public void ChestEnter_OpensEachChestOnceInEventOrder_WithChestStream()
        {
            _rig = new MonEmRig();
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None, position: new Vector2(0.5f, 0f)));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None, position: new Vector2(-0.5f, 0f)));
            _rig.Step();
            List<Chest> chests = _rig.Live<Chest>();
            Assert.AreEqual(2, chests.Count);
            Assert.AreEqual(0.5f, chests[0].Body.Radius, "chestRadius from playerStats.json");
            Assert.AreEqual(CollisionLayer.Chest, chests[0].Body.Layer);
            EntityHandle first = chests[0].Id;
            EntityHandle second = chests[1].Id;
            long step = _rig.Clock.Step;

            _rig.AddEvent(CollisionChannel.PlayerBodyVsChest, second);
            _rig.AddEvent(CollisionChannel.PlayerBodyVsChest, first);
            _rig.AddEvent(CollisionChannel.PlayerBodyVsChest, first);
            _rig.Step();

            CollectionAssert.AreEqual(new[] { step, step }, _rig.ChestLog, "TAC-F4-4: two chests in one step, one Resolve call each");
            Assert.AreSame(_rig.Rng.Get(RngStream.Chest), _rig.ChestRngLog[0], "openChest receives the Chest stream");
            Assert.IsTrue(_rig.DespawnedLog.Contains((first, DespawnReason.Consumed)));
            Assert.IsTrue(_rig.DespawnedLog.Contains((second, DespawnReason.Consumed)));
            Assert.AreEqual(0, _rig.PickupCounts.ActiveChestCount);
        }

        [Test]
        public void Events_OnWrongEntityKind_AreIgnored_WrongChannelThrows()
        {
            _rig = new MonEmRig();
            ExperienceGem gem = SpawnGem(new Vector2(3f, 0f), 1);
            _rig.AddEvent(CollisionChannel.PlayerBodyVsChest, gem.Id);
            _rig.Step();
            Assert.AreEqual(0, _rig.ChestLog.Count, "a gem id on the chest channel opens nothing");
            Assert.AreEqual(1, _rig.PickupCounts.ActiveGemCount);

            var e = new CollisionEvent(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, new EntityHandle(1, 1), gem.Id);
            Assert.Throws<InvalidOperationException>(() => _rig.Pickups.Handle(e, _rig.Context));
        }

        [Test]
        public void KilledMonsterGem_FlowsIntoPickup()
        {
            _rig = new MonEmRig();
            Monster m = _rig.SpawnMonster("M1", new Vector2(2f, 0f));
            _rig.DamageNextStep(10f, m);
            _rig.Step();
            ExperienceGem gem = _rig.Live<ExperienceGem>()[0];
            Assert.AreEqual(1, _rig.PickupCounts.ActiveGemCount, "TAC-F4-8: ActiveGemCount");

            _rig.AddEvent(CollisionChannel.PlayerBodyVsGem, gem.Id);
            _rig.Step();
            CollectionAssert.AreEqual(new[] { 1 }, _rig.XpLog);
            Assert.AreEqual(0, _rig.PickupCounts.ActiveGemCount);
        }

        [Test]
        public void Clear_ForgetsAttractedGems_AndNullDelegatesThrow()
        {
            _rig = new MonEmRig();
            ExperienceGem gem = SpawnGem(new Vector2(3f, 0f), 1);
            _rig.AddEvent(CollisionChannel.PlayerMagnetVsGem, gem.Id);
            _rig.Step();
            _rig.Pickups.Clear();
            Assert.AreEqual(0, _rig.Pickups.AttractedGemCount);

            Assert.Throws<ArgumentNullException>(() => new PickupSystem(_rig.Lifecycle, _rig.Data, _rig.Rng, null, (r, c) => { }));
            Assert.Throws<ArgumentNullException>(() => new PickupSystem(_rig.Lifecycle, _rig.Data, _rig.Rng, x => { }, null));
            Assert.Throws<ArgumentNullException>(() => new PickupRegistry(null));
        }

        private ExperienceGem SpawnGem(Vector2 position, int xp)
        {
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None, position: position, count: xp));
            _rig.Step();
            List<ExperienceGem> gems = _rig.Live<ExperienceGem>();
            return gems[gems.Count - 1];
        }
    }
}
