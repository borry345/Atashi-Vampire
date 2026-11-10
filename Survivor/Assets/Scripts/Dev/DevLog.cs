using System;
using System.Globalization;
using UnityEngine;

namespace Survivor.Dev
{
    // "[HH:mm:ss.fff] [LEVEL] [Dev] message" (5c.3 #8, Area = Dev). Attach, run starts and user actions only; never per frame.
    internal static class DevLog
    {
        private const string Area = "Dev";

        internal static void Info(string message)
        {
            Debug.Log(Format("INFO", message));
        }

        internal static void Warning(string message)
        {
            Debug.LogWarning(Format("WARN", message));
        }

        private static string Format(string level, string message)
        {
            // Wall-clock time is used only for the log prefix, never for simulation state (TAC-0-2).
            string time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return "[" + time + "] [" + level + "] [" + Area + "] " + message;
        }
    }
}
