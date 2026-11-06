using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    // Straight-line per-step movement shared by monsters and attracted gems.
    internal static class StepMove
    {
        // Moves from toward target by at most distance; lands exactly on target instead of overshooting (no jitter around it).
        internal static Vector2 Toward(Vector2 from, Vector2 target, float distance)
        {
            Vector2 delta = target - from;
            float sq = delta.sqrMagnitude;
            if (sq == 0f)
            {
                return from;
            }

            if (sq <= distance * distance)
            {
                return target;
            }

            return from + delta * (distance / Mathf.Sqrt(sq));
        }

        // Unit vector from -> target; +x when both points coincide (MoveMath.DirectionOrDefault convention).
        internal static Vector2 UnitDirection(Vector2 from, Vector2 target)
        {
            Vector2 delta = MoveMath.DirectionOrDefault(target - from);
            return delta / Mathf.Sqrt(delta.sqrMagnitude);
        }
    }
}
