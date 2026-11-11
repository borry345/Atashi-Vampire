using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    // Straight-line per-step movement shared by monsters and attracted gems (MoveStopAtContact is monster-only, R32).
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

        // Ratio search budget: the step doubles every try (1, 2, 4, ... ulps of the ratio), so 32 tries always reach 1 from any
        // non-negative ratio (the bit pattern of 1f is below 2^30).
        private const int MaxRatioSearchSteps = 32;
        private static readonly int OneBits = BitConverter.SingleToInt32Bits(1f);

        // R32: moves from by displacement but stops at the first point of the segment on the contact circle (center player,
        // radius monsterRadius + playerRadius): the first intersection ratio is found in double, then raised in whole ulps of
        // the ratio (doubling steps) until R5 CircleMath.Overlaps holds at the float position. Already touching (R5) or zero
        // displacement: stays at from (no retreat, no push). A move that misses the circle or ends before it is from + displacement.
        internal static Vector2 MoveStopAtContact(Vector2 from, Vector2 displacement, Vector2 player, float monsterRadius, float playerRadius)
        {
            if (CircleMath.Overlaps(from, monsterRadius, player, playerRadius) || displacement == Vector2.zero)
            {
                return from;
            }

            float radiusSum = monsterRadius + playerRadius;
            double fx = (double)from.x - player.x;
            double fy = (double)from.y - player.y;
            double dx = displacement.x;
            double dy = displacement.y;
            double a = dx * dx + dy * dy;
            double b = fx * dx + fy * dy;
            double c = fx * fx + fy * fy - (double)radiusSum * radiusSum;
            double disc = b * b - a * c;
            if (b >= 0d || disc < 0d)
            {
                return from + displacement;
            }

            double t = (-b - Math.Sqrt(disc)) / a;
            if (double.IsNaN(t))
            {
                Debug.Assert(false, "StepMove.MoveStopAtContact: non-finite input.");
                return from;
            }

            if (t >= 1d)
            {
                return from + displacement;
            }

            // R5 is false at from, so a ratio at or below 0 only comes from rounding; the search then starts at 0.
            float s = t > 0d ? (float)t : 0f;
            long step = 1;
            for (int i = 0; i < MaxRatioSearchSteps; i++)
            {
                Vector2 p = from + displacement * s;
                if (CircleMath.Overlaps(p, monsterRadius, player, playerRadius))
                {
                    return p;
                }

                long next = BitConverter.SingleToInt32Bits(s) + step;
                if (next >= OneBits)
                {
                    return from + displacement;
                }

                s = BitConverter.Int32BitsToSingle((int)next);
                step *= 2;
            }

            Debug.Assert(false, "StepMove.MoveStopAtContact: no touching point found (non-finite input?).");
            return from;
        }

        // Unit vector from -> target; +x when both points coincide (MoveMath.DirectionOrDefault convention).
        internal static Vector2 UnitDirection(Vector2 from, Vector2 target)
        {
            Vector2 delta = MoveMath.DirectionOrDefault(target - from);
            return delta / Mathf.Sqrt(delta.sqrMagnitude);
        }
    }
}
