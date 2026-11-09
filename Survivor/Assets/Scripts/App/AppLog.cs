using System;
using System.Globalization;
using UnityEngine;

namespace Survivor.App
{
    // "[HH:mm:ss.fff] [LEVEL] [Flow] message" (5c.3 #8, Area = Flow). Run start/end, level-up, strategy and screen changes only;
    // never once per step or per frame (measurement contamination).
    internal static class AppLog
    {
        private const string Area = "Flow";

        internal static void Info(string message)
        {
            Debug.Log(Format("INFO", message));
        }

        internal static void Warning(string message)
        {
            Debug.LogWarning(Format("WARN", message));
        }

        internal static void Error(string message)
        {
            Debug.LogError(Format("ERROR", message));
        }

        private static string Format(string level, string message)
        {
            // Wall-clock time is used only for the log prefix, never for simulation state (TAC-0-2).
            string time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return "[" + time + "] [" + level + "] [" + Area + "] " + message;
        }
    }
}
