using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // F7 (TAC-F7-1..3) and chest effects (TAC-F4-4..6, 5c.5 "two chests in one step").
    public sealed class EvolutionChestTests
    {
        private static readonly EntityHandle ChestA = new EntityHandle(900, 1);
        private static readonly EntityHandle ChestB = new EntityHandle(901, 1);
        private PlrRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
        }

        // Closes the gate once and applies the level-up options, then reopens.
        private static void LevelUp(PlrRig rig, params UpgradeOption[] options)
        {
            rig.StepAndCloseForLevelUp();
            foreach (UpgradeOption option in options)
            {
                rig.Applier.ApplyLevelUpOption(option);
            }

            rig.Clock.Open();
        }

        private static UpgradeOption Up(string weaponId) => new UpgradeOption(UpgradeKind.WeaponUpgrade, weaponId);

        private void Chest(long step, EntityHandle chest)
        {
            _rig.Collisions.Add(step, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsChest, _rig.PlayerId, chest);
        }

        [Test]
        public void TACF7_1_EligibilityNeedsMaxLevelAndOwnedPassiveInSlotOrder()
        {
            _rig = new PlrRig();
            Assert.IsFalse(_rig.Evolution.TryFindFirstEligible(out int slot, out CombinationRule rule));
            Assert.AreEqual(-1, slot);
            Assert.IsNull(rule);
            WeaponTests.QueueLoadout(_rig, ("W2", 5), ("W1", 4));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            Assert.IsFalse(_rig.Evolution.TryFindFirstEligible(out slot, out rule), "W1 at 4 with P1, W2 at 5 without P3");
            LevelUp(_rig, Up("W1"));
            Assert.IsTrue(_rig.Evolution.TryFindFirstEligible(out slot, out rule));
            Assert.AreEqual(1, slot);
            Assert.AreEqual("E1", rule.ResultWeaponId);
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            Assert.IsTrue(_rig.Evolution.TryFindFirstEligible(out slot, out rule));
            Assert.AreEqual(0, slot, "two eligible weapons: the smaller slot first (Q42)");
            Assert.AreEqual("E2", rule.ResultWeaponId);
        }

        [Test]
        public void TACF7_3_NoCombinationFileMeansNoEvolution()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.Combinations, null));
            WeaponTests.QueueLoadout(_rig, ("W1", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            Assert.AreEqual(0, _rig.Data.Combinations.Count);
            Assert.IsFalse(_rig.Evolution.TryFindFirstEligible(out _, out _));
        }

        [Test]
        public void TACF7_2_W2ToE2ReplacesInPlaceAndLeavesExactlyOneOrblessStep()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 1), ("W2", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            _rig.Steps(2);
            List<EntityHandle> originals = _rig.Live<OrbitOrb>().ConvertAll(o => o.Id);
            Assert.AreEqual(3, originals.Count);
            long s = _rig.Clock.Step;
            Chest(s, ChestA);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Evolved, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual("E2", _rig.Weapons.Slots[1].Id);
            Assert.AreEqual(1, _rig.Weapons.Slots[1].Level);
            Assert.AreEqual(s, _rig.Weapons.Slots[1].NextFireStep, "first-ownership rule: the replacement step");
            Assert.AreEqual(2, _rig.Weapons.Count, "slot count unchanged");
            CollectionAssert.AreEqual(new[] { "W2" }, _rig.Weapons.ConsumedByEvolution);
            List<SimulationCommand> despawns = _rig.Commands.At(s, SimulationCommandKind.Despawn);
            Assert.AreEqual(3, despawns.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(DespawnReason.Replaced, despawns[i].Reason);
                Assert.AreEqual(_rig.PlayerId, despawns[i].Source);
                Assert.AreEqual(originals[i], despawns[i].Target, "ascending id");
            }

            Assert.AreEqual(0, _rig.Live<OrbitOrb>().Count, "originals recycled in step s");
            _rig.Step();
            Assert.AreEqual(4, _rig.Commands.At(s + 1, SimulationCommandKind.SpawnPlayerOrb).Count);
            List<EntityHandle> evolved = _rig.Live<OrbitOrb>().ConvertAll(o => o.Id);
            Assert.AreEqual(4, evolved.Count);
            _rig.Step();
            var orbLayerAtCollect = new Dictionary<long, int>();
            for (long step = s; step <= s + 2; step++)
            {
                int count = 0;
                foreach (EntityHandle id in _rig.Collisions.RegisteredAtCollect[step])
                {
                    if (originals.Contains(id) || evolved.Contains(id))
                    {
                        count++;
                    }
                }

                orbLayerAtCollect[step] = count;
            }

            Assert.AreEqual(3, orbLayerAtCollect[s], "W2 orbs still collide in step s");
            Assert.AreEqual(0, orbLayerAtCollect[s + 1], "the single orbless step");
            Assert.AreEqual(4, orbLayerAtCollect[s + 2], "E2 orbs from s + 2");
        }

        [Test]
        public void TACF7_2_E1FiresOnTheStepAfterTheReplacement()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(6f, 0f));
            WeaponTests.QueueLoadout(_rig, ("W1", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            _rig.Steps(5);
            long s = _rig.Clock.Step;
            Chest(s, ChestA);
            _rig.Step();
            Assert.AreEqual("E1", _rig.Weapons.Slots[0].Id);
            _rig.Step();
            List<SimulationCommand> shots = _rig.Commands.At(s + 1, SimulationCommandKind.SpawnPlayerProjectile);
            Assert.AreEqual(3, shots.Count, "E1 fires 3 projectiles at once");
            Assert.AreEqual("E1", shots[0].DefinitionId);
        }

        [Test]
        public void TACF4_4_EvolutionDrawsNoRandomNumber()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            var twin = new RngStreams(20261107UL).Get(RngStream.Chest);
            Chest(_rig.Clock.Step, ChestA);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Evolved, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual(twin.NextUInt(), _rig.ChestRng.NextUInt(), "the Chest stream was not advanced");
        }

        [Test]
        public void TACF4_4_UpgradeOrHealIsSeventyThirtyWithinThreePoints()
        {
            _rig = new PlrRig();
            var rng = new RngStreams(42UL).Get(RngStream.Chest);
            WeaponInstance w1 = _rig.Weapons.Slots[0];
            int upgrades = 0;
            for (int i = 0; i < 1000; i++)
            {
                ChestEffect effect = _rig.Chests.Resolve(rng, _rig.Context());
                Assert.AreNotEqual(ChestEffect.Evolved, effect);
                if (effect == ChestEffect.WeaponUpgraded)
                {
                    upgrades++;
                    w1.SetLevel(1, 0f);
                }
            }

            Assert.AreEqual(0.70, upgrades / 1000.0, 0.03);
        }

        [Test]
        public void TACF4_4_NoUpgradableWeaponTurnsUpgradeIntoHeal()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5), ("W3", 5));
            _rig.Step();
            var rng = new RngStreams(7UL).Get(RngStream.Chest);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(rng, _rig.Context()));
            }

            Assert.AreEqual(5, _rig.Weapons.Slots[0].Level);
        }

        [Test]
        public void TACF4_4_TwoChestsInOneStepResolveInEventOrder()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5), ("W2", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"), new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            long s = _rig.Clock.Step;
            Chest(s, ChestB);
            Chest(s, ChestA);
            _rig.Step();
            CollectionAssert.AreEqual(new[] { ChestEffect.Evolved, ChestEffect.Evolved }, _rig.ChestProbe.Effects);
            Assert.AreEqual("E1", _rig.Weapons.Slots[0].Id, "first chest evolves slot 0");
            Assert.AreEqual("E2", _rig.Weapons.Slots[1].Id, "second chest sees the first result and evolves slot 1");

            _rig.Destroy();
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            s = _rig.Clock.Step;
            Chest(s, ChestA);
            Chest(s, ChestB);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Evolved, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual(ChestEffect.Healed, _rig.ChestProbe.Effects[1], "E1 is not upgradable, so the 70/30 path heals");
        }

        [Test]
        public void TACF4_5_HealIsThirtyPercentOfMaxCappedAtMax()
        {
            _rig = new PlrRig();
            _rig.Applier.SetHealth(50f);
            _rig.Applier.Heal();
            Assert.AreEqual(80f, _rig.Health.Hp, 1e-5f);
            _rig.Applier.Heal();
            Assert.AreEqual(100f, _rig.Health.Hp);
        }

        [Test]
        public void TACF4_6_ChestHealComesBeforePlayerDamageInTheSameStep()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5), ("W3", 5));
            PlrMonster monster = _rig.AddMonster(new Vector2(50f, 0f), contactDamage: 15f);
            _rig.Step();
            _rig.Applier.SetHealth(10f);
            long s = _rig.Clock.Step;
            Chest(s, ChestA);
            _rig.Collisions.Add(s, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, monster.Id);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Healed, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual(10f + 30f - 15f, _rig.Health.Hp, 1e-5f, "heal (Resolve 0) then damage (Resolve 20)");
        }
    }
}
