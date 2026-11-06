using System;
using System.Globalization;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    // "[HH:mm:ss.fff] [LEVEL] [Loop] message" (5c.3 #8). Never call this once per step: logging would skew measurements.
    internal static class LoopLog
    {
        private const string Area = "Loop";

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

        // Driver exception line (5c.3 #7): "[time] [ERROR] [Loop] phase=<phase> step=<n> <Type>: <message>".
        internal static void PhaseError(SimulationPhase phase, long step, Exception exception)
        {
            Error("phase=" + phase + " step=" + step.ToString(CultureInfo.InvariantCulture) + " "
                + exception.GetType().Name + ": " + exception.Message);
        }

        private static string Format(string level, string message)
        {
            // Wall-clock time is used only for the log prefix, never for simulation state (TAC-0-2).
            string time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return "[" + time + "] [" + level + "] [" + Area + "] " + message;
        }
    }
}
