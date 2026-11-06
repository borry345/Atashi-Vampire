using System;
using System.Globalization;
using UnityEngine;

namespace Survivor.Data
{
    /// Writes "[HH:mm:ss.fff] [LEVEL] [Data] message" console lines (5c.3 #8).
    internal static class DataLog
    {
        private const string Area = "Data";

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
