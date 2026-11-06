using UnityEngine;

namespace Survivor.Core
{
    /// The four strategy choices of a run plus engine CCD mode and spatial hash cell size.
    public readonly struct StrategySelection
    {
        // Mirrors the documented SpatialHashSettings.CellSize default (2.0, TAC-F16-4); Core cannot reference Strategies.
        private const float DefaultSpatialHashCellSize = 2f;

        /// Pool strategy (F9).
        public readonly PoolStrategyKind Pool;

        /// Target provider (F16).
        public readonly TargetProviderKind Target;

        /// Tick strategy (F17).
        public readonly TickStrategyKind Tick;

        /// Collision strategy (F18).
        public readonly CollisionStrategyKind Collision;

        /// Engine collision detection mode; Discrete by default.
        public readonly CollisionDetectionMode2D EngineDetectionMode;

        /// Spatial hash cell size, recorded as cellSize in measurements.
        public readonly float SpatialHashCellSize;

        /// Creates a selection.
        public StrategySelection(
            PoolStrategyKind pool,
            TargetProviderKind target,
            TickStrategyKind tick,
            CollisionStrategyKind collision,
            CollisionDetectionMode2D engineDetectionMode = CollisionDetectionMode2D.Discrete,
            float spatialHashCellSize = DefaultSpatialHashCellSize)
        {
            Pool = pool;
            Target = target;
            Tick = tick;
            Collision = collision;
            EngineDetectionMode = engineDetectionMode;
            SpatialHashCellSize = spatialHashCellSize;
        }

        /// CustomPool, SpatialHash, CentralTick, SelfSquaredDistance.
        public static StrategySelection ShippingDefault => new StrategySelection(
            PoolStrategyKind.CustomPool, TargetProviderKind.SpatialHash,
            TickStrategyKind.CentralTick, CollisionStrategyKind.SelfSquaredDistance);

        /// InstantiateDestroy, BruteForce, PerObjectFixedUpdate, EngineCollider2D.
        public static StrategySelection Baseline => new StrategySelection(
            PoolStrategyKind.InstantiateDestroy, TargetProviderKind.BruteForce,
            TickStrategyKind.PerObjectFixedUpdate, CollisionStrategyKind.EngineCollider2D);
    }
}
