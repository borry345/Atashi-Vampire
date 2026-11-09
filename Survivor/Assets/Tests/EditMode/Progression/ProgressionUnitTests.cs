using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Monsters;
using UnityEngine;

namespace Survivor.Tests.EditMode.Progression
{
    /// ExperienceSystem (TAC-F5-4, 5.15 LevelUpNow), LevelUpSession (R29, TAC-F5-5), BestRecordStore (TAC-F8-3) and the value types.
    public sealed class ProgressionUnitTests
    {
        private const string TestKey = "BestSurvivalSteps.Test.Flow";

        private static GameData _data;

        private static GameData RealData()
        {
            if (_data == null)
            {
                _data = MonEmData.Load().data;
            }

            return _data;
        }

        private static XpCurveDefinition Curve => RealData().PlayerStats.XpCurve;

        private static ExperienceSystem NewExperience()
        {
            GameData data = RealData();
            return new ExperienceSystem(() => data);
        }

        private static List<UpgradeOption> Three(string a = "W2", string b = "P1", string c = "W3")
        {
            return new List<UpgradeOption>
            {
                new UpgradeOption(UpgradeKind.WeaponNew, a), new UpgradeOption(UpgradeKind.PassiveNew, b), new UpgradeOption(UpgradeKind.WeaponNew, c),
            };
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(TestKey);
        }

        [Test]
        public void Experience_StartsAtLevelOne_AndAddXpNeverLevelsUp()
        {
            ExperienceSystem experience = NewExperience();
            Assert.AreEqual(1, experience.Level);
            Assert.AreEqual(0, experience.Xp);

            experience.AddXp(100);
            Assert.AreEqual(1, experience.Level, "AddXp only accumulates; the gate levels up");
            Assert.AreEqual(100, experience.Xp);
        }

        [Test]
        public void Experience_TryBeginLevelUp_SubtractsThreshold_OneLevelPerCall()
        {
            ExperienceSystem experience = NewExperience();
            CollectionAssert.AreEqual(new[] { 5, 8, 12, 16, 20, 25, 30 }, Curve.Thresholds, "real curve (playerStats.json)");
            experience.AddXp(5 + 8 + 12 + 3);

            Assert.IsTrue(experience.TryBeginLevelUp(Curve));
            Assert.AreEqual(2, experience.Level);
            Assert.AreEqual(23, experience.Xp);
            Assert.IsTrue(experience.TryBeginLevelUp(Curve));
            Assert.AreEqual(3, experience.Level);
            Assert.AreEqual(15, experience.Xp);
            Assert.IsTrue(experience.TryBeginLevelUp(Curve));
            Assert.AreEqual(4, experience.Level);
            Assert.AreEqual(3, experience.Xp);
            Assert.IsFalse(experience.TryBeginLevelUp(Curve), "3 < 16");
            Assert.AreEqual(4, experience.Level);
            Assert.AreEqual(3, experience.Xp);
        }

        [Test]
        public void Experience_BeyondTheTable_UsesGrowth()
        {
            ExperienceSystem experience = NewExperience();
            int total = 5 + 8 + 12 + 16 + 20 + 25 + 30;
            experience.AddXp(total + 35);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(experience.TryBeginLevelUp(Curve));
            }

            Assert.AreEqual(8, experience.Level);
            Assert.AreEqual(35, experience.Xp);
            Assert.IsTrue(experience.TryBeginLevelUp(Curve), "level 8 -> 9 needs 30 + 5");
            Assert.AreEqual(0, experience.Xp);
        }

        [Test]
        public void Experience_RejectsNegativeXp_Saturates_AndResets()
        {
            ExperienceSystem experience = NewExperience();
            Assert.Throws<ArgumentOutOfRangeException>(() => experience.AddXp(-1));
            Assert.Throws<ArgumentNullException>(() => experience.TryBeginLevelUp(null));
            Assert.Throws<ArgumentNullException>(() => new ExperienceSystem(null));

            experience.AddXp(int.MaxValue);
            experience.AddXp(10);
            Assert.AreEqual(int.MaxValue, experience.Xp);
            experience.TryBeginLevelUp(Curve);
            experience.Reset();
            Assert.AreEqual(1, experience.Level);
            Assert.AreEqual(0, experience.Xp);
        }

        [Test]
        public void Experience_PendingHandlers_AreMethodGroups_AddXpAndLevelUpNow()
        {
            ExperienceSystem experience = NewExperience();
            Action<PendingCommand, StepContext> addXp = experience.HandleAddXp;
            Action<PendingCommand, StepContext> levelUpNow = experience.HandleLevelUpNow;

            addXp(PendingCommand.AddXp(3), default);
            Assert.AreEqual(3, experience.Xp);
            levelUpNow(PendingCommand.LevelUpNow(), default);
            Assert.AreEqual(5, experience.Xp, "adds exactly what is missing for level 2");
            Assert.AreEqual(1, experience.Level, "the gate levels up, not the handler");

            experience.AddXp(4);
            levelUpNow(PendingCommand.LevelUpNow(), default);
            Assert.AreEqual(9, experience.Xp, "already enough: nothing added");
        }

        [Test]
        public void Experience_LevelUpNow_ReadsTheCurrentCurveEveryTime()
        {
            GameData data = RealData();
            GameData patched = MonEmData.Load((DataFileKind.PlayerStats, "\"thresholds\": [5,", "\"thresholds\": [9,")).data;
            GameData current = data;
            var experience = new ExperienceSystem(() => current);
            experience.HandleLevelUpNow(PendingCommand.LevelUpNow(), default);
            Assert.AreEqual(5, experience.Xp);

            experience.Reset();
            current = patched;
            experience.HandleLevelUpNow(PendingCommand.LevelUpNow(), default);
            Assert.AreEqual(9, experience.Xp, "hot-reloaded curve");
        }

        [Test]
        public void Session_Open_IncrementsRound_AndRejectsInvalidOpen()
        {
            var session = new LevelUpSession();
            Assert.AreEqual(0, session.RoundId);
            Assert.IsFalse(session.IsOpen);
            Assert.AreEqual(0, session.Options.Count);

            session.Open(Three(), 10);
            Assert.AreEqual(1, session.RoundId);
            Assert.IsTrue(session.IsOpen);
            Assert.AreEqual(10, session.OpenedFrame);
            Assert.Throws<InvalidOperationException>(() => session.Open(Three(), 11), "a round is open");

            session.Close();
            Assert.Throws<ArgumentException>(() => session.Open(new List<UpgradeOption> { default, default }, 12));
            Assert.Throws<ArgumentNullException>(() => session.Open(null, 12));
            Assert.AreEqual(1, session.RoundId, "failed opens do not consume a round id");
            session.Open(Three(), 12);
            Assert.AreEqual(2, session.RoundId);
        }

        [Test]
        public void Session_TrySubmit_AcceptsOnlyTheFirstValidSubmission()
        {
            var session = new LevelUpSession();
            Assert.IsFalse(session.TrySubmit(0, 0, 1, out _), "closed");
            session.Open(Three(), 10);

            Assert.IsFalse(session.TrySubmit(1, 0, 10, out _), "same frame as the open (TAC-F5-5)");
            Assert.IsFalse(session.TrySubmit(2, 0, 11, out _), "wrong round");
            Assert.IsFalse(session.TrySubmit(0, 0, 11, out _), "stale round");
            Assert.IsFalse(session.TrySubmit(1, -1, 11, out _));
            Assert.IsFalse(session.TrySubmit(1, 3, 11, out _));

            Assert.IsTrue(session.TrySubmit(1, 2, 11, out UpgradeOption chosen));
            Assert.AreEqual(UpgradeKind.WeaponNew, chosen.Kind);
            Assert.AreEqual("W3", chosen.TargetId);
            Assert.IsFalse(session.TrySubmit(1, 0, 11, out _), "second input of the same round (key + click)");
            Assert.IsFalse(session.TrySubmit(1, 1, 12, out _));
            Assert.IsTrue(session.IsOpen, "TrySubmit does not close the round");

            session.Close();
            Assert.IsFalse(session.TrySubmit(1, 0, 13, out _), "closed");
            session.Open(Three("W4", "P2", "W2"), 13);
            Assert.IsFalse(session.TrySubmit(1, 0, 14, out _), "the previous round id is stale");
            Assert.IsTrue(session.TrySubmit(2, 0, 14, out chosen));
            Assert.AreEqual("W4", chosen.TargetId);
        }

        [Test]
        public void Session_OptionsAreCopied_KeptAfterClose_AndResetClearsEverything()
        {
            var session = new LevelUpSession();
            List<UpgradeOption> input = Three();
            session.Open(input, 1);
            input[0] = new UpgradeOption(UpgradeKind.Heal, "", true);
            Assert.AreEqual("W2", session.Options[0].TargetId, "the round keeps its own copy");

            session.Close();
            Assert.AreEqual(3, session.Options.Count, "options stay until the next Open");

            session.Reset();
            Assert.AreEqual(0, session.RoundId);
            Assert.IsFalse(session.IsOpen);
            Assert.AreEqual(0, session.Options.Count);
            session.Open(Three(), 0);
            Assert.AreEqual(1, session.RoundId, "round ids restart per run");
        }

        [Test]
        public void BestRecord_NoRecord_ThenSavesOnlyImprovements()
        {
            var store = new BestRecordStore(TestKey);
            var rules = new SimulationRules();
            PlayerPrefs.DeleteKey(TestKey);
            Assert.IsFalse(store.TryGetBest(out _), "no record yet (result screen shows -)");

            Assert.IsTrue(store.TryRecord(new RunOutcome(RunOutcomeKind.Death, 600, 3, 2), rules));
            Assert.IsTrue(store.TryGetBest(out int best));
            Assert.AreEqual(600, best);

            Assert.IsFalse(store.TryRecord(new RunOutcome(RunOutcomeKind.Death, 600, 0, 1), rules), "equal is not better");
            Assert.IsFalse(store.TryRecord(new RunOutcome(RunOutcomeKind.Clear, 599, 0, 1), rules));
            Assert.IsTrue(store.TryRecord(new RunOutcome(RunOutcomeKind.Clear, 36001, 0, 1), rules));
            store.TryGetBest(out best);
            Assert.AreEqual(36001, best);
        }

        [Test]
        public void BestRecord_DevRulesAndNoOutcome_NeverSave()
        {
            var store = new BestRecordStore(TestKey);
            PlayerPrefs.DeleteKey(TestKey);
            var dev = new SimulationRules { RecordBestTime = false };
            Assert.IsFalse(store.TryRecord(new RunOutcome(RunOutcomeKind.Death, 100, 0, 1), dev));
            Assert.IsFalse(store.TryRecord(RunOutcome.None, new SimulationRules()));
            Assert.IsFalse(PlayerPrefs.HasKey(TestKey));
            Assert.Throws<ArgumentNullException>(() => store.TryRecord(new RunOutcome(RunOutcomeKind.Death, 1, 0, 1), null));
        }

        [Test]
        public void BestRecord_ClampsToIntMax_AndKeysAreSeparate()
        {
            Assert.AreEqual("BestSurvivalSteps", BestRecordStore.DefaultKey);
            Assert.AreEqual(BestRecordStore.DefaultKey, new BestRecordStore().Key);
            Assert.Throws<ArgumentException>(() => new BestRecordStore(""));
            Assert.Throws<ArgumentException>(() => new BestRecordStore(null));

            bool realBefore = PlayerPrefs.HasKey(BestRecordStore.DefaultKey);
            int realValue = PlayerPrefs.GetInt(BestRecordStore.DefaultKey, -12345);
            var store = new BestRecordStore(TestKey);
            PlayerPrefs.DeleteKey(TestKey);
            Assert.IsTrue(store.TryRecord(new RunOutcome(RunOutcomeKind.Death, (long)int.MaxValue + 5, 0, 1), new SimulationRules()));
            store.TryGetBest(out int best);
            Assert.AreEqual(int.MaxValue, best);
            Assert.AreEqual(realBefore, PlayerPrefs.HasKey(BestRecordStore.DefaultKey), "the shipping key is untouched");
            Assert.AreEqual(realValue, PlayerPrefs.GetInt(BestRecordStore.DefaultKey, -12345));
        }

        [Test]
        public void ValueTypes_KeepTheirMembers()
        {
            var outcome = new RunOutcome(RunOutcomeKind.Clear, 36001, 512, 9);
            Assert.AreEqual(RunOutcomeKind.Clear, outcome.Kind);
            Assert.AreEqual(36001, outcome.SurvivalSteps);
            Assert.AreEqual(512, outcome.Kills);
            Assert.AreEqual(9, outcome.Level);
            Assert.AreEqual(RunOutcomeKind.None, RunOutcome.None.Kind);
            Assert.AreEqual(RunOutcomeKind.None, default(RunOutcome).Kind);
            CollectionAssert.AreEqual(new[] { "None", "Death", "Clear" }, Enum.GetNames(typeof(RunOutcomeKind)));

            var pool = new Survivor.Tests.EditMode.Player.PlrPool();
            var ticks = new Survivor.Tests.EditMode.Player.PlrTicks();
            var collisions = new Survivor.Tests.EditMode.Player.PlrCollisions();
            var targets = new Survivor.Tests.EditMode.Player.PlrTargets();
            var strategies = new RunStrategies(pool, ticks, collisions, targets);
            Assert.AreSame(pool, strategies.Pool);
            Assert.AreSame(ticks, strategies.Tick);
            Assert.AreSame(collisions, strategies.Collision);
            Assert.AreSame(targets, strategies.Target);
        }
    }
}
