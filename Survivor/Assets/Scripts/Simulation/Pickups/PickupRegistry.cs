using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Counts live gems and chests through EntityLifecycle.Spawned / Despawning (statistics and measurement CSV, TAC-F4-8).
    public sealed class PickupRegistry
    {
        /// Subscribes to lifecycle; null throws ArgumentNullException.
        public PickupRegistry(EntityLifecycle lifecycle)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }

            lifecycle.Spawned += OnSpawned;
            lifecycle.Despawning += OnDespawning;
        }

        /// Live experience gems.
        public int ActiveGemCount { get; private set; }

        /// Live chests.
        public int ActiveChestCount { get; private set; }

        private void OnSpawned(SimEntity entity)
        {
            if (entity.Category == EntityCategory.Gem)
            {
                ActiveGemCount++;
            }
            else if (entity.Category == EntityCategory.Chest)
            {
                ActiveChestCount++;
            }
        }

        private void OnDespawning(SimEntity entity)
        {
            if (entity.Category == EntityCategory.Gem)
            {
                ActiveGemCount--;
            }
            else if (entity.Category == EntityCategory.Chest)
            {
                ActiveChestCount--;
            }
        }
    }
}
