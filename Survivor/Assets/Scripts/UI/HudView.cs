using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Simulation;
using TMPro;
using UnityEngine;

namespace Survivor.UI
{
    /// In-run HUD: game time (R2, StepMath.FormatGameTime), level, health and experience at the top left, and the loadout panel
    /// (owned weapons, passives, monster health multiplier, 5.17.1) at the bottom left. Texts change only when a value changes.
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
        private TextMeshProUGUI _loadout;
        private int _shownLoadoutVersion = int.MinValue;
        private float _shownHpMultiplier = float.NaN;

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

        /// Current loadout panel text (HudLoadoutFormatter.Format).
        public string LoadoutText => _loadout.text;

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
            // Bottom left, at most 8 lines (4 weapons + 3 passives + multiplier): y 24..464 from the bottom of the 1080 reference,
            // clear of the four top-left lines (y 24..244 from the top).
            view._loadout = UiFactory.CreateBottomLeftText(root, "Loadout", 32f, new Vector2(600f, 440f), new Vector2(32f, 24f));
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

        /// Rebuilds the loadout panel only when loadoutVersion (PlayerProgressionApplier.ChangeCount) or hpMultiplier differs from
        /// what is shown; otherwise builds no string. Reads only; null lists throw ArgumentNullException.
        public void RefreshLoadout(int loadoutVersion, IReadOnlyList<WeaponInstance> weapons, IReadOnlyList<PassiveInstance> passives, float hpMultiplier)
        {
            if (loadoutVersion == _shownLoadoutVersion && hpMultiplier == _shownHpMultiplier)
            {
                return;
            }

            _loadout.text = HudLoadoutFormatter.Format(weapons, passives, hpMultiplier);
            _shownLoadoutVersion = loadoutVersion;
            _shownHpMultiplier = hpMultiplier;
        }
    }
}
