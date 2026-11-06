using UnityEngine;

namespace Survivor.Simulation
{
    /// Experience gem dropped by a killed monster. No lifetime and no despawn rule (SPEC F4); attracted by the player's magnet
    /// and collected on body contact by PickupSystem.
    public sealed class ExperienceGem : SimEntity
    {
        /// Experience granted on pickup, fixed at drop time.
        public int XpValue { get; private set; }

        /// True once the magnet touched the gem; never reverts (Exit is ignored).
        public bool IsAttracted { get; internal set; }

        // Spawn initialize callback.
        internal void Initialize(int xpValue, float radius)
        {
            XpValue = xpValue;
            IsAttracted = false;
            Body.Radius = radius;
            Body.IsLarge = false;
        }

        internal void MoveTo(Vector2 position)
        {
            Position = position;
        }

        /// Clears the pickup state before the instance returns to the pool.
        protected override void OnReleased()
        {
            XpValue = 0;
            IsAttracted = false;
        }
    }
}
