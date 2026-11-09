using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Level-up choice screen: always three buttons (SPEC F5). Click, Enter on the focused button and keys 1/2/3
    /// (UiInputHandler) all call Choose, which only raises OptionChosen with the round id the view was bound to (5.14 A5);
    /// whether the choice is accepted is decided by LevelUpSession alone (R29).
    public sealed class LevelUpView : MonoBehaviour
    {
        /// Number of option buttons.
        public const int OptionCount = 3;

        private readonly Button[] _buttons = new Button[OptionCount];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[OptionCount];

        /// An option was chosen: (round id the view shows, option index 0..2).
        public event Action<long, int> OptionChosen;

        /// True while the panel is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// Round id of the options on screen (0 before the first Bind).
        public long RoundId { get; private set; }

        /// Builds the hidden level-up panel under parent.
        public static LevelUpView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreatePanel(parent, "LevelUpView", UiFactory.DimColor);
            var view = root.gameObject.AddComponent<LevelUpView>();
            UiFactory.CreateText(root, "Header", "레벨 업! 하나를 고르세요 (1 / 2 / 3)", 56f, TextAlignmentOptions.Center,
                new Vector2(1400f, 90f), new Vector2(0f, 300f));
            for (int i = 0; i < OptionCount; i++)
            {
                int index = i;
                Button button = UiFactory.CreateButton(root, "Option" + (i + 1), string.Empty, new Vector2(1100f, 120f),
                    new Vector2(0f, 130f - 160f * i), () => view.Choose(index));
                view._buttons[i] = button;
                view._labels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
            }

            UiFactory.LinkVertical(view._buttons);
            root.gameObject.SetActive(false);
            return view;
        }

        /// Puts the round's three option labels on the buttons; labels must hold exactly three entries (else ArgumentException).
        public void Bind(long roundId, IReadOnlyList<string> labels)
        {
            if (labels == null || labels.Count != OptionCount)
            {
                throw new ArgumentException("LevelUpView.Bind needs exactly " + OptionCount + " labels.", nameof(labels));
            }

            RoundId = roundId;
            for (int i = 0; i < OptionCount; i++)
            {
                _labels[i].text = (i + 1) + ". " + labels[i];
            }
        }

        /// Label shown on button index (with its "n. " prefix).
        public string LabelOf(int index)
        {
            return _labels[index].text;
        }

        /// Shows the panel and focuses the first option.
        public void Show()
        {
            gameObject.SetActive(true);
            UiFactory.Focus(_buttons[0]);
        }

        /// Hides the panel (the bound round and labels stay).
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Raises OptionChosen(RoundId, index) when the panel is visible; ignored while hidden (e.g. paused, TAC-F5-7).
        public void Choose(int index)
        {
            if (IsVisible)
            {
                OptionChosen?.Invoke(RoundId, index);
            }
        }
    }
}
