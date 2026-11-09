using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Monsters;
using UnityEngine;

namespace Survivor.Tests.EditMode.Progression
{
    /// SimulationStateHash (5.16): fixed input order, sorted entities, weapon NextFireStep, seed sensitivity.
    public sealed class SimulationStateHashTests
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private readonly List<FlowEmRig> _rigs = new List<FlowEmRig>();

        [TearDown]
        public void TearDown()
        {
            foreach (FlowEmRig rig in _rigs)
            {
                rig.Dispose();
            }

            _rigs.Clear();
        }

        private FlowEmRig NewRig(ulong seed = FlowEmRig.DefaultSeed, bool quiet = false)
        {
            var rig = new FlowEmRig(start: false) { Seed = seed };
            if (quiet)
            {
                rig.PolicyFactory = () => new MonEmNoSpawnPolicy();
                rig.Rules.WeaponsEnabled = false;
                rig.Input.Still = true;
            }

            rig.Rules.PlayerTakesDamage = false;
            rig.StartRun();
            _rigs.Add(rig);
            return rig;
        }

        // Independent re-implementation of the documented order: step; player x, y, hp, xp, level; entities by id
        // (index, generation, category, x, y, monster hp); kill count; occupied slots (slot, NextFireStep).
        private static ulong Reference(FlowEmRig rig)
        {
            var bytes = new List<byte>();
            void Int(int v) => bytes.AddRange(BitConverter.GetBytes(v).Take(4));
            void Long(long v) => bytes.AddRange(BitConverter.GetBytes(v));
            void Float(float v) => bytes.AddRange(BitConverter.GetBytes(v));
            Assert.IsTrue(BitConverter.IsLittleEndian);

            Long(rig.Clock.Step);
            Float(rig.Player.Position.x);
            Float(rig.Player.Position.y);
            Float(rig.Health.Hp);
            Int(rig.Experience.Xp);
            Int(rig.Experience.Level);
            foreach (SimEntity e in rig.Lifecycle.ActiveEntities.OrderBy(e => e.Id))
            {
                Int(e.Id.Index);
                Int(e.Id.Generation);
                Int((int)e.Category);
                Float(e.Position.x);
                Float(e.Position.y);
                if (e is Monster m)
                {
                    Float(m.Hp);
                }
            }

            Int(rig.Kills.KillCount);
            foreach (WeaponInstance w in rig.Weapons.Slots.Where(w => w != null))
            {
                Int(w.SlotIndex);
                Long(w.NextFireStep);
            }

            ulong hash = OffsetBasis;
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= Prime;
            }

            return hash;
        }

        [Test]
        public void Hash_FollowsTheDocumentedInputOrder()
        {
            FlowEmRig rig = NewRig();
            rig.Play(400);
            Assert.Greater(rig.Live<Monster>().Count, 0, "monsters are part of the input");
            Assert.AreEqual(Reference(rig), rig.Hash);
            Assert.Throws<ArgumentNullException>(() => SimulationStateHash.Compute(null));
        }

        [Test]
        public void SameSeedSameSteps_SameHash_DifferentSeed_DifferentHash()
        {
            FlowEmRig a = NewRig();
            FlowEmRig b = NewRig();
            FlowEmRig c = NewRig(FlowEmRig.DefaultSeed + 17);
            List<ulong> ha = a.Play(400);
            List<ulong> hb = b.Play(400);
            List<ulong> hc = c.Play(400);
            CollectionAssert.AreEqual(ha, hb);
            Assert.AreNotEqual(ha[ha.Count - 1], hc[hc.Count - 1]);
        }

        [Test]
        public void WeaponState_ChangesTheHash()
        {
            FlowEmRig a = NewRig(quiet: true);
            FlowEmRig b = NewRig(quiet: true);
            a.Step();
            b.Step();
            Assert.AreEqual(a.Hash, b.Hash);

            WeaponInstance weapon = b.Weapons.Slots[0];
            long saved = weapon.NextFireStep;
            weapon.NextFireStep = saved + 1;
            Assert.AreNotEqual(a.Hash, b.Hash, "NextFireStep is hashed");
            weapon.NextFireStep = saved;
            Assert.AreEqual(a.Hash, b.Hash);

            b.Enqueue(PendingCommand.SetLoadout(new List<WeaponLoadoutEntry> { new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W3", 1) }));
            a.Step();
            b.Step();
            Assert.AreEqual(a.Lifecycle.ActiveCount, b.Lifecycle.ActiveCount, "weapons are off: no child entity differs");
            Assert.AreNotEqual(a.Hash, b.Hash, "a different loadout gives a different hash");
        }

        [Test]
        public void PlayerAndProgress_ChangeTheHash()
        {
            FlowEmRig a = NewRig(quiet: true);
            FlowEmRig b = NewRig(quiet: true);
            b.Enqueue(PendingCommand.AddXp(1));
            a.Step();
            b.Step();
            Assert.AreNotEqual(a.Hash, b.Hash, "xp");
            a.Enqueue(PendingCommand.AddXp(1));
            b.Enqueue(PendingCommand.SetHealth(99f));
            a.Step();
            b.Step();
            Assert.AreNotEqual(a.Hash, b.Hash, "hp");
        }

        [Test]
        public void EntityListOrder_DoesNotChangeTheHash()
        {
            FlowEmRig a = NewRig(quiet: true);
            FlowEmRig b = NewRig(quiet: true);
            var positions = new[] { new Vector2(10f, 0f), new Vector2(0f, 10f), new Vector2(-10f, 0f), new Vector2(0f, -10f), new Vector2(7f, 7f) };
            foreach (FlowEmRig rig in new[] { a, b })
            {
                foreach (Vector2 p in positions)
                {
                    rig.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None, position: p, count: 1));
                }

                rig.Step();
            }

            List<ExperienceGem> gemsA = a.Live<ExperienceGem>();
            List<ExperienceGem> gemsB = b.Live<ExperienceGem>();
            Assert.AreEqual(5, gemsA.Count);
            CollectionAssert.AreEqual(gemsA.Select(g => g.Id), gemsB.Select(g => g.Id));

            // a removes gem #2 then gem #3; b removes #3 then #2 -> same set, different swap-remove order of ActiveEntities.
            DespawnNextStep(a, gemsA[1].Id);
            DespawnNextStep(b, gemsB[2].Id);
            DespawnNextStep(a, gemsA[2].Id);
            DespawnNextStep(b, gemsB[1].Id);

            List<EntityHandle> orderA = a.Lifecycle.ActiveEntities.Select(e => e.Id).ToList();
            List<EntityHandle> orderB = b.Lifecycle.ActiveEntities.Select(e => e.Id).ToList();
            CollectionAssert.AreEquivalent(orderA, orderB);
            CollectionAssert.AreNotEqual(orderA, orderB, "precondition: the live lists are in different orders");
            Assert.AreEqual(a.Hash, b.Hash);
        }

        private static void DespawnNextStep(FlowEmRig rig, EntityHandle id)
        {
            rig.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, EntityHandle.None, target: id, reason: DespawnReason.Consumed));
            rig.Step();
        }
    }
}
