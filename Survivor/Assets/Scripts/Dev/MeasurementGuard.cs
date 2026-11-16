using System.Globalization;

namespace Survivor.Dev
{
    /// Start refusal rules of a measurement (5.15.2 step 2, TAC-F19-5): any visualization on, the collision log option on,
    /// time scale not 1, one-step mode, or a preset without a fixed loadout / with a chest drop chance other than 0. The camera
    /// zoom is not a condition (it is reset right after the guard passes). Pure: the caller passes the current values.
    public static class MeasurementGuard
    {
        /// Null when a measurement may start, otherwise the first failing condition's cause (English, shown in the overlay).
        /// Conditions are checked in the order above; a null preset counts as "no fixed loadout".
        public static string Check(bool anyVisualizationOn, bool collisionLogOn, float timeScale, bool singleStepMode,
                                   MeasurementPreset preset)
        {
            if (anyVisualizationOn)
            {
                return "Visualization is on (turn every visualizer off).";
            }

            if (collisionLogOn)
            {
                return "Collision log is on (turn it off).";
            }

            if (timeScale != 1f)
            {
                return "Time scale is x" + timeScale.ToString("0.##", CultureInfo.InvariantCulture) + " (set it to 1).";
            }

            if (singleStepMode)
            {
                return "One-step mode is active (Resume first).";
            }

            if (preset == null || preset.WeaponLoadout == null || preset.WeaponLoadout.Count == 0)
            {
                return "The preset has no fixed weapon loadout.";
            }

            if (preset.ChestDropChance != 0f)
            {
                return "The preset's chest drop chance is not 0.";
            }

            return null;
        }
    }
}
