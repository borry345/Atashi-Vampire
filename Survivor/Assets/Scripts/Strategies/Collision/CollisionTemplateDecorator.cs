using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Attaches the engine collision components to every entity template regardless of the selected strategies (5.6, TAC-F9-7):
    /// a disabled trigger CircleCollider2D, a Kinematic Rigidbody2D with simulated = false and a CollisionBodyLink; layer = R30.
    public sealed class CollisionTemplateDecorator : IEntityTemplateDecorator
    {
        /// Adds each component once (idempotent) and sets the Unity layer of category. Null template throws ArgumentNullException.
        public void Decorate(GameObject template, EntityCategory category)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            EnsureComponents(template, LayerOf(category));
        }

        /// Collision layer with the same name as category (R30; the same mapping SimEntity.Bind uses).
        internal static CollisionLayer LayerOf(EntityCategory category)
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
                case EntityCategory.Chest: return CollisionLayer.Chest;
                default: throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown EntityCategory.");
            }
        }

        /// Adds the missing components in their decorated (inactive) state and sets the layer; existing components are kept as
        /// they are. Shared with PhysicsBodySynchronizer.Register for hosts that never went through a template (C1).
        internal static void EnsureComponents(GameObject host, CollisionLayer layer)
        {
            host.layer = CollisionLayerMapping.UnityLayerOf(layer);

            if (!host.TryGetComponent(out Rigidbody2D rb))
            {
                rb = host.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.useFullKinematicContacts = true;
                rb.simulated = false;
            }

            if (!host.TryGetComponent(out CircleCollider2D circle))
            {
                circle = host.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.enabled = false;
            }

            if (!host.TryGetComponent(out CollisionBodyLink _))
            {
                host.AddComponent<CollisionBodyLink>();
            }
        }
    }
}
