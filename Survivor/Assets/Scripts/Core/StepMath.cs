using System;
using System.Globalization;
using UnityEngine;

namespace Survivor.Core
{
    /// The only conversion point between seconds and fixed steps (rules R1, R2, R3) plus the step clock constants.
    public static class StepMath
    {
        /// Fixed steps per game second.
        public const int StepsPerSecond = 60;

        /// Length of one fixed step in seconds (1/60).
        public const float StepSeconds = 1f / 60f;

        /// Maximum steps run in one frame to catch up (maximumDeltaTime = MaxCatchUpSteps * StepSeconds at 1x).
        public const int MaxCatchUpSteps = 3;

        /// R1: round(seconds * 60, AwayFromZero), 0 allowed (invulnerability, spawn start/end); throws on negative, NaN, infinity or int overflow.
        public static int SecondsToSteps(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Seconds must be a finite, non-negative number.");
            }

            double steps = Math.Round(seconds * StepsPerSecond, MidpointRounding.AwayFromZero);
            if (steps > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Seconds are too large to express as an int step count.");
            }

            return (int)steps;
        }

        /// R1: like SecondsToSteps but at least 1 (cooldowns, lifetimes, intervals, durations).
        public static int SecondsToIntervalSteps(double seconds)
        {
            return Math.Max(1, SecondsToSteps(seconds));
        }

        /// R2: game seconds = completed steps / 60.
        public static double StepsToSeconds(long steps)
        {
            return steps / (double)StepsPerSecond;
        }

        /// R2: formats completed steps as "mm:ss" with seconds floored; minutes keep growing past 99. Throws on negative steps.
        public static string FormatGameTime(long steps)
        {
            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), steps, "Steps must be non-negative.");
            }

            long totalSeconds = steps / StepsPerSecond;
            long minutes = totalSeconds / 60;
            long seconds = totalSeconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        /// R3: units per second -> units per step (value / 60f).
        public static float PerSecondToPerStep(float valuePerSecond)
        {
            return valuePerSecond / 60f;
        }

        /// R3: degrees per second -> radians per step (deg * pi / 180 / 60).
        public static float DegPerSecondToRadPerStep(float degreesPerSecond)
        {
            return degreesPerSecond * Mathf.PI / 180f / 60f;
        }
    }
}
