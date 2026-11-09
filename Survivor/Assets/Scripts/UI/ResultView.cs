using System;
using System.Globalization;
using Survivor.Core;
using Survivor.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Result screen (SPEC F8): survival time, kills, level reached and best survival time ("-" when none), then "재시작" or "타이틀".
    public sealed class ResultView : MonoBehaviour
    {
        /// Shown in the best-record line when nothing has been recorded yet.
        public const string NoRecord = "-";

        private TextMeshProUGUI _header;
        private TextMeshProUGUI _survival;
        private TextMeshProUGUI _kills;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _best;
        private Button _restart;
        private Button _title;

        /// "재시작" was chosen.
        public event Action RestartRequested;

        /// "타이틀" was chosen.
        public event Action TitleRequested;

        /// True while the panel is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// Header text ("게임 오버" or "클리어").
        public string HeaderText => _header.text;

        /// Survival line, e.g. "생존 시간 02:31".
        public string SurvivalText => _survival.text;

        /// Kill line, e.g. "처치 수 120".
        public string KillsText => _kills.text;

        /// Level line, e.g. "도달 레벨 7".
        public string LevelText => _level.text;

        /// Best-record line, e.g. "최고 기록 03:10" or "최고 기록 -".
        public string BestText => _best.text;

        /// Builds the hidden result panel under parent.
        public static ResultView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreatePanel(parent, "ResultView", UiFactory.DimColor);
            var view = root.gameObject.AddComponent<ResultView>();
            UiFactory.CreateBox(root, "Box", new Vector2(900f, 820f), Vector2.zero, UiFactory.BoxColor);
            var line = new Vector2(760f, 64f);
            view._header = UiFactory.CreateText(root, "Header", string.Empty, 80f, TextAlignmentOptions.Center, new Vector2(800f, 120f),
                new Vector2(0f, 300f));
            view._survival = UiFactory.CreateText(root, "Survival", string.Empty, 44f, TextAlignmentOptions.Left, line, new Vector2(0f, 170f));
            view._kills = UiFactory.CreateText(root, "Kills", string.Empty, 44f, TextAlignmentOptions.Left, line, new Vector2(0f, 100f));
            view._level = UiFactory.CreateText(root, "Level", string.Empty, 44f, TextAlignmentOptions.Left, line, new Vector2(0f, 30f));
            view._best = UiFactory.CreateText(root, "Best", string.Empty, 44f, TextAlignmentOptions.Left, line, new Vector2(0f, -40f));
            view._restart = UiFactory.CreateButton(root, "RestartButton", "재시작", new Vector2(420f, 90f), new Vector2(0f, -170f),
                view.RequestRestart);
            view._title = UiFactory.CreateButton(root, "TitleButton", "타이틀", new Vector2(420f, 90f), new Vector2(0f, -290f),
                view.RequestTitle);
            UiFactory.LinkVertical(new Selectable[] { view._restart, view._title });
            root.gameObject.SetActive(false);
            return view;
        }

        /// Fills the lines from outcome and the best survival steps (null = no record yet, shown as "-").
        public void Bind(in RunOutcome outcome, long? bestSurvivalSteps)
        {
            _header.text = outcome.Kind == RunOutcomeKind.Clear ? "클리어" : "게임 오버";
            _survival.text = "생존 시간 " + StepMath.FormatGameTime(outcome.SurvivalSteps);
            _kills.text = "처치 수 " + outcome.Kills.ToString(CultureInfo.InvariantCulture);
            _level.text = "도달 레벨 " + outcome.Level.ToString(CultureInfo.InvariantCulture);
            _best.text = "최고 기록 " + (bestSurvivalSteps.HasValue ? StepMath.FormatGameTime(bestSurvivalSteps.Value) : NoRecord);
        }

        /// Shows the panel and focuses "재시작".
        public void Show()
        {
            gameObject.SetActive(true);
            UiFactory.Focus(_restart);
        }

        /// Hides the panel.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Same as choosing "재시작": raises RestartRequested when visible.
        public void RequestRestart()
        {
            if (IsVisible)
            {
                RestartRequested?.Invoke();
            }
        }

        /// Same as choosing "타이틀": raises TitleRequested when visible.
        public void RequestTitle()
        {
            if (IsVisible)
            {
                TitleRequested?.Invoke();
            }
        }
    }
}
