using System;
using System.Globalization;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Collision area log lines in the shared format "[HH:mm:ss.fff] [LEVEL] [Collision] message" (5c.3 #8). Never per step.
    internal static class CollisionLog
    {
        /// Logs an error line and returns the contract-violation exception for the caller to throw (5c.3 #7).
        internal static InvalidOperationException Fail(string message)
        {
            Debug.LogError($"[{Stamp()}] [ERROR] [Collision] {message}");
            return new InvalidOperationException(message);
        }

        /// Logs a warning line.
        internal static void Warn(string message)
        {
            Debug.LogWarning($"[{Stamp()}] [WARN] [Collision] {message}");
        }

        // DateTime.Now is log-only (TAC-0-2 exception).
        private static string Stamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }
}
