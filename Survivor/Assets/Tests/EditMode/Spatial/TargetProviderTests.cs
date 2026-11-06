using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.EditMode.Spatial
{
    [TestFixture(TargetProviderKind.BruteForce)]
    [TestFixture(TargetProviderKind.SpatialHash)]
    public sealed class TargetProviderTests
    {
        private readonly TargetProviderKind _kind;
        private MonsterSnapshotBuffer _snapshot;
        private MonsterSpatialIndex _index;
        private ITargetProvider _provider;

        public TargetProviderTests(TargetProviderKind kind)
        {
            _kind = kind;
        }

        [SetUp]
        public void SetUp()
        {
            _snapshot = new MonsterSnapshotBuffer();
            _index = new MonsterSpatialIndex(StrategySelection.DefaultSpatialHashCellSize, _snapshot) { Required = true };
            _provider = _kind == TargetProviderKind.BruteForce
                ? new BruteForceTargetProvider()
                : (ITargetProvider)new SpatialHashTargetProvider(_index);
        }

        private void Build()
        {
            _index.OnPhase(SimulationPhase.BuildIndex, default);
            _provider.OnSnapshotBuilt(_snapshot);
        }

        [Test]
        public void Kind_MatchesImplementation()
        {
            Assert.AreEqual(_kind, _provider.Kind);
        }

        [Test]
        public void BeforeSnapshot_ReturnsFalse()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0f, 0f);
            _index.OnPhase(SimulationPhase.BuildIndex, default);

            Assert.IsFalse(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(EntityHandle.None, hit.Id);
        }

        [Test]
        public void NoMonsters_ReturnsFalseWithoutThrowing()
        {
            Build();

            Assert.IsFalse(_provider.TryFindNearest(new Vector2(3f, -4f), out TargetHit hit));
            Assert.AreEqual(EntityHandle.None, hit.Id);
        }

        [Test]
        public void ReturnsNearestByCenterDistance_WithSnapshotPositionAndSquaredDistance()
        {
            SpatialTestUtil.Add(_snapshot, 1, 3f, 0f, 0.2f);
            SpatialTestUtil.Add(_snapshot, 2, 0f, 2.5f, 0.9f);
            SpatialTestUtil.Add(_snapshot, 3, -5f, -5f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(new EntityHandle(2, 1), hit.Id);
            Assert.AreEqual(new Vector2(0f, 2.5f), hit.Position);
            Assert.AreEqual(6.25f, hit.DistanceSquared);
        }

        [Test]
        public void EqualDistance_SmallerIdWins()
        {
            SpatialTestUtil.Add(_snapshot, 4, 2f, 0f);
            SpatialTestUtil.Add(_snapshot, 7, -2f, 0f);
            SpatialTestUtil.Add(_snapshot, 9, 0f, 2f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(new EntityHandle(4, 1), hit.Id);
        }

        [Test]
        public void EqualDistanceInOppositeDistantCells_SmallerIdWins()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 6.5f);
            SpatialTestUtil.Add(_snapshot, 2, 0.5f, -5.5f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(new Vector2(0.5f, 0.5f), out TargetHit hit));
            Assert.AreEqual(new EntityHandle(1, 1), hit.Id);
            Assert.AreEqual(36f, hit.DistanceSquared);
        }

        [Test]
        public void SingleFarMonster_IsFound()
        {
            SpatialTestUtil.Add(_snapshot, 1, 12345.5f, -9876.25f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(new Vector2(-3f, 7f), out TargetHit hit));
            Assert.AreEqual(new EntityHandle(1, 1), hit.Id);
            Assert.AreEqual((new Vector2(12345.5f, -9876.25f) - new Vector2(-3f, 7f)).sqrMagnitude, hit.DistanceSquared);
        }

        [Test]
        public void FarQueryPoint_FindsNearestOfCluster()
        {
            SpatialTestUtil.Add(_snapshot, 1, 1f, 1f);
            SpatialTestUtil.Add(_snapshot, 2, 30f, -2f);
            SpatialTestUtil.Add(_snapshot, 3, -30f, 4f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(new Vector2(-100000f, 0f), out TargetHit hit));
            Assert.AreEqual(new EntityHandle(3, 1), hit.Id);
        }

        [Test]
        public void LargeMonster_NearestIsReturned()
        {
            SpatialTestUtil.Add(_snapshot, 1, 5f, 5f);
            SpatialTestUtil.Add(_snapshot, 2, 1f, 0f, 3f, true);
            SpatialTestUtil.Add(_snapshot, 3, -4f, 0f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(new EntityHandle(2, 1), hit.Id);
        }

        [Test]
        public void LargeMonster_TieWithHashedMonster_SmallerIdWins()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0f, -3f, 3f, true);
            SpatialTestUtil.Add(_snapshot, 2, 3f, 0f);
            SpatialTestUtil.Add(_snapshot, 3, 0f, 3f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(new EntityHandle(1, 1), hit.Id);
        }

        [Test]
        public void OnlyLargeMonsters_AreSearched()
        {
            SpatialTestUtil.Add(_snapshot, 1, 40f, 0f, 2f, true);
            SpatialTestUtil.Add(_snapshot, 2, -20f, 0f, 2f, true);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit hit));
            Assert.AreEqual(new EntityHandle(2, 1), hit.Id);
        }

        [Test]
        public void NewSnapshot_DeadMonstersAreNotReturned()
        {
            SpatialTestUtil.Add(_snapshot, 1, 1f, 0f);
            SpatialTestUtil.Add(_snapshot, 2, 9f, 0f);
            Build();
            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit first));
            Assert.AreEqual(new EntityHandle(1, 1), first.Id);

            _snapshot.Clear();
            SpatialTestUtil.Add(_snapshot, 2, 8f, 0f);
            Build();

            Assert.IsTrue(_provider.TryFindNearest(Vector2.zero, out TargetHit second));
            Assert.AreEqual(new EntityHandle(2, 1), second.Id);
            Assert.AreEqual(new Vector2(8f, 0f), second.Position);
        }

        [Test]
        public void NullSnapshot_ForgetsPreviousSnapshot()
        {
            SpatialTestUtil.Add(_snapshot, 1, 1f, 0f);
            Build();

            _provider.OnSnapshotBuilt(null);

            Assert.IsFalse(_provider.TryFindNearest(Vector2.zero, out _));
        }
    }
}
