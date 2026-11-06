using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.EditMode.Spatial
{
    public sealed class MonsterSpatialIndexTests
    {
        private MonsterSnapshotBuffer _snapshot;
        private MonsterSpatialIndex _index;
        private List<int> _result;

        [SetUp]
        public void SetUp()
        {
            _snapshot = new MonsterSnapshotBuffer();
            _index = new MonsterSpatialIndex(2f, _snapshot);
            _result = new List<int>();
        }

        [Test]
        public void Constructor_ValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new MonsterSpatialIndex(2f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterSpatialIndex(0f, _snapshot));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterSpatialIndex(float.NaN, _snapshot));
            Assert.AreEqual(2f, _index.CellSize);
            Assert.IsFalse(_index.Required);
        }

        [Test]
        public void BuildIndex_NotRequired_DoesNotRebuild()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);
            _index.Required = false;

            _index.OnPhase(SimulationPhase.BuildIndex, default);

            Assert.AreEqual(0, _index.CellCounts.Count);
            _index.QueryCircle(new Vector2(0.5f, 0.5f), 10f, _result);
            Assert.IsEmpty(_result);
        }

        [Test]
        public void OtherPhases_DoNothingEvenWhenRequired()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);
            _index.Required = true;

            foreach (SimulationPhase phase in Enum.GetValues(typeof(SimulationPhase)))
            {
                if (phase != SimulationPhase.BuildIndex)
                {
                    _index.OnPhase(phase, default);
                }
            }

            Assert.AreEqual(0, _index.CellCounts.Count);
            _index.OnPhase(SimulationPhase.BuildIndex, default);
            Assert.AreEqual(1, _index.CellCounts.Count);
        }

        [Test]
        public void Rebuild_CountsHashedMonstersPerCell_ExcludingLarge()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);
            SpatialTestUtil.Add(_snapshot, 2, 1.5f, 1.5f);
            SpatialTestUtil.Add(_snapshot, 3, -0.5f, 0.5f);
            SpatialTestUtil.Add(_snapshot, 4, 0.7f, 0.7f, 3f, true);

            SpatialTestUtil.Rebuild(_index);

            IReadOnlyDictionary<long, int> counts = _index.CellCounts;
            Assert.AreEqual(2, counts.Count);
            Assert.AreEqual(2, counts[SpatialHash.KeyOf(0, 0)]);
            Assert.AreEqual(1, counts[SpatialHash.KeyOf(-1, 0)]);
        }

        [Test]
        public void Rebuild_ReflectsCurrentSnapshot_RemovedMonstersDisappear()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);
            SpatialTestUtil.Add(_snapshot, 2, 10.5f, 10.5f);
            SpatialTestUtil.Rebuild(_index);

            _snapshot.Clear();
            SpatialTestUtil.Add(_snapshot, 2, 11.5f, 10.5f);
            SpatialTestUtil.Rebuild(_index);

            _index.QueryCircle(new Vector2(0.5f, 0.5f), 1f, _result);
            Assert.IsEmpty(_result);
            _index.QueryCircle(new Vector2(11.5f, 10.5f), 0.1f, _result);
            CollectionAssert.AreEqual(new[] { 0 }, _result);
            Assert.AreEqual(1, _index.CellCounts.Count);
            Assert.AreEqual(1, _index.CellCounts[SpatialHash.CellOf(new Vector2(11.5f, 10.5f), 2f)]);
        }

        [Test]
        public void QueryCircle_AlwaysIncludesLargeMonsters_AndIsAscending()
        {
            SpatialTestUtil.Add(_snapshot, 1, 500f, 500f, 4f, true);
            SpatialTestUtil.Add(_snapshot, 2, 0.5f, 0.5f);
            SpatialTestUtil.Add(_snapshot, 3, 100f, 100f);
            SpatialTestUtil.Add(_snapshot, 4, -900f, 3f, 2f, true);
            SpatialTestUtil.Add(_snapshot, 5, 1.0f, 0.2f);
            SpatialTestUtil.Rebuild(_index);

            _index.QueryCircle(new Vector2(0.5f, 0.5f), 0.5f, _result);

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, _result);
        }

        [Test]
        public void QueryCircle_WidensByMaxHashedRadius()
        {
            var index = new MonsterSpatialIndex(1f, _snapshot);
            SpatialTestUtil.Add(_snapshot, 1, 1.4f, 0.5f, 0.9f);
            SpatialTestUtil.Rebuild(index);

            // Query square [0.4, 0.6] alone covers only cell 0; the monster in cell 1 overlaps (0.9 <= 0.1 + 0.9).
            index.QueryCircle(new Vector2(0.5f, 0.5f), 0.1f, _result);

            CollectionAssert.AreEqual(new[] { 0 }, _result);
        }

        [Test]
        public void QueryCircle_NullOutput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _index.QueryCircle(Vector2.zero, 1f, null));
        }

        [Test]
        public void ForEachRing_ReturnsCellsAtExactChebyshevDistance()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);    // cell (0, 0)
            SpatialTestUtil.Add(_snapshot, 2, 2.5f, -1.5f);   // cell (1, -1)
            SpatialTestUtil.Add(_snapshot, 3, -1.5f, 2.5f);   // cell (-1, 1)
            SpatialTestUtil.Add(_snapshot, 4, 4.5f, 0.5f);    // cell (2, 0)
            SpatialTestUtil.Add(_snapshot, 5, -3.5f, -3.5f);  // cell (-2, -2)
            SpatialTestUtil.Add(_snapshot, 6, 0.5f, 6.5f);    // cell (0, 3)
            SpatialTestUtil.Add(_snapshot, 7, 0.1f, 0.1f, 5f, true);
            SpatialTestUtil.Rebuild(_index);
            var point = new Vector2(1f, 1f);

            _index.ForEachRing(point, 0, _result);
            CollectionAssert.AreEquivalent(new[] { 0 }, _result);
            _index.ForEachRing(point, 1, _result);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, _result);
            _index.ForEachRing(point, 2, _result);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, _result);
            _index.ForEachRing(point, 3, _result);
            CollectionAssert.AreEquivalent(new[] { 5 }, _result);
            _index.ForEachRing(point, 4, _result);
            Assert.IsEmpty(_result);
        }

        [Test]
        public void ForEachRing_NegativeCoordinates()
        {
            SpatialTestUtil.Add(_snapshot, 1, -100.5f, -100.5f);  // cell (-51, -51)
            SpatialTestUtil.Add(_snapshot, 2, -98.5f, -100.5f);   // cell (-50, -51)
            SpatialTestUtil.Rebuild(_index);

            _index.ForEachRing(new Vector2(-100.1f, -100.9f), 0, _result);
            CollectionAssert.AreEqual(new[] { 0 }, _result);
            _index.ForEachRing(new Vector2(-100.1f, -100.9f), 1, _result);
            CollectionAssert.AreEqual(new[] { 1 }, _result);
        }

        [Test]
        public void ForEachRing_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _index.ForEachRing(Vector2.zero, -1, _result));
            Assert.Throws<ArgumentNullException>(() => _index.ForEachRing(Vector2.zero, 0, null));
        }

        [Test]
        public void Clear_EmptiesHashAndLargeList()
        {
            SpatialTestUtil.Add(_snapshot, 1, 0.5f, 0.5f);
            SpatialTestUtil.Add(_snapshot, 2, 9f, 9f, 3f, true);
            SpatialTestUtil.Rebuild(_index);
            Assert.AreEqual(1, _index.CellCounts.Count);

            _index.Clear();

            Assert.AreEqual(0, _index.CellCounts.Count);
            _index.QueryCircle(Vector2.zero, 1e6f, _result);
            Assert.IsEmpty(_result);
            _index.ForEachRing(new Vector2(0.5f, 0.5f), 0, _result);
            Assert.IsEmpty(_result);
            Assert.IsTrue(_index.Required, "Clear does not change Required");
        }
    }
}
