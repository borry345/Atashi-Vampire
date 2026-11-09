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
    /// StepOutcomeGate (TAC-F8-8, TAC-F5-8, TAC-F5-6) and the level-up round sequence (TAC-F5-4 EM part, TAC-0-1 2) on the full rig.
    public sealed class StepOutcomeGateTests
    {
        private FlowEmRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new FlowEmRig(start: false);
            _rig.PolicyFactory = () => new MonEmNoSpawnPolicy();
            _rig.Input.Still = true;
            _rig.StartRun();
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
        }

        // Spawns the boss (data's bossMonsterId) through the SpawnMonsters command in the next step's Lifecycle.
        private Monster SpawnBoss()
        {
            string bossId = _rig.Data.Current.SpawnTable.BossMonsterId;
            _rig.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: new Vector2(30f, 0f),
                definitionId: bossId, count: 1, ignoreCap: true));
            _rig.Step();
            Monster boss = _rig.Live<Monster>().Single(m => m.IsBoss);
            Assert.IsTrue(_rig.Spawner.BossSpawnedThisRun);
            return boss;
        }

        private void KillNextStep(Monster monster)
        {
            _rig.ResolveHook.Pending.Add(context => monster.ApplyDamage(1e9f, context.Step));
        }

        [Test]
        public void LevelUpNow_ClosesTheGateAfterThisStep_WithOneLevel()
        {
            _rig.Step();
            _rig.Enqueue(PendingCommand.LevelUpNow());
            long s = _rig.Clock.Step;
            _rig.Step();

            Assert.AreEqual(GateCloseReason.LevelUp, _rig.Clock.CloseReason);
            Assert.IsFalse(_rig.Clock.IsRunning);
            Assert.AreEqual(s + 1, _rig.Clock.Step, "closed after step s completed (TAC-0-7)");
            Assert.AreEqual(2, _rig.Experience.Level);
            Assert.AreEqual(0, _rig.Experience.Xp);
            Assert.AreEqual(RunOutcomeKind.None, _rig.Gate.PendingOutcome.Kind);
        }

        [Test]
        public void StoredLevels_OpenOneAtATime_RoundIds123_WithoutSteps()
        {
            _rig.Enqueue(PendingCommand.AddXp(5 + 8 + 12));
            _rig.Step();
            Assert.AreEqual(GateCloseReason.LevelUp, _rig.Clock.CloseReason);
            Assert.AreEqual(2, _rig.Experience.Level, "the gate takes exactly one level per step");
            Assert.AreEqual(20, _rig.Experience.Xp);
            long stepAtClose = _rig.Clock.Step;
            int closeSequence = _rig.Clock.CloseSequence;

            _rig.ResolveLevelUps();

            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, _rig.RoundLog);
            Assert.AreEqual(stepAtClose, _rig.Clock.Step, "rounds 2 and 3 open without running a step");
            Assert.AreEqual(closeSequence, _rig.Clock.CloseSequence, "no further gate close");
            Assert.AreEqual(4, _rig.Experience.Level);
            Assert.AreEqual(0, _rig.Experience.Xp);
            Assert.IsTrue(_rig.Clock.IsRunning, "the last re-check was false, so the clock reopened");
            Assert.AreEqual(4, _rig.Applier.ChangeCount, "run start + three applied choices");
        }

        [Test]
        public void ApplyLevelUpOption_AfterOpen_Throws_AndBeforeOpen_Works()
        {
            _rig.Enqueue(PendingCommand.LevelUpNow());
            _rig.Step();
            Assert.DoesNotThrow(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "W2")));
            _rig.Clock.Open();
            Assert.AreEqual(GateCloseReason.LevelUp, _rig.Clock.CloseReason, "CloseReason keeps its last value after Open");
            Assert.Throws<InvalidOperationException>(() => _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "W3")));
        }

        [Test]
        public void NewWeaponFromLevelUp_FirstShotIsTheClosedStep()
        {
            _rig.Step();
            _rig.Step();
            _rig.Enqueue(PendingCommand.LevelUpNow());
            _rig.Step();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "W3"));
            Assert.AreEqual(_rig.Clock.Step, _rig.Weapons.Slots[1].NextFireStep, "first-ownership rule (TAC-F2-2)");
        }

        [Test]
        public void LevelUpDisabled_XpAccumulates_NoLevelNoClose()
        {
            _rig.Rules.LevelUpEnabled = false;
            _rig.Enqueue(PendingCommand.AddXp(100));
            _rig.Step();
            _rig.Step();
            Assert.IsTrue(_rig.Clock.IsRunning);
            Assert.AreEqual(1, _rig.Experience.Level);
            Assert.AreEqual(100, _rig.Experience.Xp);
        }

        [Test]
        public void Death_ClosesWithDeath_AndPendingOutcome()
        {
            _rig.Step();
            _rig.Step();
            _rig.Enqueue(PendingCommand.SetHealth(0f));
            long s = _rig.Clock.Step;
            _rig.Step();

            Assert.AreEqual(GateCloseReason.Death, _rig.Clock.CloseReason);
            RunOutcome outcome = _rig.Gate.PendingOutcome;
            Assert.AreEqual(RunOutcomeKind.Death, outcome.Kind);
            Assert.AreEqual(s + 1, outcome.SurvivalSteps);
            Assert.AreEqual(0, outcome.Kills);
            Assert.AreEqual(1, outcome.Level);
        }

        [Test]
        public void BossKill_BeforeStep36000_IsClear_WithKills()
        {
            Monster boss = SpawnBoss();
            KillNextStep(boss);
            long s = _rig.Clock.Step;
            _rig.Step();

            Assert.AreEqual(GateCloseReason.Clear, _rig.Clock.CloseReason);
            RunOutcome outcome = _rig.Gate.PendingOutcome;
            Assert.AreEqual(RunOutcomeKind.Clear, outcome.Kind);
            Assert.AreEqual(s + 1, outcome.SurvivalSteps);
            Assert.AreEqual(1, outcome.Kills, "the boss counts as a kill (R24)");
            Assert.AreEqual(_rig.Kills.KillCount, outcome.Kills);
        }

        [Test]
        public void BossFromTheSpawnPolicy_KilledLater_IsClear()
        {
            _rig.Dispose();
            _rig = new FlowEmRig(start: false);
            _rig.PolicyFactory = () => new FlowBossAtPolicy(2);
            _rig.Input.Still = true;
            _rig.StartRun();
            _rig.Step();
            _rig.Step();
            _rig.Step();
            Monster boss = _rig.Live<Monster>().Single(m => m.IsBoss);
            _rig.Step();
            Assert.IsTrue(_rig.Clock.IsRunning);
            KillNextStep(boss);
            _rig.Step();
            Assert.AreEqual(GateCloseReason.Clear, _rig.Clock.CloseReason);
        }

        [Test]
        public void DeathAndBossKillInOneStep_IsDeath_AndNoLevelUp()
        {
            Monster boss = SpawnBoss();
            _rig.Enqueue(PendingCommand.AddXp(50));
            _rig.Enqueue(PendingCommand.SetHealth(0f));
            KillNextStep(boss);
            _rig.Step();

            Assert.IsTrue(_rig.Kills.BossKilledThisStep, "the boss did die this step");
            Assert.AreEqual(GateCloseReason.Death, _rig.Clock.CloseReason, "death > clear (TAC-F8-8)");
            Assert.AreEqual(RunOutcomeKind.Death, _rig.Gate.PendingOutcome.Kind);
            Assert.AreEqual(1, _rig.Experience.Level, "TryBeginLevelUp is not called on death (TAC-F5-8)");
            Assert.AreEqual(50, _rig.Experience.Xp);
        }

        [Test]
        public void ClearWithEnoughXp_IsClear_AndNoLevelUp()
        {
            Monster boss = SpawnBoss();
            _rig.Enqueue(PendingCommand.LevelUpNow());
            KillNextStep(boss);
            _rig.Step();

            Assert.AreEqual(GateCloseReason.Clear, _rig.Clock.CloseReason, "clear > level-up");
            Assert.AreEqual(1, _rig.Experience.Level);
            Assert.AreEqual(5, _rig.Experience.Xp);
        }

        [Test]
        public void DeathWithEnoughXp_IsDeath_AndNoLevelUp()
        {
            _rig.Enqueue(PendingCommand.LevelUpNow());
            _rig.Enqueue(PendingCommand.SetHealth(0f));
            _rig.Step();
            Assert.AreEqual(GateCloseReason.Death, _rig.Clock.CloseReason);
            Assert.AreEqual(1, _rig.Experience.Level);
        }

        [Test]
        public void Gate_OnlyActsInLifecycle_AndRejectsNulls()
        {
            var context = new StepContext(0, Vector2.zero, _rig.Rules, null, null);
            _rig.Enqueue(PendingCommand.LevelUpNow());
            _rig.Step();
            _rig.Clock.Open();
            int level = _rig.Experience.Level;
            _rig.Experience.AddXp(100);
            foreach (SimulationPhase phase in Enum.GetValues(typeof(SimulationPhase)))
            {
                if (phase != SimulationPhase.Lifecycle)
                {
                    _rig.Gate.OnPhase(phase, context);
                }
            }

            Assert.AreEqual(level, _rig.Experience.Level);
            Func<Survivor.Data.GameData> data = () => _rig.Data.Current;
            Assert.Throws<ArgumentNullException>(() => new StepOutcomeGate(null, _rig.Health, _rig.Kills, _rig.Experience, data));
            Assert.Throws<ArgumentNullException>(() => new StepOutcomeGate(_rig.Clock, null, _rig.Kills, _rig.Experience, data));
            Assert.Throws<ArgumentNullException>(() => new StepOutcomeGate(_rig.Clock, _rig.Health, null, _rig.Experience, data));
            Assert.Throws<ArgumentNullException>(() => new StepOutcomeGate(_rig.Clock, _rig.Health, _rig.Kills, null, data));
            Assert.Throws<ArgumentNullException>(() => new StepOutcomeGate(_rig.Clock, _rig.Health, _rig.Kills, _rig.Experience, null));
        }

        [Test]
        public void PendingCommandQueue_HasAllFourOwnerHandlers()
        {
            Assert.DoesNotThrow(() => _rig.Enqueue(PendingCommand.SetLoadout(new List<WeaponLoadoutEntry> { new WeaponLoadoutEntry("W1", 2) })));
            Assert.DoesNotThrow(() => _rig.Enqueue(PendingCommand.SetHealth(80f)));
            Assert.DoesNotThrow(() => _rig.Enqueue(PendingCommand.AddXp(1)));
            Assert.DoesNotThrow(() => _rig.Enqueue(PendingCommand.LevelUpNow()));
            _rig.Step();
            Assert.AreEqual(2, _rig.Weapons.Slots[0].Level);
            Assert.AreEqual(80f, _rig.Health.Hp);
            Assert.AreEqual(2, _rig.Experience.Level, "AddXp 1 + LevelUpNow 4 = 5, then the gate");
        }
    }
}
