using UnityEngine;

namespace Survivor.Core
{
    /// One monster's frozen state for this step.
    public struct MonsterSnapshot
    {
        /// Monster id.
        public EntityHandle Id;

        /// Position at BuildIndex.
        public Vector2 Position;

        /// Collision radius.
        public float Radius;

        /// (rev 3) MonsterSizeRules.IsLarge(Radius, isBoss): kept out of the spatial hash.
        public bool IsLarge;
    }
}
