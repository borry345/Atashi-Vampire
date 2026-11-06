using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Base component for pooled simulation entities (no FixedUpdate here); spawned and despawned only by EntityLifecycle (5.10).
    public abstract class SimEntity : MonoBehaviour
    {
        // Serialized so pool clones (Instantiate) inherit the category the catalog set on the template.
        [SerializeField, HideInInspector]
        private EntityCategory _category;

        private Vector2 _position;

        /// Entity id; EntityHandle.None while pooled.
        public EntityHandle Id { get; private set; }

        /// Template category, fixed by PrefabCatalog.
        public EntityCategory Category => _category;

        /// Collision circle of this entity (created on the first Bind and reused across pool cycles).
        public CollisionBody Body { get; private set; }

        /// Authoritative position; every write is mirrored into Body.Position. transform follows in PresentationSync.
        public Vector2 Position
        {
            get => _position;
            protected set
            {
                _position = value;
                if (Body != null)
                {
                    Body.Position = value;
                }
            }
        }

        /// Step in whose Lifecycle the entity was spawned.
        public long SpawnStep { get; private set; }

        /// long.MaxValue when no lifetime; LifetimeSystem despawns when Step >= ExpireStep.
        public long ExpireStep { get; private set; }

        /// Set in Resolve; later events for this entity are ignored.
        public bool IsConsumed { get; private set; }

        /// Lifecycle-assigned slot in the active list (-1 while pooled).
        internal int ActiveSlot { get; set; } = -1;

        internal void SetCategory(EntityCategory category)
        {
            _category = category;
        }

        // Spawn step 3 (after pool.Acquire and ids.Allocate): identity, lifetime and a fresh body bound to this entity.
        internal void Bind(EntityHandle id, long spawnStep, long expireStep)
        {
            Id = id;
            SpawnStep = spawnStep;
            ExpireStep = expireStep;
            IsConsumed = false;
            if (Body == null)
            {
                Body = new CollisionBody();
            }

            Body.Id = id;
            Body.Host = gameObject;
            Body.Layer = LayerOf(_category);
            Body.Position = _position;
        }

        internal void PlaceAt(Vector2 position)
        {
            Position = position;
        }

        internal void MarkConsumed()
        {
            IsConsumed = true;
        }

        // Despawn: OnReleased runs after the Despawned event and before the id is freed; the id is cleared afterwards.
        internal void Release()
        {
            OnReleased();
            Id = EntityHandle.None;
            if (Body != null)
            {
                Body.Id = EntityHandle.None;
            }
        }

        /// Clears references held by the entity; called before the instance goes back to the pool.
        protected abstract void OnReleased();

        // EntityCategory and CollisionLayer share names for every spawnable category (R30).
        private static CollisionLayer LayerOf(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.Monster: return CollisionLayer.Monster;
                case EntityCategory.PlayerProjectile: return CollisionLayer.PlayerProjectile;
                case EntityCategory.EnemyProjectile: return CollisionLayer.EnemyProjectile;
                case EntityCategory.PlayerOrb: return CollisionLayer.PlayerOrb;
                case EntityCategory.PlayerZone: return CollisionLayer.PlayerZone;
                case EntityCategory.EnemyZone: return CollisionLayer.EnemyZone;
                case EntityCategory.Gem: return CollisionLayer.Gem;
                default: return CollisionLayer.Chest;
            }
        }
    }
}
