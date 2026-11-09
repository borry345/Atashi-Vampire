using System;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Pool;
using Survivor.Strategies.Tick;
using UnityEngine;

namespace Survivor.Tests.EditMode.App
{
    /// StrategyFactory builds the concrete Strategies type of every kind (5c.1: only App references Strategies) over the shared
    /// clock / spatial index / snapshot / synchronizer, keeps the current pool when the kind does not change (5.10) and applies
    /// the collision decorator.
    public sealed class StrategyFactoryTests
    {
        private SimulationClock _clock;
        private MonsterSnapshotBuffer _snapshot;
        private MonsterSpatialIndex _index;
        private PhysicsBodySynchronizer _bodies;
        private StrategyFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _clock = new SimulationClock();
            _snapshot = new MonsterSnapshotBuffer();
            _index = new MonsterSpatialIndex(StrategySelection.DefaultSpatialHashCellSize, _snapshot);
            _bodies = new PhysicsBodySynchronizer(ColliderActivationPolicy.Resolve(StrategySelection.ShippingDefault));
            _factory = new StrategyFactory(_clock, _index, _snapshot, _bodies);
        }

        [TestCase(PoolStrategyKind.InstantiateDestroy, typeof(InstantiatePoolStrategy))]
        [TestCase(PoolStrategyKind.CustomPool, typeof(CustomPoolStrategy))]
        [TestCase(PoolStrategyKind.UnityObjectPool, typeof(UnityObjectPoolStrategy))]
        public void CreatePool_BuildsTheTypeOfEachKind(PoolStrategyKind kind, Type expected)
        {
            IPoolStrategy pool = _factory.CreatePool(kind);
            Assert.IsInstanceOf(expected, pool);
            Assert.AreEqual(kind, pool.Kind);
        }

        [TestCase(TickStrategyKind.CentralTick, typeof(CentralTickStrategy))]
        [TestCase(TickStrategyKind.PerObjectFixedUpdate, typeof(SelfTickStrategy))]
        public void CreateTick_BuildsTheTypeOfEachKind(TickStrategyKind kind, Type expected)
        {
            ITickStrategy tick = _factory.CreateTick(kind);
            Assert.IsInstanceOf(expected, tick);
            Assert.AreEqual(kind, tick.Kind);
        }

        [TestCase(CollisionStrategyKind.SelfSquaredDistance, typeof(SelfCollisionStrategy))]
        [TestCase(CollisionStrategyKind.EngineCollider2D, typeof(EngineCollisionStrategy))]
        public void CreateCollision_BuildsTheTypeOfEachKind(CollisionStrategyKind kind, Type expected)
        {
            ICollisionStrategy collision = _factory.CreateCollision(kind, CollisionDetectionMode2D.Discrete);
            Assert.IsInstanceOf(expected, collision);
            Assert.AreEqual(kind, collision.Kind);
        }

        [TestCase(TargetProviderKind.BruteForce, typeof(BruteForceTargetProvider))]
        [TestCase(TargetProviderKind.SpatialHash, typeof(SpatialHashTargetProvider))]
        [TestCase(TargetProviderKind.Physics2DQuery, typeof(Physics2DTargetProvider))]
        public void CreateTarget_BuildsTheTypeOfEachKind(TargetProviderKind kind, Type expected)
        {
            ITargetProvider target = _factory.CreateTarget(kind);
            Assert.IsInstanceOf(expected, target);
            Assert.AreEqual(kind, target.Kind);
        }

        [Test]
        public void CreateCollision_EngineWritesItsDetectionModeToTheSharedSynchronizer()
        {
            var engine = (EngineCollisionStrategy)_factory.CreateCollision(CollisionStrategyKind.EngineCollider2D, CollisionDetectionMode2D.Continuous);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, engine.DetectionMode);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, _bodies.DetectionMode);
        }

        [Test]
        public void MonsterLayerMask_IsTheMonsterCollisionLayerBit()
        {
            Assert.AreEqual(CollisionLayerMapping.MaskOf(CollisionLayer.Monster), StrategyFactory.MonsterLayerMask);
            Assert.AreNotEqual(0, StrategyFactory.MonsterLayerMask);
        }

        [Test]
        public void UndefinedKinds_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreatePool((PoolStrategyKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateTick((TickStrategyKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateCollision((CollisionStrategyKind)99, CollisionDetectionMode2D.Discrete));
            Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateTarget((TargetProviderKind)99));
        }

        [Test]
        public void Constructor_NullArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new StrategyFactory(null, _index, _snapshot, _bodies));
            Assert.Throws<ArgumentNullException>(() => new StrategyFactory(_clock, null, _snapshot, _bodies));
            Assert.Throws<ArgumentNullException>(() => new StrategyFactory(_clock, _index, null, _bodies));
            Assert.Throws<ArgumentNullException>(() => new StrategyFactory(_clock, _index, _snapshot, null));
        }

        [Test]
        public void Create_BuildsTheFourKindsOfTheSelection()
        {
            RunStrategies run = _factory.Create(StrategySelection.Baseline, null, null);
            Assert.IsInstanceOf<InstantiatePoolStrategy>(run.Pool);
            Assert.IsInstanceOf<BruteForceTargetProvider>(run.Target);
            Assert.IsInstanceOf<SelfTickStrategy>(run.Tick);
            Assert.IsInstanceOf<EngineCollisionStrategy>(run.Collision);

            run = _factory.Create(StrategySelection.ShippingDefault, null, null);
            Assert.IsInstanceOf<CustomPoolStrategy>(run.Pool);
            Assert.IsInstanceOf<SpatialHashTargetProvider>(run.Target);
            Assert.IsInstanceOf<CentralTickStrategy>(run.Tick);
            Assert.IsInstanceOf<SelfCollisionStrategy>(run.Collision);
        }

        [Test]
        public void Create_KeepsTheCurrentPoolOfTheSameKind_AndReplacesAnotherKind()
        {
            IPoolStrategy current = _factory.CreatePool(PoolStrategyKind.CustomPool);
            Assert.AreSame(current, _factory.Create(StrategySelection.ShippingDefault, current, null).Pool, "same kind: no Clear, no prewarm");
            IPoolStrategy next = _factory.Create(StrategySelection.Baseline, current, null).Pool;
            Assert.AreNotSame(current, next);
            Assert.AreEqual(PoolStrategyKind.InstantiateDestroy, next.Kind);
        }

        [Test]
        public void Create_NewTickCollisionAndTargetEveryRun()
        {
            RunStrategies a = _factory.Create(StrategySelection.ShippingDefault, null, null);
            RunStrategies b = _factory.Create(StrategySelection.ShippingDefault, a.Pool, null);
            Assert.AreNotSame(a.Tick, b.Tick);
            Assert.AreNotSame(a.Collision, b.Collision);
            Assert.AreNotSame(a.Target, b.Target);
        }

        [Test]
        public void Create_WrapsTheCollisionStrategyWithTheDecorator()
        {
            ICollisionStrategy seen = null;
            var wrapper = new SelfCollisionStrategy(_index, _snapshot);
            RunStrategies run = _factory.Create(StrategySelection.ShippingDefault, null, inner =>
            {
                seen = inner;
                return wrapper;
            });
            Assert.IsInstanceOf<SelfCollisionStrategy>(seen);
            Assert.AreNotSame(wrapper, seen);
            Assert.AreSame(wrapper, run.Collision);
            Assert.Throws<InvalidOperationException>(() => _factory.Create(StrategySelection.ShippingDefault, null, inner => null));
        }
    }
}
