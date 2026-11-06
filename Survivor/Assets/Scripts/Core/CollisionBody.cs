using UnityEngine;

namespace Survivor.Core
{
    /// Mutable circle owned by an entity; strategies read Position/Radius during CollectEvents.
    public sealed class CollisionBody
    {
        /// Owner entity id.
        public EntityHandle Id;

        /// Collision layer of the body.
        public CollisionLayer Layer;

        /// Center in world units.
        public Vector2 Position;

        /// Radius in world units.
        public float Radius;

        /// (rev 3) MonsterSizeRules.IsLarge(Radius, isBoss) for monsters, false otherwise.
        public bool IsLarge;

        /// PhysicsBodySynchronizer toggles CircleCollider2D/Rigidbody2D on it.
        public GameObject Host;

        /// Private to the active collision strategy.
        public int StrategySlot = -1;

        /// (rev 2) Private to PhysicsBodySynchronizer.
        public int PhysicsSlot = -1;

        /// Set by PhysicsBodySynchronizer when engine simulation owns the transform.
        public bool DrivesTransform;
    }
}
