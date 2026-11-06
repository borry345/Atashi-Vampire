using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R19_ColliderActivationTests
    {
        private static PhysicsBodyRequirements Resolve(TargetProviderKind target, CollisionStrategyKind collision)
        {
            var selection = new StrategySelection(PoolStrategyKind.CustomPool, target, TickStrategyKind.CentralTick, collision);
            return ColliderActivationPolicy.Resolve(in selection);
        }

        [TestCase(TargetProviderKind.BruteForce)]
        [TestCase(TargetProviderKind.SpatialHash)]
        public void SelfCollision_NonPhysicsTarget_NeedsNothing(TargetProviderKind target)
        {
            PhysicsBodyRequirements r = Resolve(target, CollisionStrategyKind.SelfSquaredDistance);
            Assert.IsFalse(r.MonsterColliders);
            Assert.IsFalse(r.AllColliders);
            Assert.IsFalse(r.SyncMonstersAtBuildIndex);
            Assert.IsFalse(r.SimulateAtCollision);
            for (int layer = 0; layer <= (int)CollisionLayer.Chest; layer++)
            {
                Assert.IsFalse(r.CollidersEnabledFor((CollisionLayer)layer));
            }
        }

        [Test]
        public void SelfCollision_PhysicsTarget_NeedsMonsterCollidersAndSync()
        {
            PhysicsBodyRequirements r = Resolve(TargetProviderKind.Physics2DQuery, CollisionStrategyKind.SelfSquaredDistance);
            Assert.IsTrue(r.MonsterColliders);
            Assert.IsFalse(r.AllColliders);
            Assert.IsTrue(r.SyncMonstersAtBuildIndex);
            Assert.IsFalse(r.SimulateAtCollision);
            for (int layer = 0; layer <= (int)CollisionLayer.Chest; layer++)
            {
                Assert.AreEqual((CollisionLayer)layer == CollisionLayer.Monster, r.CollidersEnabledFor((CollisionLayer)layer));
            }
        }

        [TestCase(TargetProviderKind.BruteForce)]
        [TestCase(TargetProviderKind.SpatialHash)]
        public void EngineCollision_NonPhysicsTarget_NeedsAllCollidersAndSimulate(TargetProviderKind target)
        {
            PhysicsBodyRequirements r = Resolve(target, CollisionStrategyKind.EngineCollider2D);
            Assert.IsTrue(r.MonsterColliders);
            Assert.IsTrue(r.AllColliders);
            Assert.IsFalse(r.SyncMonstersAtBuildIndex);
            Assert.IsTrue(r.SimulateAtCollision);
            for (int layer = 0; layer <= (int)CollisionLayer.Chest; layer++)
            {
                Assert.IsTrue(r.CollidersEnabledFor((CollisionLayer)layer));
            }
        }

        [Test]
        public void EngineCollision_PhysicsTarget_NeedsEverything()
        {
            PhysicsBodyRequirements r = Resolve(TargetProviderKind.Physics2DQuery, CollisionStrategyKind.EngineCollider2D);
            Assert.IsTrue(r.MonsterColliders);
            Assert.IsTrue(r.AllColliders);
            Assert.IsTrue(r.SyncMonstersAtBuildIndex);
            Assert.IsTrue(r.SimulateAtCollision);
        }

        [Test]
        public void ShippingDefaultAndBaseline_ResolveAsDocumented()
        {
            StrategySelection shipping = StrategySelection.ShippingDefault;
            PhysicsBodyRequirements s = ColliderActivationPolicy.Resolve(in shipping);
            Assert.IsFalse(s.MonsterColliders || s.AllColliders || s.SyncMonstersAtBuildIndex || s.SimulateAtCollision);

            StrategySelection baseline = StrategySelection.Baseline;
            PhysicsBodyRequirements b = ColliderActivationPolicy.Resolve(in baseline);
            Assert.IsTrue(b.AllColliders && b.MonsterColliders && b.SimulateAtCollision);
            Assert.IsFalse(b.SyncMonstersAtBuildIndex);
        }
    }
}
