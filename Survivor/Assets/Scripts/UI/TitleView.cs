using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Title screen: "시작" and "종료" (SPEC F8). Raises events only; the flow controller (App) decides what they do.
    public sealed class TitleView : MonoBehaviour
    {
        private Button _start;
        private Button _quit;

        /// "시작" was chosen (click or Enter on the focused button).
        public event Action StartRequested;

        /// "종료" was chosen.
        public event Action QuitRequested;

        /// True while the panel is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// True when "시작" can be chosen (false while the data is not playable, TAC-F8-4).
        public bool StartEnabled => _start != null && _start.interactable;

        /// Builds the hidden title panel under parent.
        public static TitleView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreatePanel(parent, "TitleView", new Color(0.06f, 0.07f, 0.09f, 1f));
            var view = root.gameObject.AddComponent<TitleView>();
            UiFactory.CreateText(root, "Title", "SURVIVOR", 120f, TextAlignmentOptions.Center, new Vector2(1200f, 180f), new Vector2(0f, 260f));
            UiFactory.CreateText(root, "Subtitle", "살아남아 보스를 쓰러뜨리세요", 40f, TextAlignmentOptions.Center,
                new Vector2(1200f, 70f), new Vector2(0f, 140f));
            view._start = UiFactory.CreateButton(root, "StartButton", "시작", new Vector2(420f, 90f), new Vector2(0f, -40f), view.RequestStart);
            view._quit = UiFactory.CreateButton(root, "QuitButton", "종료", new Vector2(420f, 90f), new Vector2(0f, -160f), view.RequestQuit);
            UiFactory.LinkVertical(new Selectable[] { view._start, view._quit });
            root.gameObject.SetActive(false);
            return view;
        }

        /// Enables or disables "시작".
        public void SetStartEnabled(bool enabled)
        {
            _start.interactable = enabled;
        }

        /// Shows the panel and focuses "시작" (or "종료" when start is disabled).
        public void Show()
        {
            gameObject.SetActive(true);
            UiFactory.Focus(_start.interactable ? _start : _quit);
        }

        /// Hides the panel.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Same as choosing "시작": raises StartRequested when visible and enabled.
        public void RequestStart()
        {
            if (IsVisible && _start.interactable)
            {
                StartRequested?.Invoke();
            }
        }

        /// Same as choosing "종료": raises QuitRequested when visible.
        public void RequestQuit()
        {
            if (IsVisible)
            {
                QuitRequested?.Invoke();
            }
        }
    }
}
