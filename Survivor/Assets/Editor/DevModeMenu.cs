using UnityEditor;

namespace Survivor.Editor
{
    /// "Survivor/Dev Mode On Play" checkbox: when checked, entering Play mode in the editor attaches dev mode as if --dev
    /// were given (DevModeActivation reads the same EditorPrefs key).
    public static class DevModeMenu
    {
        /// EditorPrefs key; the same literal as DevModeActivation.EditorPrefsKey (this assembly may not reference Survivor.Dev).
        public const string PrefsKey = "Survivor.DevModeOnPlay";

        private const string MenuPath = "Survivor/Dev Mode On Play";

        /// Flips the toggle.
        [MenuItem(MenuPath, false, 100)]
        public static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(PrefsKey, false);
            EditorPrefs.SetBool(PrefsKey, on);
            Menu.SetChecked(MenuPath, on);
        }

        /// Keeps the check mark in sync with the stored value.
        [MenuItem(MenuPath, true)]
        public static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefsKey, false));
            return true;
        }
    }
}
