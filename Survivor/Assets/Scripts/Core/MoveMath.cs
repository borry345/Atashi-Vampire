using System;
using UnityEngine;

namespace Survivor.Core
{
    /// Eight-way movement directions from a constant table (rule R17).
    public static class MoveMath
    {
        /// Diagonal component as a literal so every platform gets identical bits (never computed with Normalize).
        public const float Diagonal = 0.70710677f;

        // Indexed by (y + 1) * 3 + (x + 1).
        private static readonly Vector2[] Directions =
        {
            new Vector2(-Diagonal, -Diagonal), new Vector2(0f, -1f), new Vector2(Diagonal, -Diagonal),
            new Vector2(-1f, 0f),              new Vector2(0f, 0f),  new Vector2(1f, 0f),
            new Vector2(-Diagonal, Diagonal),  new Vector2(0f, 1f),  new Vector2(Diagonal, Diagonal),
        };

        /// Unit direction for an input (x, y) in {-1, 0, 1}^2; (0, 0) gives the zero vector. Throws ArgumentOutOfRangeException otherwise.
        public static Vector2 ToUnitDirection(int x, int y)
        {
            if (x < -1 || x > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "x must be -1, 0 or 1.");
            }

            if (y < -1 || y > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "y must be -1, 0 or 1.");
            }

            return Directions[(y + 1) * 3 + (x + 1)];
        }

        /// Returns v unchanged, or Vector2.right when v is exactly the zero vector.
        public static Vector2 DirectionOrDefault(Vector2 v)
        {
            return v.x == 0f && v.y == 0f ? Vector2.right : v;
        }
    }
}
