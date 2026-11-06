using System;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    /// M3 projectile: flies straight at SpeedPerStep until LifetimeSystem expires it. Consumption on hitting the player is the
    /// player's damage receiver's job (it marks and despawns the projectile); this class only moves.
    public sealed class EnemyProjectile : SimEntity, IUpdatable, IContactDamageSource
    {
        /// Definition captured at spawn.
        public EnemyProjectileDefinition Definition { get; private set; }

        /// Unit flight direction fixed at spawn.
        public Vector2 Direction { get; private set; }

        /// Tick group of every projectile.
        public TickGroup Group => TickGroup.Projectiles;

        /// Damage dealt to the player on contact.
        public float ContactDamage => Definition.Damage;

        // Spawn initialize callback.
        internal void Initialize(EnemyProjectileDefinition definition, Vector2 direction)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Direction = direction;
            Body.Radius = definition.Radius;
            Body.IsLarge = false;
        }

        /// Advances Position by Direction * SpeedPerStep.
        public void Tick(in StepContext context)
        {
            Position += Direction * Definition.SpeedPerStep;
        }

        /// Clears the definition before the instance returns to the pool.
        protected override void OnReleased()
        {
            Definition = null;
            Direction = Vector2.zero;
        }
    }
}
