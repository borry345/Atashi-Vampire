using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Pause menu: "재개" and "타이틀로 나가기" only, no settings (SPEC F8). Raises events only.
    public sealed class PauseMenuView : MonoBehaviour
    {
        private Button _resume;
        private Button _exit;

        /// "재개" was chosen.
        public event Action ResumeRequested;

        /// "타이틀로 나가기" was chosen.
        public event Action ExitToTitleRequested;

        /// True while the panel is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// Builds the hidden pause panel under parent.
        public static PauseMenuView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreatePanel(parent, "PauseMenuView", UiFactory.DimColor);
            var view = root.gameObject.AddComponent<PauseMenuView>();
            UiFactory.CreateBox(root, "Box", new Vector2(700f, 520f), Vector2.zero, UiFactory.BoxColor);
            UiFactory.CreateText(root, "Header", "일시정지", 72f, TextAlignmentOptions.Center, new Vector2(600f, 110f), new Vector2(0f, 150f));
            view._resume = UiFactory.CreateButton(root, "ResumeButton", "재개", new Vector2(480f, 90f), new Vector2(0f, 0f),
                view.RequestResume);
            view._exit = UiFactory.CreateButton(root, "ExitButton", "타이틀로 나가기", new Vector2(480f, 90f), new Vector2(0f, -120f),
                view.RequestExitToTitle);
            UiFactory.LinkVertical(new Selectable[] { view._resume, view._exit });
            root.gameObject.SetActive(false);
            return view;
        }

        /// Shows the panel and focuses "재개".
        public void Show()
        {
            gameObject.SetActive(true);
            UiFactory.Focus(_resume);
        }

        /// Hides the panel.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Same as choosing "재개": raises ResumeRequested when visible.
        public void RequestResume()
        {
            if (IsVisible)
            {
                ResumeRequested?.Invoke();
            }
        }

        /// Same as choosing "타이틀로 나가기": raises ExitToTitleRequested when visible.
        public void RequestExitToTitle()
        {
            if (IsVisible)
            {
                ExitToTitleRequested?.Invoke();
            }
        }
    }
}
