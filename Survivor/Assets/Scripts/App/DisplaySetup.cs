using System.Globalization;
using UnityEngine;

namespace Survivor.App
{
    /// The only caller of Screen.SetResolution (5.18, TAC-F8-5). Called from GameBootstrap.Awake; applies once per process.
    public static class DisplaySetup
    {
        private static bool _applied;

        /// Size chosen by the first Apply (zero before it ran).
        public static Vector2Int AppliedSize { get; private set; }

        /// Reads Screen.mainWindowDisplayInfo.workArea [U35], chooses the window size and sets it windowed. Later calls do nothing
        /// (no settings menu, the size never changes while running).
        public static void Apply()
        {
            if (_applied)
            {
                return;
            }

            _applied = true;
            RectInt workArea = Screen.mainWindowDisplayInfo.workArea;
            Vector2Int size = WindowSizeSelector.Choose(workArea.width, workArea.height);
            AppliedSize = size;
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            AppLog.Info("Window " + size.x.ToString(CultureInfo.InvariantCulture) + "x" + size.y.ToString(CultureInfo.InvariantCulture)
                + " (work area " + workArea.width.ToString(CultureInfo.InvariantCulture) + "x"
                + workArea.height.ToString(CultureInfo.InvariantCulture) + ").");
        }
    }
}
