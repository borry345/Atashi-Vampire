using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Survivor.Simulation;

namespace Survivor.UI
{
    /// Text of the HUD loadout panel (5.17.1, SPEC F8 12~14): owned weapons in slot order, then owned passives, then the monster
    /// health multiplier line. The single owner of these strings; the dev status panel has its own English formatter.
    public static class HudLoadoutFormatter
    {
        /// Line of one weapon: "<name> Lv n", or "<name> (진화)" for an evolved weapon (no level).
        public static string WeaponLine(string displayName, int level, bool evolved)
        {
            return evolved ? displayName + " (진화)" : displayName + " Lv " + level.ToString(CultureInfo.InvariantCulture);
        }

        /// Line of one passive: "<name> Lv n".
        public static string PassiveLine(string displayName, int level)
        {
            return displayName + " Lv " + level.ToString(CultureInfo.InvariantCulture);
        }

        /// Multiplier line, one decimal with a '.' in every culture. ASCII 'x' because the font atlas has no U+00D7 (21st PM decision).
        public static string HpMultiplierLine(float hpMultiplier)
        {
            return "몬스터 체력 x" + hpMultiplier.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// Whole panel text, lines joined by '\n'; null slots are skipped and the multiplier line is always last. Null lists throw
        /// ArgumentNullException.
        public static string Format(IReadOnlyList<WeaponInstance> weapons, IReadOnlyList<PassiveInstance> passives, float hpMultiplier)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (passives == null)
            {
                throw new ArgumentNullException(nameof(passives));
            }

            var text = new StringBuilder();
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInstance weapon = weapons[i];
                if (weapon != null)
                {
                    text.Append(WeaponLine(weapon.Definition.DisplayName, weapon.Level, weapon.Definition.EvolvedOnly)).Append('\n');
                }
            }

            for (int i = 0; i < passives.Count; i++)
            {
                PassiveInstance passive = passives[i];
                if (passive != null)
                {
                    text.Append(PassiveLine(passive.Definition.DisplayName, passive.Level)).Append('\n');
                }
            }

            return text.Append(HpMultiplierLine(hpMultiplier)).ToString();
        }
    }
}
