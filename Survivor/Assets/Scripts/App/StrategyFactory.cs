using System;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Pool;
using Survivor.Strategies.Tick;
using UnityEngine;

namespace Survivor.App
{
    /// Builds the Strategies instances of a run from a StrategySelection (5c.1: only App references Strategies). Shares the
    /// app-lifetime spatial index, snapshot buffer and physics body synchronizer with every strategy that needs them.
    public sealed class StrategyFactory
    {
        private readonly IStepClockReader _clock;
        private readonly MonsterSpatialIndex _index;
        private readonly MonsterSnapshotBuffer _snapshot;
        private readonly PhysicsBodySynchronizer _bodies;

        /// Creates the factory over the shared objects; null arguments throw ArgumentNullException.
        public StrategyFactory(IStepClockReader clock, MonsterSpatialIndex index, MonsterSnapshotBuffer snapshot, PhysicsBodySynchronizer bodies)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
        }

        /// Unity layer mask of the monster collision layer (R30), used by the Physics2D target provider.
        public static int MonsterLayerMask => CollisionLayerMapping.MaskOf(CollisionLayer.Monster);

        /// New pool of kind; undefined kinds throw ArgumentOutOfRangeException.
        public IPoolStrategy CreatePool(PoolStrategyKind kind)
        {
            switch (kind)
            {
                case PoolStrategyKind.InstantiateDestroy:
                    return new InstantiatePoolStrategy();
                case PoolStrategyKind.CustomPool:
                    return new CustomPoolStrategy();
                case PoolStrategyKind.UnityObjectPool:
                    return new UnityObjectPoolStrategy();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown pool strategy.");
            }
        }

        /// New tick strategy of kind; undefined kinds throw ArgumentOutOfRangeException.
        public ITickStrategy CreateTick(TickStrategyKind kind)
        {
            switch (kind)
            {
                case TickStrategyKind.CentralTick:
                    return new CentralTickStrategy();
                case TickStrategyKind.PerObjectFixedUpdate:
                    return new SelfTickStrategy(_clock);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tick strategy.");
            }
        }

        /// New collision strategy of kind (engine: with engineMode, which the constructor writes to the synchronizer);
        /// undefined kinds throw ArgumentOutOfRangeException.
        public ICollisionStrategy CreateCollision(CollisionStrategyKind kind, CollisionDetectionMode2D engineMode)
        {
            switch (kind)
            {
                case CollisionStrategyKind.SelfSquaredDistance:
                    return new SelfCollisionStrategy(_index, _snapshot);
                case CollisionStrategyKind.EngineCollider2D:
                    return new EngineCollisionStrategy(_bodies, engineMode);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown collision strategy.");
            }
        }

        /// New target provider of kind; undefined kinds throw ArgumentOutOfRangeException.
        public ITargetProvider CreateTarget(TargetProviderKind kind)
        {
            switch (kind)
            {
                case TargetProviderKind.BruteForce:
                    return new BruteForceTargetProvider();
                case TargetProviderKind.SpatialHash:
                    return new SpatialHashTargetProvider(_index);
                case TargetProviderKind.Physics2DQuery:
                    return new Physics2DTargetProvider(_bodies, MonsterLayerMask);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown target provider.");
            }
        }

        /// The four strategies of the next run. currentPool is kept when its kind equals selection.Pool (no Clear, no prewarm,
        /// 5.10); otherwise a new pool is made. collisionDecorator (null = none) wraps the collision strategy (dev logging).
        public RunStrategies Create(in StrategySelection selection, IPoolStrategy currentPool,
                                    Func<ICollisionStrategy, ICollisionStrategy> collisionDecorator)
        {
            IPoolStrategy pool = currentPool != null && currentPool.Kind == selection.Pool ? currentPool : CreatePool(selection.Pool);
            ITickStrategy tick = CreateTick(selection.Tick);
            ICollisionStrategy collision = CreateCollision(selection.Collision, selection.EngineDetectionMode);
            if (collisionDecorator != null)
            {
                collision = collisionDecorator(collision)
                    ?? throw new InvalidOperationException("StrategyFactory.Create: the collision decorator returned null.");
            }

            return new RunStrategies(pool, tick, collision, CreateTarget(selection.Target));
        }
    }
}
