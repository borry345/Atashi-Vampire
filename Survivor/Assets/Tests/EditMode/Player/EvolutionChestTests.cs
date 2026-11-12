using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Monsters;
using Survivor.Tests.EditMode.Progression;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // F7 (TAC-F7-1..3), chest effects (TAC-F4-4..6, 5c.5 "two chests in one step"), the chest result log (TAC-F4-9) and the
    // passive upgrade candidates (TAC-F4-10).
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
            _rig.Passives.Slots[0].Level = 5; // P1 at max: no passive upgrade candidate either (TAC-F4-10)
            s = _rig.Clock.Step;
            Chest(s, ChestA);
            Chest(s, ChestB);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Evolved, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual(ChestEffect.Healed, _rig.ChestProbe.Effects[1], "E1 and P1 (max) are not upgradable, so the 70/30 path heals");
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

        private static void AssertResult(ChestResult result, long sequence, long step, ChestEffect kind, string targetId, int fromLevel,
                                         int toLevel, float healAmount)
        {
            Assert.AreEqual(sequence, result.Sequence, "Sequence");
            Assert.AreEqual(step, result.Step, "Step");
            Assert.AreEqual(kind, result.Kind, "Kind");
            Assert.AreEqual(targetId, result.TargetId, "TargetId");
            Assert.AreEqual(fromLevel, result.FromLevel, "FromLevel");
            Assert.AreEqual(toLevel, result.ToLevel, "ToLevel");
            Assert.AreEqual(healAmount, result.HealAmount, 1e-5f, "HealAmount");
        }

        [Test]
        public void TACF4_9_EveryResolveRecordsOneResultWithItsValues()
        {
            _rig = new PlrRig();
            ChestResultLog log = _rig.Applier.ChestResults;
            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(0, log.LastSequence);

            // Evolution: (Evolved, result weapon id, 0 -> 1).
            WeaponTests.QueueLoadout(_rig, ("W1", 5));
            LevelUp(_rig, new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            _rig.Passives.Slots[0].Level = 5; // P1 at max: no passive upgrade candidate either (TAC-F4-10)
            long s = _rig.Clock.Step;
            Chest(s, ChestA);
            _rig.Step();
            Assert.AreEqual(ChestEffect.Evolved, _rig.ChestProbe.Effects[0]);
            Assert.AreEqual(1, log.Count);
            AssertResult(log[0], 1, s, ChestEffect.Evolved, "E1", 0, 1, 0f);

            // Heal (E1 is the only weapon and P1 is at max, so nothing is upgradable): HealAmount is the amount actually restored, 0 at full health.
            _rig.Applier.SetHealth(50f);
            StepContext ctx = _rig.Context();
            Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(_rig.ChestRng, ctx));
            AssertResult(log[1], 2, ctx.Step, ChestEffect.Healed, null, 0, 0, 30f);
            Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(_rig.ChestRng, ctx));
            AssertResult(log[2], 3, ctx.Step, ChestEffect.Healed, null, 0, 0, 20f);
            Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(_rig.ChestRng, ctx));
            AssertResult(log[3], 4, ctx.Step, ChestEffect.Healed, null, 0, 0, 0f);
            Assert.AreEqual(100f, _rig.Health.Hp);
            Assert.AreEqual(4, log.Count);
            Assert.AreEqual(4, log.LastSequence);

            // Upgrade or 30% heal with two upgradable weapons: the record names the weapon whose level actually changed.
            _rig.Destroy();
            _rig = new PlrRig();
            log = _rig.Applier.ChestResults;
            WeaponTests.QueueLoadout(_rig, ("W1", 1), ("W2", 1));
            _rig.Step();
            var rng = new RngStreams(42UL).Get(RngStream.Chest);
            int upgrades = 0;
            int heals = 0;
            for (int i = 0; i < 40; i++)
            {
                _rig.Applier.SetHealth(50f);
                int w1Before = _rig.Weapons.Slots[0].Level;
                int w2Before = _rig.Weapons.Slots[1].Level;
                ctx = _rig.Context();
                ChestEffect effect = _rig.Chests.Resolve(rng, ctx);
                Assert.AreEqual(Math.Min(i + 1, ChestResultLog.Capacity), log.Count);
                ChestResult newest = log[log.Count - 1];
                if (effect == ChestEffect.Healed)
                {
                    heals++;
                    AssertResult(newest, i + 1, ctx.Step, ChestEffect.Healed, null, 0, 0, 30f);
                    Assert.AreEqual(w1Before, _rig.Weapons.Slots[0].Level);
                    Assert.AreEqual(w2Before, _rig.Weapons.Slots[1].Level);
                    continue;
                }

                Assert.AreEqual(ChestEffect.WeaponUpgraded, effect);
                upgrades++;
                int slot = newest.TargetId == "W1" ? 0 : 1;
                Assert.AreEqual(slot == 0 ? "W1" : "W2", newest.TargetId);
                WeaponInstance upgraded = _rig.Weapons.Slots[slot];
                WeaponInstance other = _rig.Weapons.Slots[1 - slot];
                AssertResult(newest, i + 1, ctx.Step, ChestEffect.WeaponUpgraded, upgraded.Id, slot == 0 ? w1Before : w2Before,
                    upgraded.Level, 0f);
                Assert.AreEqual(newest.FromLevel + 1, newest.ToLevel);
                Assert.AreEqual(slot == 0 ? w2Before : w1Before, other.Level, "the other weapon is untouched");
                upgraded.SetLevel(1, 0f);
            }

            Assert.Greater(upgrades, 0);
            Assert.Greater(heals, 0);
        }

        // Applies a passive loadout directly (slot = list position).
        private static void SetPassives(PlrRig rig, params (string id, int level)[] entries)
        {
            var list = new List<PassiveLoadoutEntry>();
            foreach ((string id, int level) in entries)
            {
                list.Add(new PassiveLoadoutEntry(id, level));
            }

            rig.Applier.SetPassiveLoadout(list, rig.Context());
        }

        [Test]
        public void TACF4_10_OnlyAnUpgradablePassiveLeftMeansEveryUpgradeChestRaisesIt()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("E1", 1), ("W3", 5));
            _rig.Step();
            SetPassives(_rig, ("P1", 2));
            Assert.IsFalse(_rig.Evolution.TryFindFirstEligible(out _, out _));
            PassiveInstance p1 = _rig.Passives.Slots[0];
            ChestResultLog log = _rig.Applier.ChestResults;
            var rng = new RngStreams(11UL).Get(RngStream.Chest);
            var twin = new RngStreams(11UL).Get(RngStream.Chest);
            int upgrades = 0;
            int heals = 0;
            for (int i = 0; i < 200; i++)
            {
                StepContext ctx = _rig.Context();
                int changes = _rig.Applier.ChangeCount;
                ChestEffect effect = _rig.Chests.Resolve(rng, ctx);
                bool upgrade = twin.Chance(GameRules.ChestUpgradeChance);
                if (!upgrade)
                {
                    Assert.AreEqual(ChestEffect.Healed, effect);
                    Assert.AreEqual(2, p1.Level);
                    heals++;
                    continue;
                }

                // Twin replay of (3): one NextInt call over the single candidate.
                Assert.AreEqual(0, twin.NextInt(1));
                Assert.AreEqual(ChestEffect.PassiveUpgraded, effect, "Chance true with only P1 upgradable");
                Assert.AreEqual(3, p1.Level);
                Assert.AreEqual(changes + 1, _rig.Applier.ChangeCount);
                AssertResult(log[log.Count - 1], log.LastSequence, ctx.Step, ChestEffect.PassiveUpgraded, "P1", 2, 3, 0f);
                upgrades++;
                p1.Level = 2;
            }

            Assert.Greater(upgrades, 0);
            Assert.Greater(heals, 0);
            Assert.AreEqual(twin.NextUInt(), rng.NextUInt(), "same Chest stream consumption as the twin replay");
        }

        [Test]
        public void TACF4_10_EverythingAtMaxStillHeals()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W3", 5), ("W4", 5));
            _rig.Step();
            SetPassives(_rig, ("P2", 5), ("P1", 5));
            Assert.IsFalse(_rig.Evolution.TryFindFirstEligible(out _, out _), "P1 is owned but W1 is not");
            var rng = new RngStreams(7UL).Get(RngStream.Chest);
            var twin = new RngStreams(7UL).Get(RngStream.Chest);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(rng, _rig.Context()));
                twin.Chance(GameRules.ChestUpgradeChance);
            }

            Assert.AreEqual(5, _rig.Passives.Slots[0].Level);
            Assert.AreEqual(5, _rig.Passives.Slots[1].Level);
            Assert.AreEqual(twin.NextUInt(), rng.NextUInt(), "no candidates: only Chance is drawn");
        }

        [Test]
        public void TACF4_10_OneWeaponAndOnePassiveSplitTheUpgradesEvenly()
        {
            _rig = new PlrRig();
            SetPassives(_rig, ("P2", 1));
            WeaponInstance w1 = _rig.Weapons.Slots[0];
            PassiveInstance p2 = _rig.Passives.Slots[0];
            var rng = new RngStreams(42UL).Get(RngStream.Chest);
            var twin = new RngStreams(42UL).Get(RngStream.Chest);
            int weapon = 0;
            int passive = 0;
            for (int i = 0; i < 1000; i++)
            {
                ChestEffect effect = _rig.Chests.Resolve(rng, _rig.Context());
                if (twin.Chance(GameRules.ChestUpgradeChance))
                {
                    // Candidate order: weapon slots first, then passive slots.
                    Assert.AreEqual(twin.NextInt(2) == 0 ? ChestEffect.WeaponUpgraded : ChestEffect.PassiveUpgraded, effect);
                }
                else
                {
                    Assert.AreEqual(ChestEffect.Healed, effect);
                }

                if (effect == ChestEffect.WeaponUpgraded)
                {
                    weapon++;
                    Assert.AreEqual(2, w1.Level);
                    w1.SetLevel(1, 0f);
                }
                else if (effect == ChestEffect.PassiveUpgraded)
                {
                    passive++;
                    Assert.AreEqual(2, p2.Level);
                    p2.Level = 1;
                }
            }

            int upgrades = weapon + passive;
            Assert.Greater(upgrades, 0);
            Assert.AreEqual(0.5, weapon / (double)upgrades, 0.05);
            Assert.AreEqual(0.5, passive / (double)upgrades, 0.05);
            Assert.AreEqual(twin.NextUInt(), rng.NextUInt());
        }

        [Test]
        public void TACF4_10_P3FromAChestResizesTheMagnetAtOnce()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("E1", 1));
            _rig.Step();
            SetPassives(_rig, ("P3", 1));
            Assert.AreEqual(StatMath.ApplyMultiplier(2.0f, 0.10f), _rig.Player.MagnetBody.Radius);
            var rng = new RngStreams(3UL).Get(RngStream.Chest);
            ChestEffect effect = ChestEffect.Healed;
            for (int i = 0; i < 50 && effect != ChestEffect.PassiveUpgraded; i++)
            {
                effect = _rig.Chests.Resolve(rng, _rig.Context());
            }

            Assert.AreEqual(ChestEffect.PassiveUpgraded, effect);
            Assert.AreEqual(2, _rig.Passives.Slots[0].Level);
            Assert.AreEqual(StatMath.ApplyMultiplier(2.0f, 0.20f), _rig.Player.MagnetBody.Radius, "refreshed in the same call");
        }

        [Test]
        public void ApplyChestPassiveUpgrade_RejectsEmptyOutOfRangeAndMaxSlots()
        {
            _rig = new PlrRig();
            SetPassives(_rig, ("P1", 5), ((string)null, 0), ("P2", 1));
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyChestPassiveUpgrade(-1));
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyChestPassiveUpgrade(GameRules.MaxPassiveSlots));
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyChestPassiveUpgrade(1), "empty slot");
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyChestPassiveUpgrade(0), "P1 at max (R22)");
            int changes = _rig.Applier.ChangeCount;
            _rig.Applier.ApplyChestPassiveUpgrade(2);
            Assert.AreEqual(2, _rig.Passives.Slots[2].Level);
            Assert.AreEqual(changes + 1, _rig.Applier.ChangeCount);
        }

        [Test]
        public void TACF4_9_TheEleventhResultDropsTheOldestAndResetForRunKeepsTheSequence()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5), ("W3", 5));
            _rig.Step();
            ChestResultLog log = _rig.Applier.ChestResults;
            var steps = new List<long>();
            for (int i = 0; i < 11; i++)
            {
                StepContext ctx = _rig.Context();
                steps.Add(ctx.Step);
                Assert.AreEqual(ChestEffect.Healed, _rig.Chests.Resolve(_rig.ChestRng, ctx));
                _rig.Step();
            }

            Assert.AreEqual(ChestResultLog.Capacity, log.Count);
            Assert.AreEqual(11, log.LastSequence);
            for (int i = 0; i < log.Count; i++)
            {
                Assert.AreEqual(i + 2, log[i].Sequence, "0 = oldest kept; the first result was dropped");
                Assert.AreEqual(steps[i + 1], log[i].Step);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => { ChestResult unused = log[ChestResultLog.Capacity]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { ChestResult unused = log[-1]; });

            _rig.Applier.ResetForRun();
            Assert.AreSame(log, _rig.Applier.ChestResults);
            Assert.AreEqual(0, log.Count, "a new run starts with an empty list");
            Assert.AreEqual(11, log.LastSequence, "Clear keeps the sequence");
            Assert.Throws<ArgumentOutOfRangeException>(() => { ChestResult unused = log[0]; });
            _rig.Chests.Resolve(_rig.ChestRng, _rig.Context());
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(12, log[0].Sequence, "the sequence continues across runs");
            Assert.AreEqual(12, log.LastSequence);
        }

        [Test]
        public void TACF4_9_ReadingTheLogChangesNeitherTheChestStreamNorTheHash()
        {
            var rigs = new List<FlowEmRig>();
            try
            {
                var effects = new List<ChestEffect>[2];
                var hashes = new List<ulong>[2];
                var read = new List<ChestResult>();
                for (int r = 0; r < 2; r++)
                {
                    var rig = new FlowEmRig(start: false) { PolicyFactory = () => new MonEmNoSpawnPolicy() };
                    rigs.Add(rig);
                    rig.Rules.WeaponsEnabled = false;
                    rig.Rules.PlayerTakesDamage = false;
                    rig.Input.Still = true;
                    rig.StartRun();
                    rig.Enqueue(PendingCommand.SetLoadout(new List<WeaponLoadoutEntry>
                    {
                        new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W2", 1),
                    }));
                    rig.Step();
                    var sink = new List<ChestEffect>();
                    effects[r] = sink;
                    hashes[r] = new List<ulong>();
                    bool reads = r == 0;
                    for (int i = 0; i < 20; i++)
                    {
                        rig.Enqueue(PendingCommand.SetHealth(40f));
                        rig.ResolveHook.Pending.Add(ctx =>
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                sink.Add(rig.Chests.Resolve(rig.Rng.Get(RngStream.Chest), ctx));
                                if (reads)
                                {
                                    ChestResultLog log = rig.Applier.ChestResults;
                                    for (int j = 0; j < log.Count; j++)
                                    {
                                        read.Add(log[j]);
                                    }

                                    Assert.AreEqual(sink.Count, log.LastSequence);
                                }
                            }
                        });
                        rig.Step();
                        hashes[r].Add(rig.Hash);
                    }
                }

                Assert.Greater(read.Count, 0);
                Assert.AreEqual(ChestResultLog.Capacity, rigs[0].Applier.ChestResults.Count);
                CollectionAssert.Contains(effects[0], ChestEffect.WeaponUpgraded);
                CollectionAssert.Contains(effects[0], ChestEffect.Healed);
                CollectionAssert.AreEqual(effects[0], effects[1], "same effects");
                CollectionAssert.AreEqual(hashes[0], hashes[1], "same state hash after every step");
                for (int slot = 0; slot < GameRules.MaxWeaponSlots; slot++)
                {
                    rigs[0].Weapons.TryGetSlot(slot, out WeaponInstance a);
                    rigs[1].Weapons.TryGetSlot(slot, out WeaponInstance b);
                    Assert.AreEqual(a?.Id, b?.Id);
                    Assert.AreEqual(a?.Level, b?.Level);
                }

                Assert.AreEqual(rigs[0].Health.Hp, rigs[1].Health.Hp);
                Assert.AreEqual(rigs[1].Rng.Get(RngStream.Chest).NextUInt(), rigs[0].Rng.Get(RngStream.Chest).NextUInt(),
                    "the next Chest stream value is the same");
            }
            finally
            {
                foreach (FlowEmRig rig in rigs)
                {
                    rig.Dispose();
                }
            }
        }
    }
}
