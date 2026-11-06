using System;
using UnityEngine;

namespace Survivor.Core
{
    /// Circle helpers shared by every collision and visual sizing path (rules R4, R5).
    public static class CircleMath
    {
        /// R5: (b - a).sqrMagnitude <= (ra + rb)^2; touching counts as overlapping, no square root.
        public static bool Overlaps(Vector2 a, float ra, Vector2 b, float rb)
        {
            float radiusSum = ra + rb;
            return (b - a).sqrMagnitude <= radiusSum * radiusSum;
        }

        /// R4: uniform scale that makes the sprite's larger world-space side equal to the diameter 2r.
        public static float VisualScaleFor(Sprite sprite, float radius)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be a finite, non-negative number.");
            }

            Vector3 size = sprite.bounds.size;
            float side = Mathf.Max(size.x, size.y);
            if (side <= 0f)
            {
                throw new ArgumentException("Sprite has an empty bounds size.", nameof(sprite));
            }

            return 2f * radius / side;
        }
    }
}
