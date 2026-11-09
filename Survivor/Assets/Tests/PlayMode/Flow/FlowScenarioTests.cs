using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// The screen flow in Main.unity (5.17, TAC-F5-4/5/7, TAC-F8-1..4/9/12, 5c.5 "흐름"): title -> start -> three consecutive
    /// level-up rounds without a step -> ESC keeps the round -> choose -> death -> result + best record -> restart resets
    /// everything -> back to title; plus clear, no-record paths, focus loss, a runtime abort and unplayable data. Batch mode
    /// has no keyboard, so inputs go through the views' public methods and GameFlowController.TogglePause (ESC).
    public sealed class FlowScenarioTests : FlowPmFixture
    {
        private const ulong Seed = 20261109UL;

        // thresholds [5, 8, 12]: 25 xp = exactly three levels (2, 3, 4).
        private const int ThreeLevelsXp = 25;

        [UnityTest]
        public IEnumerator Title_Start_ThreeRounds_PauseKeepsRound_Death_Result_Restart_Reset_Title()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            var ended = new List<RunOutcome>();
            Host.RunEnded += outcome => ended.Add(outcome);

            // Title -> start.
            Assert.AreEqual(FlowState.Title, Flow.State);
            Flow.Title.RequestStart();
            Assert.AreEqual(FlowState.Playing, Flow.State);
            Assert.IsTrue(Flow.Hud.IsVisible);
            Assert.AreEqual("시간 00:00", Flow.Hud.TimeText);
            Host.Commands.Enqueue(PendingCommand.AddXp(ThreeLevelsXp));

            // Round 1 opens after step 0 (gate closed at s + 1 = 1), in the frame the flow saw the close.
            yield return WaitFor(() => Flow.State == FlowState.LevelUp, "level-up round 1");
            LevelUpSession round = Host.Session.LevelUpSession;
            Assert.AreEqual(1, Clock.Step, "closed after step 0 (TAC-0-7: s + 1)");
            Assert.AreEqual(GateCloseReason.LevelUp, Clock.CloseReason);
            Assert.AreEqual(1L, round.RoundId);
            Assert.IsTrue(round.IsOpen);
            Assert.AreEqual(3, round.Options.Count);
            Assert.AreEqual(2, Host.Session.Experience.Level);
            Assert.IsTrue(Flow.LevelUp.IsVisible);
            Assert.AreEqual(1L, Flow.LevelUp.RoundId);
            Assert.AreEqual(Time.frameCount, round.OpenedFrame, "the round opened in this frame");

            // An input in the frame the round opened is refused (TAC-F5-5: frame > OpenedFrame).
            Flow.LevelUp.Choose(0);
            Assert.AreEqual(1L, round.RoundId);
            Assert.IsTrue(round.IsOpen, "same-frame input refused");
            Assert.AreEqual(FlowState.LevelUp, Flow.State);

            // ESC on the level-up screen -> pause; the round, its three options and the step are kept (TAC-F5-7).
            List<UpgradeOption> shown = round.Options.ToList();
            Flow.TogglePause();
            Assert.AreEqual(FlowState.Paused, Flow.State);
            Assert.AreEqual(FlowState.LevelUp, Flow.ResumeState);
            Assert.IsTrue(Flow.Pause.IsVisible);
            Assert.IsFalse(Flow.LevelUp.IsVisible);
            yield return Frames(3);
            Flow.LevelUp.Choose(1);
            Assert.IsTrue(round.IsOpen, "choices are ignored while paused");
            Assert.AreEqual(1, Clock.Step);
            Flow.Pause.RequestResume();
            Assert.AreEqual(FlowState.LevelUp, Flow.State);
            Assert.IsTrue(Flow.LevelUp.IsVisible);
            Assert.AreEqual(1L, round.RoundId, "same round after resume");
            CollectionAssert.AreEqual(shown, round.Options.ToList(), "same three options, no new draw");

            // Choose -> applied without a step, round 2 opens in the same call; a second input in that frame is refused.
            yield return null;
            Flow.LevelUp.Choose(0);
            Assert.AreEqual(2L, round.RoundId, "round 2 opened in the same Update (TAC-F5-4 step 4)");
            Assert.IsTrue(round.IsOpen);
            Assert.AreEqual(1, Clock.Step, "no step between rounds");
            Assert.AreEqual(3, Host.Session.Experience.Level);
            Flow.LevelUp.Choose(1);
            Assert.AreEqual(2L, round.RoundId, "the frame that opened round 2 cannot answer it");
            yield return null;
            Flow.LevelUp.Choose(0);
            Assert.AreEqual(3L, round.RoundId);
            Assert.AreEqual(4, Host.Session.Experience.Level);
            yield return null;
            Flow.LevelUp.Choose(0);
            Assert.IsFalse(round.IsOpen);
            Assert.AreEqual(FlowState.Playing, Flow.State);
            Assert.IsFalse(Flow.LevelUp.IsVisible);
            Assert.IsTrue(Clock.IsRunning);
            Assert.AreEqual(1, Clock.Step, "three rounds, still step 1 (TAC-F5-4)");

            // Play on, then die through SetHealth(0).
            yield return WaitFor(() => Clock.Step >= 120, "two seconds of play");
            Assert.AreEqual("레벨 4", Flow.Hud.LevelText);
            Host.Commands.Enqueue(PendingCommand.SetHealth(0f));
            yield return WaitFor(() => Flow.State == FlowState.Result, "result after death");
            RunOutcome outcome = Flow.LastOutcome;
            Assert.AreEqual(RunOutcomeKind.Death, outcome.Kind);
            Assert.AreEqual(Clock.Step, outcome.SurvivalSteps, "survival = completed steps including the death step");
            Assert.AreEqual(GateCloseReason.Death, Clock.CloseReason);
            Assert.AreEqual(4, outcome.Level);
            Assert.AreEqual(1, ended.Count, "RunEnded once");
            Assert.AreEqual(outcome.SurvivalSteps, ended[0].SurvivalSteps);
            Assert.IsTrue(Flow.Result.IsVisible);
            Assert.AreEqual("게임 오버", Flow.Result.HeaderText);
            Assert.AreEqual("생존 시간 " + StepMath.FormatGameTime(outcome.SurvivalSteps), Flow.Result.SurvivalText);
            Assert.AreEqual("처치 수 0", Flow.Result.KillsText);
            Assert.AreEqual("도달 레벨 4", Flow.Result.LevelText);
            Assert.AreEqual((int)outcome.SurvivalSteps, PlayerPrefs.GetInt(RecordKey, -1), "first result records the best (TAC-F8-3)");
            Assert.AreEqual("최고 기록 " + StepMath.FormatGameTime(outcome.SurvivalSteps), Flow.Result.BestText);
            int record = PlayerPrefs.GetInt(RecordKey);

            // Restart -> every run record is reset (TAC-F8-2, 5c.4 wave 4 list) before the first step.
            Flow.Result.RequestRestart();
            Assert.AreEqual(FlowState.Playing, Flow.State);
            AssertFreshRun(Seed);
            yield return WaitFor(() => Clock.Step >= 30, "the restarted run steps");

            // Pause -> back to title: reset without opening and without recording.
            Flow.TogglePause();
            Flow.Pause.RequestExitToTitle();
            Assert.AreEqual(FlowState.Title, Flow.State);
            Assert.IsTrue(Flow.Title.IsVisible);
            Assert.IsFalse(Clock.IsRunning);
            Assert.AreEqual(GateCloseReason.RunReset, Clock.CloseReason);
            Assert.AreEqual(0, Clock.Step);
            Assert.AreEqual(record, PlayerPrefs.GetInt(RecordKey), "back to title records nothing");
            Assert.AreEqual(1, ended.Count);
        }

        [UnityTest]
        public IEnumerator BackToTitle_WithoutAResult_StoresNoRecord_AndResultWithoutRecordShowsDash()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Flow.StartRun();
            yield return WaitFor(() => Clock.Step >= 60, "a second of play");
            Flow.TogglePause();
            Flow.Pause.RequestExitToTitle();
            Assert.IsFalse(PlayerPrefs.HasKey(RecordKey), "TAC-F8-3: back to title is not a run end");

            // Dev-style run (RecordBestTime = false): death shows the result with "-" and stores nothing.
            Host.Rules.RecordBestTime = false;
            Flow.Title.RequestStart();
            Host.Commands.Enqueue(PendingCommand.SetHealth(0f));
            yield return WaitFor(() => Flow.State == FlowState.Result, "result");
            Assert.AreEqual("최고 기록 " + ResultView_NoRecord, Flow.Result.BestText);
            Assert.IsFalse(PlayerPrefs.HasKey(RecordKey));

            // Result "타이틀" -> reset only.
            Flow.Result.RequestTitle();
            Assert.AreEqual(FlowState.Title, Flow.State);
            Assert.IsFalse(Clock.IsRunning);
        }

        [UnityTest]
        public IEnumerator BossKilled_IsAClear_WithTheKillStepAsSurvival()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Flow.StartRun();
            Host.Commands.Enqueue(PendingCommand.SpawnBossNow("M4", new Vector2(12f, 0f)));
            yield return WaitFor(() => Host.BossSpawnedThisRun, "boss spawned");
            Flow.TogglePause();
            Monster boss = Live<Monster>().Single(m => m.IsBoss);
            boss.ApplyDamage(boss.Hp + 1f, Clock.Step);
            Flow.TogglePause();
            yield return WaitFor(() => Flow.State == FlowState.Result, "result after the boss kill");
            Assert.AreEqual(RunOutcomeKind.Clear, Flow.LastOutcome.Kind);
            Assert.AreEqual(GateCloseReason.Clear, Clock.CloseReason);
            Assert.AreEqual(Clock.Step, Flow.LastOutcome.SurvivalSteps);
            Assert.GreaterOrEqual(Flow.LastOutcome.Kills, 1, "the boss (and any summons W1 killed)");
            Assert.AreEqual("클리어", Flow.Result.HeaderText);
            Assert.AreEqual((int)Flow.LastOutcome.SurvivalSteps, PlayerPrefs.GetInt(RecordKey, -1));
        }

        [UnityTest]
        public IEnumerator FocusLoss_PausesWhilePlaying_AndNeverResumesByItself()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Flow.StartRun();
            yield return Frames(2);
            Flow.HandleFocusChanged(false);
            Assert.AreEqual(FlowState.Paused, Flow.State, "TAC-F8-9");
            Assert.IsFalse(Clock.IsRunning);
            Assert.AreEqual(GateCloseReason.Paused, Clock.CloseReason);
            Flow.HandleFocusChanged(true);
            yield return Frames(2);
            Assert.AreEqual(FlowState.Paused, Flow.State, "no automatic resume");
            Flow.Pause.RequestResume();
            Assert.AreEqual(FlowState.Playing, Flow.State);
            Assert.IsTrue(Clock.IsRunning);

            Host.Rules.AutoPauseOnFocusLoss = false;
            Flow.HandleFocusChanged(false);
            Assert.AreEqual(FlowState.Playing, Flow.State, "measurement mode keeps running");
        }

        [UnityTest]
        public IEnumerator StepException_ShowsTheErrorPanel_NotTheResult_AndRecordsNothing()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Flow.StartRun();
            yield return WaitFor(() => Clock.Step >= 10, "ten steps");
            LogAssert.Expect(LogType.Error, new Regex(@"\[ERROR\] \[Loop\] phase=Begin step=\d+ ArgumentException"));
            Host.Commands.Enqueue(PendingCommand.SetLoadout("not a loadout"));
            yield return WaitFor(() => Flow.State == FlowState.DataError, "error panel");
            Assert.AreEqual(GateCloseReason.Error, Clock.CloseReason);
            Assert.IsTrue(Flow.ShowsRuntimeError);
            Assert.IsTrue(Flow.Error.IsVisible);
            Assert.IsTrue(Flow.Error.TitleButtonVisible);
            Assert.IsFalse(Flow.Result.IsVisible, "an aborted step is not a result");
            Assert.IsFalse(PlayerPrefs.HasKey(RecordKey));
            long step = Clock.Step;
            yield return Frames(3);
            Assert.AreEqual(step, Clock.Step, "the aborted step was not counted and nothing runs");
            Flow.Error.RequestTitle();
            Assert.AreEqual(FlowState.Title, Flow.State);
            Assert.IsFalse(Flow.ShowsRuntimeError);
            Assert.IsTrue(Flow.Title.StartEnabled);
        }

        [UnityTest]
        public IEnumerator UnplayableData_DisablesStart_AndListsTheBlockingIssues()
        {
            string root = Path.Combine(Application.temporaryCachePath, "FlowPmNoData");
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            LogAssert.ignoreFailingMessages = true;
            GameBootstrap.DataRootOverride = root;
            yield return LoadMain();
            Assert.AreEqual(FlowState.DataError, Flow.State, "TAC-F8-4");
            Assert.IsTrue(Flow.Title.IsVisible);
            Assert.IsFalse(Flow.Title.StartEnabled);
            Assert.IsTrue(Flow.Error.IsVisible);
            Assert.IsFalse(Flow.Error.TitleButtonVisible);
            Assert.Greater(Flow.Error.IssueCount, 0);
            StringAssert.Contains("playerStats.json", Flow.Error.BodyText, "file names are listed");
            Flow.Title.RequestStart();
            Flow.StartRun();
            Assert.AreEqual(FlowState.DataError, Flow.State, "start is refused");
            Assert.IsFalse(Clock.IsRunning);
            yield return Frames(3);
            Assert.AreEqual(0, Clock.Step);
        }

        private const string ResultView_NoRecord = Survivor.UI.ResultView.NoRecord;

        // The 4a reset list plus the 5c.4 wave 4 candidates, read before the first step of a restarted run.
        private void AssertFreshRun(ulong seed)
        {
            RunSession session = Host.Session;
            Assert.AreEqual(0, Clock.Step);
            Assert.IsTrue(Clock.IsRunning, "the flow opened the gate");
            Assert.AreEqual(0, Host.Lifecycle.ActiveCount);
            Assert.AreEqual(0, Host.Lifecycle.Pool.ActiveCount, "every pooled object went back");
            Assert.AreEqual(new EntityHandle(1, 1), Host.Player.Body.Id, "player body 1:1");
            Assert.AreEqual(new EntityHandle(2, 1), Host.Player.MagnetBody.Id, "player magnet 2:1");
            Assert.AreEqual(Vector2.zero, Host.Player.Position);
            Assert.AreEqual(1, session.Weapons.Count);
            Assert.AreEqual("W1", session.Weapons.Slots[0].Id);
            Assert.AreEqual(1, session.Weapons.Slots[0].Level);
            Assert.AreEqual(0, session.Weapons.Slots[0].NextFireStep);
            Assert.AreEqual(0, session.Passives.Count);
            Assert.AreEqual(session.Health.MaxHp, session.Health.Hp);
            Assert.AreEqual(0, Host.DamageReceiver.OverlapCount);
            Assert.AreEqual(0, Host.Pickups.AttractedGemCount);
            Assert.AreEqual(0, Host.PickupRegistry.ActiveGemCount);
            Assert.AreEqual(0, session.KillPipeline.KillCount);
            Assert.IsFalse(Host.Spawner.BossSpawnedThisRun);
            Assert.AreEqual(0, Host.Spawner.LiveNonBossCount);
            Assert.AreEqual(0L, session.LevelUpSession.RoundId);
            Assert.IsFalse(session.LevelUpSession.IsOpen);
            Assert.AreEqual(1, session.Experience.Level);
            Assert.AreEqual(0, session.Experience.Xp);
            Assert.AreEqual(0, Host.Commands.PendingCount);
            Assert.AreEqual(0, Host.CommandBuffer.PendingCount);
            Assert.AreEqual(RunOutcomeKind.None, session.OutcomeGate.PendingOutcome.Kind);
            Assert.AreEqual(seed, session.Rng.MasterSeed);
            Assert.IsTrue(Flow.Hud.IsVisible);
            Assert.IsFalse(Flow.Result.IsVisible);
        }
    }
}
