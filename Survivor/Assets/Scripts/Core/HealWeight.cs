using System;
using UnityEngine;

namespace Survivor.Core
{
    /// Level-up heal candidate weight (rule R8).
    public static class HealWeight
    {
        /// r = clamp01(hp / maxHp), returns wBase * (1 - r)^2. Callers drop the candidate when the result is <= 0. Throws if maxHp <= 0.
        public static float Compute(float wBase, float hp, float maxHp)
        {
            if (!(maxHp > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "maxHp must be positive.");
            }

            float r = Mathf.Clamp01(hp / maxHp);
            float missing = 1f - r;
            return wBase * missing * missing;
        }
    }
}
