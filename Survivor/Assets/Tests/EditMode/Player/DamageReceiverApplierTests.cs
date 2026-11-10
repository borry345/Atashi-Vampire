using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // TAC-F3-12 (player damage), PlayerProgressionApplier contracts (5.14, TAC-0-1, R22), static checks (TAC-0-2, F1-1, F2-4)
    // and a same-input determinism check.
    public sealed class DamageReceiverApplierTests
    {
        private PlrRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
        }

        private List<long> RunAndRecordDamageSteps(int steps)
        {
            var damaged = new List<long>();
            for (int i = 0; i < steps; i++)
            {
                float before = _rig.Health.Hp;
                long step = _rig.Clock.Step;
                _rig.Step();
                if (_rig.Health.Hp < before)
                {
                    damaged.Add(step);
                }
            }

            return damaged;
        }

        [Test]
        public void TACF3_12_FourHundredOverlapsDamageOncePerInvulnerabilityWindowWithTheMaximum()
        {
            _rig = new PlrRig();
            _rig.Health.ResetForRun(1e5f);
            for (int i = 0; i < 400; i++)
            {
                PlrMonster m = _rig.AddMonster(new Vector2(100f, i), contactDamage: 1f + (i % 7));
                _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, m.Id);
            }

            _rig.Step();
            float hpBefore = _rig.Health.Hp;
            List<long> damaged = RunAndRecordDamageSteps(100);
            CollectionAssert.AreEqual(new long[] { 1, 31, 61, 91 }, damaged, "invulnerabilitySeconds 0.5 = 30 steps");
            Assert.AreEqual(hpBefore - 4 * 7f, _rig.Health.Hp, 1e-3f, "maximum contact damage (7) once per window");
            Assert.AreEqual(91 + 30, _rig.Receiver.InvulnerableUntilStep);
        }

        [Test]
        public void TACF3_12_InvulnerablePlayerStillConsumesEnemyProjectilesWithoutDamage()
        {
            _rig = new PlrRig();
            PlrMonster body = _rig.AddMonster(new Vector2(50f, 0f), contactDamage: 10f);
            PlrMonster[] bullet = _rig.SpawnLifecycleMonster(EntityCategory.EnemyProjectile, new Vector2(50f, 5f), contactDamage: 25f,
                targetable: false);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, body.Id);
            _rig.Step();
            _rig.Step();
            Assert.AreEqual(90f, _rig.Health.Hp);
            _rig.Collisions.Add(2, CollisionEventKind.Enter, CollisionChannel.EnemyProjectileVsPlayerBody, bullet[0].Id, _rig.PlayerId);
            _rig.Collisions.Add(2, CollisionEventKind.Enter, CollisionChannel.EnemyProjectileVsPlayerBody, bullet[0].Id, _rig.PlayerId);
            _rig.Step();
            Assert.AreEqual(90f, _rig.Health.Hp, "no damage while invulnerable");
            List<SimulationCommand> despawns = _rig.Commands.At(2, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count, "consumed once; the second event saw IsConsumed");
            Assert.AreEqual(DespawnReason.Consumed, despawns[0].Reason);
            Assert.AreEqual(bullet[0].Id, despawns[0].Source);
            Assert.IsFalse(_rig.Lifecycle.TryGet(bullet[0].Id, out _), "recycled in the same step's Lifecycle");
        }

        [Test]
        public void TACF3_12_EnemyProjectileDamageCompetesWithBodyContact()
        {
            _rig = new PlrRig();
            PlrMonster body = _rig.AddMonster(new Vector2(50f, 0f), contactDamage: 10f);
            PlrMonster[] bullet = _rig.SpawnLifecycleMonster(EntityCategory.EnemyProjectile, new Vector2(50f, 5f), contactDamage: 25f,
                targetable: false);
            _rig.Step();
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, body.Id);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.EnemyProjectileVsPlayerBody, bullet[0].Id, _rig.PlayerId);
            _rig.Step();
            Assert.AreEqual(75f, _rig.Health.Hp, "max(10, 25) once");
        }

        [Test]
        public void TACF3_12_DespawnedOverlapStopsDamagingWithoutAnExit()
        {
            _rig = new PlrRig();
            PlrMonster[] monster = _rig.SpawnLifecycleMonster(EntityCategory.Monster, new Vector2(50f, 0f), contactDamage: 10f);
            _rig.Step();
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, monster[0].Id);
            _rig.Step();
            Assert.AreEqual(1, _rig.Receiver.OverlapCount);
            _rig.DespawnInLifecycle(monster[0].Id, DespawnReason.OutOfRange);
            _rig.Step();
            Assert.AreEqual(0, _rig.Receiver.OverlapCount, "Despawned removed it from the overlap set");
            List<long> damaged = RunAndRecordDamageSteps(60);
            Assert.AreEqual(0, damaged.Count);
            Assert.AreEqual(90f, _rig.Health.Hp);
        }

        [Test]
        public void TACF3_12_PlayerTakesDamageFalseAndZeroInvulnerabilityAndNullSources()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.PlayerStats, PlrTestData.PlayerStatsNoInvulnerability));
            PlrMonster monster = _rig.AddMonster(new Vector2(50f, 0f), contactDamage: 1f);
            _rig.Collisions.Add(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, monster.Id);
            _rig.Collisions.Add(0, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, _rig.PlayerId, new EntityHandle(77, 1));
            _rig.Rules.PlayerTakesDamage = false;
            _rig.Steps(3);
            Assert.AreEqual(100f, _rig.Health.Hp);
            _rig.Rules.PlayerTakesDamage = true;
            List<long> damaged = RunAndRecordDamageSteps(4);
            CollectionAssert.AreEqual(new long[] { 3, 4, 5, 6 }, damaged, "invulnerabilitySteps 0: every step");
        }

        [Test]
        public void TAC0_1_ApplyLevelUpOptionOnlyWhileTheLevelUpGateIsClosed()
        {
            _rig = new PlrRig();
            var option = new UpgradeOption(UpgradeKind.Heal, null);
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(option), "gate never closed for level-up");
            _rig.ResolveHook.Pending.Add(ctx => Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(option)));
            _rig.StepAndCloseForLevelUp();
            Assert.DoesNotThrow(() => _rig.Applier.ApplyLevelUpOption(option));
            _rig.Clock.Open();
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(option), "reopened gate");
        }

        [Test]
        public void R22_InapplicableLevelUpOptionsThrow()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 5), ("W2", 1), ("W3", 1), ("W4", 1));
            _rig.StepAndCloseForLevelUp();
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponUpgrade, "W1")), "level 5 is max");
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "W2")), "already owned");
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "E1")), "slots full / evolved-only");
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P1")), "not owned");
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P2"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            Assert.AreEqual(3, _rig.Passives.Count);
            for (int i = 0; i < 4; i++)
            {
                _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P2"));
            }

            Assert.AreEqual(5, _rig.Passives.Level("P2"));
            Assert.AreEqual(0.5f, _rig.Passives.BonusOf(PassiveStat.MoveSpeed));
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P2")));
        }

        [Test]
        public void SetLoadout_ValidatesFirstAndReplacesRemovedWeaponChildren()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W2", 1));
            _rig.Step();
            EntityHandle firstOrb = _rig.Live<OrbitOrb>()[0].Id;
            StepContext ctx = _rig.Context();
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W2", 1), new WeaponLoadoutEntry("W3", 1),
                new WeaponLoadoutEntry("W4", 1), new WeaponLoadoutEntry("E1", 1),
            }, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[] { new WeaponLoadoutEntry("NOPE", 1) }, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[] { new WeaponLoadoutEntry("W1", 6) }, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[] { new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W1", 2) }, ctx));
            Assert.Throws<ArgumentNullException>(() => _rig.Applier.SetLoadout(null, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.HandleSetLoadout(PendingCommand.SetLoadout("W1"), ctx));
            Assert.AreEqual("W2", _rig.Weapons.Slots[0].Id, "nothing changed after rejected loadouts");

            WeaponTests.QueueLoadout(_rig, ("W3", 2));
            _rig.Step();
            List<SimulationCommand> despawns = _rig.Commands.At(1, SimulationCommandKind.Despawn);
            Assert.AreEqual(2, despawns.Count);
            Assert.AreEqual(firstOrb, despawns[0].Target);
            Assert.AreEqual(DespawnReason.Replaced, despawns[0].Reason);
            Assert.AreEqual(_rig.PlayerId, despawns[0].Source);
            Assert.AreEqual(0, _rig.Live<OrbitOrb>().Count);
            Assert.AreEqual("W3", _rig.Weapons.Slots[0].Id);
            Assert.AreEqual(2, _rig.Weapons.Slots[0].Level);
            Assert.AreEqual(1, _rig.Weapons.Slots[0].NextFireStep);
        }

        [Test]
        public void TACF19_15_NullWeaponIdEmptiesThatSlotAndKeepsTheOtherInstances()
        {
            _rig = new PlrRig();
            WeaponTests.QueueLoadout(_rig, ("W1", 2), ("W2", 3), ("W3", 1));
            _rig.Step();
            WeaponInstance w1 = _rig.Weapons.Slots[0];
            WeaponInstance w3 = _rig.Weapons.Slots[2];
            long w1Next = w1.NextFireStep;
            long w3Next = w3.NextFireStep;
            List<EntityHandle> orbs = _rig.Live<OrbitOrb>().ConvertAll(o => o.Id);
            Assert.AreEqual(3, orbs.Count);

            // Level is ignored for an empty-slot entry (no definition, level or duplicate check among the null entries).
            WeaponTests.QueueLoadout(_rig, ("W1", 2), ((string)null, 0), ("W3", 4), ((string)null, 99));
            long s = _rig.Clock.Step;
            _rig.Step();
            Assert.AreSame(w1, _rig.Weapons.Slots[0], "slot 0 keeps its instance");
            Assert.IsNull(_rig.Weapons.Slots[1], "the null entry empties slot 1");
            Assert.AreSame(w3, _rig.Weapons.Slots[2], "slot 2 keeps its instance and is not pulled forward");
            Assert.IsNull(_rig.Weapons.Slots[3]);
            Assert.AreEqual(2, _rig.Weapons.Count);
            Assert.AreEqual(4, w3.Level);
            Assert.AreEqual(w1Next, w1.NextFireStep, "kept cooldown");
            Assert.AreEqual(w3Next, w3.NextFireStep, "kept cooldown");
            List<SimulationCommand> despawns = _rig.Commands.At(s, SimulationCommandKind.Despawn);
            Assert.AreEqual(3, despawns.Count, "only the W2 orbs are recycled");
            for (int i = 0; i < despawns.Count; i++)
            {
                Assert.AreEqual(orbs[i], despawns[i].Target, "ascending id");
                Assert.AreEqual(DespawnReason.Replaced, despawns[i].Reason);
                Assert.AreEqual(_rig.PlayerId, despawns[i].Source);
            }

            Assert.AreEqual(0, _rig.Live<OrbitOrb>().Count);

            // Refilling the empty slot creates a new instance there; the other slots still keep theirs.
            WeaponTests.QueueLoadout(_rig, ("W1", 2), ("W4", 1), ("W3", 4));
            s = _rig.Clock.Step;
            _rig.Step();
            Assert.AreSame(w1, _rig.Weapons.Slots[0]);
            Assert.AreEqual("W4", _rig.Weapons.Slots[1].Id);
            Assert.AreEqual(1, _rig.Weapons.Slots[1].SlotIndex);
            Assert.AreEqual(s, _rig.Weapons.Slots[1].NextFireStep, "first-ownership rule");
            Assert.AreSame(w3, _rig.Weapons.Slots[2]);

            // The other rules are unchanged: more than four entries throw even with null entries, duplicates among real ids throw.
            StepContext ctx = _rig.Context();
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry(null, 0), new WeaponLoadoutEntry(null, 0),
                new WeaponLoadoutEntry(null, 0), new WeaponLoadoutEntry(null, 0),
            }, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry(null, 0), new WeaponLoadoutEntry("W1", 2),
            }, ctx));
            Assert.Throws<ArgumentException>(() => _rig.Applier.SetLoadout(new[]
            {
                new WeaponLoadoutEntry(null, 0), new WeaponLoadoutEntry("W2", 0),
            }, ctx));
            Assert.AreSame(w1, _rig.Weapons.Slots[0], "nothing changed after rejected loadouts");
            Assert.AreEqual("W4", _rig.Weapons.Slots[1].Id);
        }

        [Test]
        public void SetHealthHandlerClampsAndResetForRunRestoresTheStart()
        {
            _rig = new PlrRig();
            _rig.Applier.HandleSetHealth(PendingCommand.SetHealth(250f), _rig.Context());
            Assert.AreEqual(100f, _rig.Health.Hp);
            _rig.Applier.HandleSetHealth(PendingCommand.SetHealth(33f), _rig.Context());
            Assert.AreEqual(33f, _rig.Health.Hp);
            WeaponTests.QueueLoadout(_rig, ("W3", 1), ("W4", 1));
            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            int changes = _rig.Applier.ChangeCount;
            _rig.Applier.ResetForRun();
            Assert.Greater(_rig.Applier.ChangeCount, changes);
            Assert.AreEqual(100f, _rig.Health.Hp);
            Assert.AreEqual(0, _rig.Passives.Count);
            Assert.AreEqual(1, _rig.Weapons.Count);
            Assert.AreEqual("W1", _rig.Weapons.Slots[0].Id);
            Assert.AreEqual(_rig.Clock.Step, _rig.Weapons.Slots[0].NextFireStep);
        }

        [Test]
        public void TAC0_2_PlayerAndWeaponCodeReadsNoClockRandomOrInputDevice()
        {
            var forbidden = new Regex(@"\bTime\.|UnityEngine\.Random|System\.Random|\bRandom\.|DateTime|Keyboard|Survivor\.Strategies");
            int files = 0;
            foreach (string folder in new[] { "Player", "Weapons" })
            {
                foreach (string path in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts", "Simulation", folder), "*.cs"))
                {
                    files++;
                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = Regex.Replace(lines[i], @"//.*$", string.Empty);
                        Assert.IsFalse(forbidden.IsMatch(code), Path.GetFileName(path) + ":" + (i + 1) + " " + lines[i].Trim());
                    }
                }
            }

            Assert.GreaterOrEqual(files, 23);
        }

        [Test]
        public void Determinism_SameInputsGiveTheSameCommandsAndPositions()
        {
            string first = RunScenario();
            string second = RunScenario();
            Assert.AreEqual(first, second);
        }

        private static string RunScenario()
        {
            var rig = new PlrRig();
            try
            {
                for (int i = 0; i < 12; i++)
                {
                    rig.AddMonster(new Vector2(4f + i, (i % 3) - 1f), contactDamage: 3f);
                }

                WeaponTests.QueueLoadout(rig, ("W1", 4), ("W2", 3), ("W3", 2), ("W4", 5));
                rig.Input.Next = new MoveCommand(MoveMath.ToUnitDirection(1, 1));
                rig.Steps(240);
                var text = new System.Text.StringBuilder();
                foreach ((long step, SimulationPhase phase, SimulationCommand c) in rig.Commands.Log)
                {
                    text.Append(step).Append(c.Kind).Append(c.Source).Append(c.Sequence).Append(c.Target).Append(c.Position.x.ToString("R"))
                        .Append(c.Position.y.ToString("R")).Append(c.Direction.x.ToString("R")).Append(c.OrbIndex).Append('|');
                }

                foreach (SimEntity e in rig.Live<SimEntity>())
                {
                    text.Append(e.Id).Append(e.Position.x.ToString("R")).Append(e.Position.y.ToString("R")).Append(';');
                }

                for (int i = 0; i < 4; i++)
                {
                    text.Append(rig.Weapons.Slots[i].NextFireStep).Append(',');
                }

                return text.ToString();
            }
            finally
            {
                rig.Destroy();
            }
        }
    }
}
