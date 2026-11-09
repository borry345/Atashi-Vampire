using System.Globalization;
using Survivor.Core;
using TMPro;
using UnityEngine;

namespace Survivor.UI
{
    /// In-run HUD: game time (R2, StepMath.FormatGameTime), level, health and experience. Texts change only when a value changes.
    public sealed class HudView : MonoBehaviour
    {
        private TextMeshProUGUI _time;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _health;
        private TextMeshProUGUI _experience;
        private long _shownStep = -1;
        private int _shownLevel = -1;
        private int _shownHp = -1;
        private int _shownMaxHp = -1;
        private int _shownXp = -1;
        private int _shownRequired = -1;

        /// True while the HUD is shown.
        public bool IsVisible => gameObject.activeSelf;

        /// Current time text, e.g. "시간 01:05".
        public string TimeText => _time.text;

        /// Current level text, e.g. "레벨 3".
        public string LevelText => _level.text;

        /// Current health text, e.g. "체력 80 / 100".
        public string HealthText => _health.text;

        /// Current experience text, e.g. "경험치 4 / 12".
        public string ExperienceText => _experience.text;

        /// Builds the hidden HUD under parent.
        public static HudView Create(Transform parent)
        {
            RectTransform root = UiFactory.CreateLayer(parent, "HudView");
            var view = root.gameObject.AddComponent<HudView>();
            var size = new Vector2(600f, 56f);
            view._time = UiFactory.CreateCornerText(root, "Time", 44f, size, new Vector2(32f, 24f));
            view._level = UiFactory.CreateCornerText(root, "Level", 36f, size, new Vector2(32f, 84f));
            view._health = UiFactory.CreateCornerText(root, "Health", 36f, size, new Vector2(32f, 136f));
            view._experience = UiFactory.CreateCornerText(root, "Experience", 36f, size, new Vector2(32f, 188f));
            view.Refresh(0, 1, 0f, 0f, 0, 0);
            root.gameObject.SetActive(false);
            return view;
        }

        /// Shows the HUD.
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// Hides the HUD.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// Updates the four lines; health is shown rounded up so a living player never reads 0.
        public void Refresh(long step, int level, float hp, float maxHp, int xp, int requiredXp)
        {
            if (step != _shownStep)
            {
                _shownStep = step;
                _time.text = "시간 " + StepMath.FormatGameTime(step);
            }

            if (level != _shownLevel)
            {
                _shownLevel = level;
                _level.text = "레벨 " + level.ToString(CultureInfo.InvariantCulture);
            }

            int shownHp = Mathf.CeilToInt(hp);
            int shownMaxHp = Mathf.CeilToInt(maxHp);
            if (shownHp != _shownHp || shownMaxHp != _shownMaxHp)
            {
                _shownHp = shownHp;
                _shownMaxHp = shownMaxHp;
                _health.text = "체력 " + shownHp.ToString(CultureInfo.InvariantCulture) + " / " + shownMaxHp.ToString(CultureInfo.InvariantCulture);
            }

            if (xp != _shownXp || requiredXp != _shownRequired)
            {
                _shownXp = xp;
                _shownRequired = requiredXp;
                _experience.text = "경험치 " + xp.ToString(CultureInfo.InvariantCulture) + " / " + requiredXp.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
