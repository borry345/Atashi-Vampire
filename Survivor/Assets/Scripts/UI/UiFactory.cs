using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    /// Builds the uGUI hierarchy in code (5c.1: no prefabs or UXML): the overlay canvas (TAC-F8-7) and the panels, texts and
    /// buttons the views are made of. Texts never set a font: the TMP Settings default font asset (Korean SDF) is used.
    public static class UiFactory
    {
        /// CanvasScaler reference resolution width (D25).
        public const float ReferenceWidth = 1920f;

        /// CanvasScaler reference resolution height (D25).
        public const float ReferenceHeight = 1080f;

        /// CanvasScaler match between width (0) and height (1).
        public const float MatchWidthOrHeight = 0.5f;

        /// Panel background behind full-screen menus.
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);

        /// Box background of menus and dialogs.
        public static readonly Color BoxColor = new Color(0.12f, 0.13f, 0.16f, 0.95f);

        private static readonly Color ButtonColor = new Color(0.24f, 0.27f, 0.33f, 1f);
        private static readonly Color ButtonHighlight = new Color(0.36f, 0.45f, 0.62f, 1f);
        private static readonly Color ButtonPressed = new Color(0.45f, 0.58f, 0.82f, 1f);
        private static readonly Color ButtonDisabled = new Color(0.18f, 0.18f, 0.2f, 0.8f);

        /// Screen Space Overlay canvas with CanvasScaler (Scale With Screen Size, 1920x1080, match 0.5) and GraphicRaycaster.
        public static Canvas CreateCanvas(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// Empty full-stretch container under parent.
        public static RectTransform CreateLayer(Transform parent, string name)
        {
            var rect = NewRect(parent, name);
            Stretch(rect);
            return rect;
        }

        /// Full-stretch image under parent (a raycast target, so it blocks clicks to what is behind it).
        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateLayer(parent, name);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        /// Image of size at anchoredPosition, anchored at the parent's center.
        public static RectTransform CreateBox(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var rect = NewRect(parent, name);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        /// TMP text of size at anchoredPosition (center anchor); the font is the TMP Settings default.
        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment,
                                                 Vector2 size, Vector2 anchoredPosition)
        {
            var rect = NewRect(parent, name);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        /// TMP text anchored to the parent's top-left corner at offset (x right, y down), for HUD lines.
        public static TextMeshProUGUI CreateCornerText(Transform parent, string name, float fontSize, Vector2 size, Vector2 offset)
        {
            TextMeshProUGUI label = CreateText(parent, name, string.Empty, fontSize, TextAlignmentOptions.TopLeft, size, Vector2.zero);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(offset.x, -offset.y);
            return label;
        }

        /// Button of size at anchoredPosition with a centered label; onClick is the single callback for mouse click and
        /// EventSystem Submit (Enter), TAC-F8-12. Null onClick throws ArgumentNullException.
        public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchoredPosition, Action onClick)
        {
            if (onClick == null)
            {
                throw new ArgumentNullException(nameof(onClick));
            }

            RectTransform rect = CreateBox(parent, name, size, anchoredPosition, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI text = CreateText(rect, "Label", label, 36f, TextAlignmentOptions.Center, size, Vector2.zero);
            Stretch(text.rectTransform);
            return button;
        }

        /// Explicit up/down navigation through items in list order, wrapping at both ends (arrow keys, TAC-F8-12).
        public static void LinkVertical(IReadOnlyList<Selectable> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            for (int i = 0; i < items.Count; i++)
            {
                Navigation navigation = items[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = items[(i - 1 + items.Count) % items.Count];
                navigation.selectOnDown = items[(i + 1) % items.Count];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                items[i].navigation = navigation;
            }
        }

        /// Makes target the EventSystem selection when an EventSystem exists and target is interactable.
        public static void Focus(Selectable target)
        {
            UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
            if (current != null && target != null && target.IsInteractable())
            {
                current.SetSelectedGameObject(target.gameObject);
            }
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
