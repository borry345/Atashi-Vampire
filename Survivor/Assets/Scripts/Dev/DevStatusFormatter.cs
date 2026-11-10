using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// Builds the status panel lines (5.15.4, TAC-F19-12): English fixed labels, InvariantCulture numbers, data strings (display
    /// names) as they are. Pure: no Unity object access, no simulation writes.
    public static class DevStatusFormatter
    {
        /// HP multiplier line while the spawn table has no multiplier API (wave 5a).
        public const string HpMultiplierUnavailable = "HP mult: n/a";

        /// Clears linesOut, then appends: time and step, seed, monsters and gems, strategies, speed, HP multiplier, flow state,
        /// "Weapons:" with one line per owned weapon, "Passives:" with one line per owned passive. Null linesOut throws.
        public static void Format(in DevStatusInput input, List<string> linesOut)
        {
            if (linesOut == null)
            {
                throw new ArgumentNullException(nameof(linesOut));
            }

            CultureInfo c = CultureInfo.InvariantCulture;
            linesOut.Clear();
            linesOut.Add("Time " + StepMath.FormatGameTime(Math.Max(0L, input.Step)) + "  Step " + input.Step.ToString(c));
            linesOut.Add("Seed " + input.Seed.ToString(c) + (input.SeedFixed ? " (fixed)" : " (random)"));
            linesOut.Add("Monsters " + input.LiveMonsters.ToString(c) + "  Gems " + input.ActiveGems.ToString(c));
            linesOut.Add("Pool " + input.Pool + "  Target " + input.Target + "  Tick " + input.Tick + "  Collision " + input.Collision
                + "  CCD " + input.Ccd + "  Cell " + input.CellSize.ToString("F2", c));
            linesOut.Add("Speed x" + input.TimeScale.ToString("F2", c) + " (actual x" + input.ActualSpeed.ToString("F2", c) + ")");
            linesOut.Add(input.HpMultiplier.HasValue ? "HP mult: x" + input.HpMultiplier.Value.ToString("F1", c) : HpMultiplierUnavailable);
            linesOut.Add("State " + input.State + "  Gate " + (input.GateRunning ? "open" : "closed (" + input.CloseReason + ")"));

            linesOut.Add("Weapons:");
            int owned = 0;
            if (input.Weapons != null)
            {
                for (int i = 0; i < input.Weapons.Count; i++)
                {
                    WeaponInstance weapon = input.Weapons[i];
                    if (weapon == null)
                    {
                        continue;
                    }

                    linesOut.Add(WeaponLine(weapon.Definition.DisplayName, weapon.Id, weapon.Level, weapon.Definition.EvolvedOnly));
                    owned++;
                }
            }

            if (owned == 0)
            {
                linesOut.Add("  (none)");
            }

            linesOut.Add("Passives:");
            owned = 0;
            if (input.Passives != null)
            {
                for (int i = 0; i < input.Passives.Count; i++)
                {
                    PassiveInstance passive = input.Passives[i];
                    if (passive == null)
                    {
                        continue;
                    }

                    linesOut.Add(PassiveLine(passive.Definition.DisplayName, passive.Id, passive.Level));
                    owned++;
                }
            }

            if (owned == 0)
            {
                linesOut.Add("  (none)");
            }
        }

        // "  <displayName> Lv <n>", or "  <displayName> (evolved)" for an evolution-only weapon (5.15.4); the id stands in for a
        // missing display name.
        internal static string WeaponLine(string displayName, string id, int level, bool evolved)
        {
            string name = string.IsNullOrEmpty(displayName) ? id : displayName;
            return evolved ? "  " + name + " (evolved)" : "  " + name + " Lv " + level.ToString(CultureInfo.InvariantCulture);
        }

        // "  <displayName> Lv <n>".
        internal static string PassiveLine(string displayName, string id, int level)
        {
            string name = string.IsNullOrEmpty(displayName) ? id : displayName;
            return "  " + name + " Lv " + level.ToString(CultureInfo.InvariantCulture);
        }
    }
}
