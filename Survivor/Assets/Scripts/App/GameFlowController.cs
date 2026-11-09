using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.UI;
using UnityEngine;

namespace Survivor.App
{
    /// Screen flow (5.17, TAC-F8-1): polls clock.CloseSequence in Update and switches Title / Playing / Paused / LevelUp / Result /
    /// DataError. LevelUp opens a round (TAC-F5-4); Death and Clear go to the result screen and are the only path that records
    /// the best time (TAC-F8-3); Error shows the error panel; StopAtStep and SingleStepDone are left to dev mode; Paused and
    /// RunReset are closes this class made itself. The views only raise events (UI never references App); this class subscribes.
    public sealed class GameFlowController : MonoBehaviour
    {
        private readonly List<(UpgradeOption option, float weight)> _candidates = new List<(UpgradeOption option, float weight)>();
        private readonly List<UpgradeOption> _options = new List<UpgradeOption>(LevelUpView.OptionCount);
        private readonly List<string> _labels = new List<string>(LevelUpView.OptionCount);

        private GameBootstrap _host;
        private TitleView _title;
        private HudView _hud;
        private LevelUpView _levelUp;
        private PauseMenuView _pause;
        private ResultView _result;
        private DataErrorView _error;
        private BestRecordStore _bestRecord;
        private int _seenCloseSequence;
        private bool _runtimeError;

        /// Current screen state.
        public FlowState State { get; private set; } = FlowState.Title;

        /// State the pause menu returns to (Playing or LevelUp); meaningful while State == Paused.
        public FlowState ResumeState { get; private set; } = FlowState.Playing;

        /// True while the error panel shows an aborted step (State == DataError with playable data).
        public bool ShowsRuntimeError => _runtimeError;

        /// Outcome of the latest finished run (RunOutcome.None before the first one).
        public RunOutcome LastOutcome { get; private set; } = RunOutcome.None;

        internal TitleView Title => _title;
        internal HudView Hud => _hud;
        internal LevelUpView LevelUp => _levelUp;
        internal PauseMenuView Pause => _pause;
        internal ResultView Result => _result;
        internal DataErrorView Error => _error;
        internal BestRecordStore BestRecord => _bestRecord;

        // Composition root only: binds the host and the views, subscribes to every view event and shows the first screen
        // (Title, or DataError when the boot data is not playable, TAC-F8-4).
        internal void Initialize(GameBootstrap host, TitleView title, HudView hud, LevelUpView levelUp, PauseMenuView pause,
                                 ResultView result, DataErrorView error, UiInputHandler input, BestRecordStore bestRecord)
        {
            _host = host ? host : throw new ArgumentNullException(nameof(host));
            _title = title ? title : throw new ArgumentNullException(nameof(title));
            _hud = hud ? hud : throw new ArgumentNullException(nameof(hud));
            _levelUp = levelUp ? levelUp : throw new ArgumentNullException(nameof(levelUp));
            _pause = pause ? pause : throw new ArgumentNullException(nameof(pause));
            _result = result ? result : throw new ArgumentNullException(nameof(result));
            _error = error ? error : throw new ArgumentNullException(nameof(error));
            _bestRecord = bestRecord ?? throw new ArgumentNullException(nameof(bestRecord));
            if (!input)
            {
                throw new ArgumentNullException(nameof(input));
            }

            _title.StartRequested += StartRun;
            _title.QuitRequested += Quit;
            _pause.ResumeRequested += TogglePause;
            _pause.ExitToTitleRequested += ExitToTitle;
            _result.RestartRequested += StartRun;
            _result.TitleRequested += ExitToTitle;
            _error.TitleRequested += ExitToTitle;
            _levelUp.OptionChosen += OnOptionChosen;
            input.EscapePressed += TogglePause;
            _seenCloseSequence = _host.Clock.CloseSequence;

            if (_host.IsDataPlayable)
            {
                EnterTitle();
            }
            else
            {
                EnterDataError();
            }
        }

        /// Starts a run (title "시작", result "재시작", dev StartNewRun): reset with the next selection, HUD, then open the gate.
        /// Refused with a warning while the data is not playable.
        public void StartRun()
        {
            if (!_host.IsDataPlayable)
            {
                AppLog.Warning("StartRun refused: the data is not playable.");
                return;
            }

            _host.ResetRun();
            _seenCloseSequence = _host.Clock.CloseSequence;
            HideAll();
            _runtimeError = false;
            _hud.Show();
            RefreshHud();
            State = FlowState.Playing;
            _host.Clock.Open();
            AppLog.Info("Run started (step 0).");
        }

        /// "타이틀로 나가기" / result "타이틀" / error "타이틀로": resets the run without opening the gate and without recording.
        public void ExitToTitle()
        {
            if (!_host.IsDataPlayable)
            {
                return;
            }

            _host.ResetRun();
            _seenCloseSequence = _host.Clock.CloseSequence;
            _runtimeError = false;
            EnterTitle();
            AppLog.Info("Back to title (run reset, nothing recorded).");
        }

        /// ESC (TAC-F8-12, TAC-F5-7): Playing -> Paused (returns to Playing, gate closed with Paused); LevelUp -> Paused (returns to
        /// LevelUp, the round stays open and choices are ignored); Paused -> the return state (Playing reopens the gate). Other
        /// states ignore it. A gate close not yet seen by Update is handled first.
        public void TogglePause()
        {
            PollGate();
            switch (State)
            {
                case FlowState.Playing:
                    _host.Clock.Close(GateCloseReason.Paused);
                    _seenCloseSequence = _host.Clock.CloseSequence;
                    EnterPaused(FlowState.Playing);
                    break;
                case FlowState.LevelUp:
                    EnterPaused(FlowState.LevelUp);
                    break;
                case FlowState.Paused:
                    Resume();
                    break;
            }
        }

        /// Title "종료": quits the application (no record is written here, TAC-F8-3).
        public void Quit()
        {
            AppLog.Info("Quit requested.");
            Application.Quit();
        }

        // TAC-F8-9: focus loss while playing pauses when the rules allow it; focus return never resumes.
        internal void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus && State == FlowState.Playing && _host.Rules.AutoPauseOnFocusLoss)
            {
                TogglePause();
                AppLog.Info("Paused on focus loss.");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_host != null)
            {
                HandleFocusChanged(hasFocus);
            }
        }

        private void Update()
        {
            if (_host == null)
            {
                return;
            }

            PollGate();
            if (_hud.IsVisible)
            {
                RefreshHud();
            }
        }

        // Handles at most one new close per call (no step can run between a close and the next Open).
        private void PollGate()
        {
            SimulationClock clock = _host.Clock;
            if (clock.CloseSequence == _seenCloseSequence)
            {
                return;
            }

            _seenCloseSequence = clock.CloseSequence;
            switch (clock.CloseReason)
            {
                case GateCloseReason.LevelUp:
                    if (State == FlowState.Playing)
                    {
                        OpenRound();
                    }

                    break;
                case GateCloseReason.Death:
                case GateCloseReason.Clear:
                    if (State == FlowState.Playing)
                    {
                        EnterResult();
                    }

                    break;
                case GateCloseReason.Error:
                    EnterRuntimeError();
                    break;
            }
        }

        // TAC-F5-4 step 2: candidates from the state at this moment, drawn from the LevelUp stream, round opened at this frame.
        private void OpenRound()
        {
            RunSession session = _host.Session;
            GameData data = _host.Data.Current;
            LevelUpCandidateBuilder.Build(data, session.Weapons, session.Passives, session.Health.Hp, session.Health.MaxHp, _candidates);
            LevelUpSelector.Draw(_candidates, session.Rng.Get(RngStream.LevelUp), _options);
            LevelUpSession round = session.LevelUpSession;
            round.Open(_options, Time.frameCount);
            _labels.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                _labels.Add(Describe(_options[i], data, session));
            }

            _levelUp.Bind(round.RoundId, _labels);
            _levelUp.Show();
            State = FlowState.LevelUp;
            AppLog.Info("Level-up round " + round.RoundId.ToString(CultureInfo.InvariantCulture) + " opened at step "
                + _host.Clock.Step.ToString(CultureInfo.InvariantCulture) + " (level " + session.Experience.Level.ToString(CultureInfo.InvariantCulture)
                + "): " + string.Join(" | ", _labels) + ".");
        }

        // TAC-F5-4 steps 3-4 / TAC-F5-5: accepted only by LevelUpSession (first valid submission, frame > OpenedFrame); then
        // close the round, apply without a step, re-check, and either open the next round in this same call or resume.
        private void OnOptionChosen(long roundId, int index)
        {
            if (State != FlowState.LevelUp)
            {
                return;
            }

            RunSession session = _host.Session;
            LevelUpSession round = session.LevelUpSession;
            if (!round.TrySubmit(roundId, index, Time.frameCount, out UpgradeOption chosen))
            {
                return;
            }

            round.Close();
            _host.Applier.ApplyLevelUpOption(chosen);
            AppLog.Info("Level-up round " + roundId.ToString(CultureInfo.InvariantCulture) + ": chose " + chosen.Kind + " "
                + chosen.TargetId + ".");
            if (_host.Rules.LevelUpEnabled && session.Experience.TryBeginLevelUp(_host.Data.Current.PlayerStats.XpCurve))
            {
                OpenRound();
                return;
            }

            _levelUp.Hide();
            State = FlowState.Playing;
            _host.Clock.Open();
        }

        // Death / Clear (TAC-F8-3): the gate's outcome, the best record (this is the only recording call), the result screen,
        // then RunEnded.
        private void EnterResult()
        {
            RunOutcome outcome = _host.Session.OutcomeGate.PendingOutcome;
            bool saved = _bestRecord.TryRecord(in outcome, _host.Rules);
            long? best = _bestRecord.TryGetBest(out int bestSteps) ? bestSteps : (long?)null;
            LastOutcome = outcome;
            HideAll();
            _result.Bind(in outcome, best);
            _result.Show();
            State = FlowState.Result;
            AppLog.Info("Run ended: " + outcome.Kind + ", survival " + outcome.SurvivalSteps.ToString(CultureInfo.InvariantCulture)
                + " steps, kills " + outcome.Kills.ToString(CultureInfo.InvariantCulture) + ", level "
                + outcome.Level.ToString(CultureInfo.InvariantCulture) + (saved ? ", new best saved." : "."));
            _host.NotifyRunEnded(in outcome);
        }

        // A driver aborted a step (5c.3 #7): error panel, not the result screen, nothing recorded.
        private void EnterRuntimeError()
        {
            HideAll();
            _runtimeError = true;
            _error.ShowRuntimeError("단계 실행 중 예외가 발생해 판을 중단했습니다. 로그의 [ERROR] [Loop] 줄을 확인하세요.\n"
                + "이 판은 기록되지 않습니다.");
            State = FlowState.DataError;
            AppLog.Warning("Step aborted at step " + _host.Clock.Step.ToString(CultureInfo.InvariantCulture) + "; showing the error panel.");
        }

        private void EnterTitle()
        {
            HideAll();
            _title.SetStartEnabled(true);
            _title.Show();
            State = FlowState.Title;
        }

        private void EnterDataError()
        {
            HideAll();
            _title.SetStartEnabled(false);
            _title.Show();
            _error.ShowDataIssues(_host.Data.LastReport.BlockingIssues);
            State = FlowState.DataError;
            AppLog.Warning("Data is not playable (" + _host.Data.LastReport.BlockingCount.ToString(CultureInfo.InvariantCulture)
                + " blocking issue(s)); start is disabled.");
        }

        private void EnterPaused(FlowState resumeState)
        {
            ResumeState = resumeState;
            _levelUp.Hide();
            _pause.Show();
            State = FlowState.Paused;
        }

        // Pause -> the state it came from. LevelUp only shows the same round again (no Open, no new draw, TAC-F5-7).
        private void Resume()
        {
            _pause.Hide();
            if (ResumeState == FlowState.LevelUp)
            {
                _levelUp.Show();
                State = FlowState.LevelUp;
                return;
            }

            State = FlowState.Playing;
            _host.Clock.Open();
        }

        private void HideAll()
        {
            _title.Hide();
            _hud.Hide();
            _levelUp.Hide();
            _pause.Hide();
            _result.Hide();
            _error.Hide();
        }

        private void RefreshHud()
        {
            RunSession session = _host.Session;
            GameData data = _host.Data.Current;
            int level = session.Experience.Level;
            int required = data != null && data.PlayerStats != null ? XpCurve.RequiredForNext(level, data.PlayerStats.XpCurve) : 0;
            _hud.Refresh(_host.Clock.Step, level, session.Health.Hp, session.Health.MaxHp, session.Experience.Xp, required);
        }

        private static string Describe(in UpgradeOption option, GameData data, RunSession session)
        {
            switch (option.Kind)
            {
                case UpgradeKind.WeaponNew:
                    return "새 무기: " + WeaponName(option.TargetId, data);
                case UpgradeKind.WeaponUpgrade:
                {
                    int index = session.Weapons.IndexOf(option.TargetId);
                    int level = index >= 0 && session.Weapons.TryGetSlot(index, out WeaponInstance weapon) ? weapon.Level : 0;
                    return "무기 강화: " + WeaponName(option.TargetId, data) + " (Lv " + level.ToString(CultureInfo.InvariantCulture)
                        + " -> " + (level + 1).ToString(CultureInfo.InvariantCulture) + ")";
                }
                case UpgradeKind.PassiveNew:
                    return "새 패시브: " + PassiveName(option.TargetId, data);
                case UpgradeKind.PassiveUpgrade:
                {
                    int level = session.Passives.Level(option.TargetId);
                    return "패시브 강화: " + PassiveName(option.TargetId, data) + " (Lv " + level.ToString(CultureInfo.InvariantCulture)
                        + " -> " + (level + 1).ToString(CultureInfo.InvariantCulture) + ")";
                }
                case UpgradeKind.Heal:
                    return "회복: 최대 체력의 " + Mathf.RoundToInt(GameRules.HealRatio * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                default:
                    return option.Kind + " " + option.TargetId;
            }
        }

        private static string WeaponName(string id, GameData data)
        {
            return data.TryGetWeapon(id, out WeaponDefinition def) && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName + " (" + id + ")" : id;
        }

        private static string PassiveName(string id, GameData data)
        {
            return data.TryGetPassive(id, out PassiveDefinition def) && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName + " (" + id + ")" : id;
        }
    }
}
