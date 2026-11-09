using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Survivor.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Error panel. Data mode (TAC-F8-4): shown over the title with start disabled, listing the blocking issues with file name,
    /// definition id and field. Runtime mode (a step aborted, 5c.3 #7): the run stopped without a result, with "타이틀로".
    public sealed class DataErrorView : MonoBehaviour
    {
        /// Blocking issues listed before the "외 N건" line.
        public const int MaxListedIssues = 10;

        private TextMeshProUGUI _header;
        private TextMeshProUGUI _body;
        private Button _title;

        /// "타이틀로" was chosen (runtime mode only).
        public event Action TitleRequested;

        /// True while the panel is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// Number of issues the latest ShowDataIssues received (0 in runtime mode).
        public int IssueCount { get; private set; }

        /// Body text currently shown.
        public string BodyText => _body.text;

        /// True when the "타이틀로" button is offered (runtime mode).
        public bool TitleButtonVisible => _title.gameObject.activeSelf;

        /// Builds the hidden error panel under parent (right half of the screen, so the title stays visible).
        public static DataErrorView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreateLayer(parent, "DataErrorView");
            var view = root.gameObject.AddComponent<DataErrorView>();
            UiFactory.CreateBox(root, "Box", new Vector2(1500f, 760f), new Vector2(0f, -120f), new Color(0.25f, 0.05f, 0.05f, 0.95f));
            view._header = UiFactory.CreateText(root, "Header", string.Empty, 48f, TextAlignmentOptions.Center, new Vector2(1400f, 80f),
                new Vector2(0f, 200f));
            view._body = UiFactory.CreateText(root, "Body", string.Empty, 28f, TextAlignmentOptions.TopLeft, new Vector2(1400f, 520f),
                new Vector2(0f, -110f));
            view._title = UiFactory.CreateButton(root, "TitleButton", "타이틀로", new Vector2(420f, 80f), new Vector2(0f, -440f),
                view.RequestTitle);
            root.gameObject.SetActive(false);
            return view;
        }

        /// Data mode: lists issues (Blocking ones, in order) as "파일 / 정의 / 필드: 메시지", no "타이틀로" button.
        public void ShowDataIssues(IEnumerable<ValidationIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var text = new StringBuilder();
            int count = 0;
            foreach (ValidationIssue issue in issues)
            {
                count++;
                if (count <= MaxListedIssues)
                {
                    text.Append("- ").Append(Describe(issue)).Append('\n');
                }
            }

            if (count > MaxListedIssues)
            {
                text.Append("... 외 ").Append((count - MaxListedIssues).ToString(CultureInfo.InvariantCulture)).Append("건 (로그 참조)");
            }

            IssueCount = count;
            _header.text = "데이터 오류로 게임을 시작할 수 없습니다";
            _body.text = text.ToString();
            _title.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        /// Runtime mode: shows message with a "타이틀로" button (the run ends without a result or best record).
        public void ShowRuntimeError(string message)
        {
            IssueCount = 0;
            _header.text = "오류로 게임이 중단되었습니다";
            _body.text = message ?? string.Empty;
            _title.gameObject.SetActive(true);
            gameObject.SetActive(true);
            UiFactory.Focus(_title);
        }

        /// Hides the panel.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Same as choosing "타이틀로": raises TitleRequested when visible in runtime mode.
        public void RequestTitle()
        {
            if (IsVisible && _title.gameObject.activeSelf)
            {
                TitleRequested?.Invoke();
            }
        }

        private static string Describe(ValidationIssue issue)
        {
            string file = string.IsNullOrEmpty(issue.FileName) ? "(파일 없음)" : issue.FileName;
            if (issue.Line > 0)
            {
                file += ":" + issue.Line.ToString(CultureInfo.InvariantCulture);
            }

            string definition = string.IsNullOrEmpty(issue.DefinitionId) ? "-" : issue.DefinitionId;
            string field = string.IsNullOrEmpty(issue.FieldPath) ? "-" : issue.FieldPath;
            return file + " / " + definition + " / " + field + ": " + issue.Message;
        }
    }
}
