using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// M4 hazard zone: a static circle that damages the player while overlapping; LifetimeSystem removes it after DurationSteps.
    /// Hit interval and invulnerability are the player's rules.
    public sealed class EnemyHazardZone : SimEntity, IContactDamageSource
    {
        /// Boss pattern captured at spawn.
        public BossPatternDefinition Pattern { get; private set; }

        /// Damage dealt to the player by one hit.
        public float ContactDamage => Pattern.Damage;

        // Spawn initialize callback.
        internal void Initialize(BossPatternDefinition pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Body.Radius = pattern.ZoneRadius;
            Body.IsLarge = false;
        }

        /// Clears the pattern before the instance returns to the pool.
        protected override void OnReleased()
        {
            Pattern = null;
        }
    }
}
