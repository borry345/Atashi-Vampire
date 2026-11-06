using System;
using UnityEngine;

namespace Survivor.Core
{
    /// Spawn placement and despawn distance rules (R7, R18, R26).
    public static class SpawnGeometry
    {
        private const float TwoPi = 2f * Mathf.PI;

        // N at and below which the default despawn distance applies (R26).
        private const int DespawnReferenceCount = 400;

        /// R7: theta = u01 * 2pi, center + radius * (cos theta, sin theta); one draw, no retry. u01 must be in [0, 1].
        public static Vector2 PointOnCircle(Vector2 center, float radius, float u01)
        {
            if (!(u01 >= 0f && u01 <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(u01), u01, "u01 must be in [0, 1].");
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be a finite, non-negative number.");
            }

            float theta = u01 * TwoPi;
            return center + radius * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
        }

        /// R18: (pos - player).sqrMagnitude > d^2 (strictly beyond). Bosses are excluded by the caller.
        public static bool IsBeyondDespawn(Vector2 pos, Vector2 player, float d)
        {
            return (pos - player).sqrMagnitude > d * d;
        }

        /// R26: N <= 400 -> 45, N > 400 -> 45 * sqrt(N / 400) (float). Throws ArgumentOutOfRangeException for negative N.
        public static float DespawnDistanceFor(int n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n), n, "Monster count must be non-negative.");
            }

            if (n <= DespawnReferenceCount)
            {
                return GameRules.DefaultDespawnDistance;
            }

            return GameRules.DefaultDespawnDistance * Mathf.Sqrt(n / (float)DespawnReferenceCount);
        }
    }
}
