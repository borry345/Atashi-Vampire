using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Per-category prewarm counts (Q13, adjusted after measurement) and the prewarm helper the composition root calls.
    public static class PoolPrewarmCounts
    {
        /// Monster instances.
        public const int Monster = 450;

        /// Player projectile instances.
        public const int PlayerProjectile = 300;

        /// Enemy projectile instances.
        public const int EnemyProjectile = 100;

        /// Player orb instances.
        public const int PlayerOrb = 16;

        /// Player zone instances.
        public const int PlayerZone = 16;

        /// Enemy zone instances.
        public const int EnemyZone = 8;

        /// Gem instances.
        public const int Gem = 600;

        /// Chest instances.
        public const int Chest = 16;

        /// Prewarm count of category.
        public static int CountOf(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.Monster: return Monster;
                case EntityCategory.PlayerProjectile: return PlayerProjectile;
                case EntityCategory.EnemyProjectile: return EnemyProjectile;
                case EntityCategory.PlayerOrb: return PlayerOrb;
                case EntityCategory.PlayerZone: return PlayerZone;
                case EntityCategory.EnemyZone: return EnemyZone;
                case EntityCategory.Gem: return Gem;
                case EntityCategory.Chest: return Chest;
                default: throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown entity category.");
            }
        }

        /// Prewarms pool with CountOf(category) instances of every catalog template, in EntityCategory order; categories without a
        /// template are skipped. Called by the composition root at boot and after a pool replacement. Null arguments throw ArgumentNullException.
        public static void PrewarmAll(IPoolStrategy pool, PrefabCatalog catalog)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                if (catalog.HasTemplate(category))
                {
                    pool.Prewarm(catalog.GetTemplate(category), CountOf(category));
                }
            }
        }
    }
}
