using System;

namespace Survivor.Dev
{
    /// Decides once at startup whether dev mode is requested (TAC-F19-1, SPEC F19 "one argument check").
    public static class DevModeActivation
    {
        /// Command line switch that turns dev mode on (case-insensitive).
        public const string Argument = "--dev";

        /// EditorPrefs key of the "Survivor/Dev Mode On Play" menu toggle (editor only; same literal as DevModeMenu).
        public const string EditorPrefsKey = "Survivor.DevModeOnPlay";

        /// True when args contain --dev (case-insensitive) or, in the editor, the Dev Mode On Play toggle is set.
        public static bool IsRequested(string[] args)
        {
            if (HasDevArgument(args))
            {
                return true;
            }

#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false);
#else
            return false;
#endif
        }

        /// True when args contain --dev, ignoring case; null or empty args give false.
        public static bool HasDevArgument(string[] args)
        {
            if (args == null)
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], Argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
