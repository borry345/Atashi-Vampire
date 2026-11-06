using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.EditMode.Spatial
{
    public sealed class SpatialHashTests
    {
        [Test]
        public void KeyOf_PacksCxHighAndCyLowBits()
        {
            Assert.AreEqual(0L, SpatialHash.KeyOf(0, 0));
            Assert.AreEqual(1L << 32, SpatialHash.KeyOf(1, 0));
            Assert.AreEqual(1L, SpatialHash.KeyOf(0, 1));
            Assert.AreEqual(0x00000003_00000007L, SpatialHash.KeyOf(3, 7));
        }

        [Test]
        public void KeyOf_NegativeCy_KeepsTwosComplementInLowBits()
        {
            Assert.AreEqual(0x00000000_FFFFFFFFL, SpatialHash.KeyOf(0, -1));
            Assert.AreEqual(0x00000001_FFFFFFFFL, SpatialHash.KeyOf(1, -1));
            Assert.AreEqual(-1L, SpatialHash.KeyOf(-1, -1));
            Assert.AreEqual(unchecked((long)0xFFFFFFFF_00000000UL), SpatialHash.KeyOf(-1, 0));
            Assert.AreEqual(unchecked((long)0xFFFFFFFF_00000001UL), SpatialHash.KeyOf(-1, 1));
            Assert.AreEqual(unchecked((long)0x80000000_7FFFFFFFUL), SpatialHash.KeyOf(int.MinValue, int.MaxValue));
            Assert.AreEqual(0x7FFFFFFF_80000000L, SpatialHash.KeyOf(int.MaxValue, int.MinValue));
        }

        [Test]
        public void KeyOf_DistinctCellsNeverCollide()
        {
            var seen = new HashSet<long>();
            for (int cx = -3; cx <= 3; cx++)
            {
                for (int cy = -3; cy <= 3; cy++)
                {
                    Assert.IsTrue(seen.Add(SpatialHash.KeyOf(cx, cy)), $"collision at ({cx}, {cy})");
                }
            }
        }

        [Test]
        public void CellOf_PositiveCoordinates()
        {
            Assert.AreEqual(SpatialHash.KeyOf(0, 0), SpatialHash.CellOf(new Vector2(0.5f, 1.99f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(1, 2), SpatialHash.CellOf(new Vector2(3.99f, 5f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(10, 0), SpatialHash.CellOf(new Vector2(5f, 0.1f), 0.5f));
        }

        [Test]
        public void CellOf_NegativeCoordinates_FloorsInsteadOfTruncating()
        {
            Assert.AreEqual(SpatialHash.KeyOf(-1, -1), SpatialHash.CellOf(new Vector2(-0.5f, -0.5f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(-1, 0), SpatialHash.CellOf(new Vector2(-1.99f, 0f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(-2, -3), SpatialHash.CellOf(new Vector2(-2.01f, -4.5f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(-1, 0), SpatialHash.CellOf(new Vector2(-0.0001f, 0.0001f), 2f));
        }

        [Test]
        public void CellOf_ExactBoundary_BelongsToUpperCell()
        {
            Assert.AreEqual(SpatialHash.KeyOf(1, 1), SpatialHash.CellOf(new Vector2(2f, 2f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(-1, -1), SpatialHash.CellOf(new Vector2(-2f, -2f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(-2, 0), SpatialHash.CellOf(new Vector2(-4f, 0f), 2f));
            Assert.AreEqual(SpatialHash.KeyOf(0, 0), SpatialHash.CellOf(new Vector2(-0f, -0f), 2f));
        }

        [Test]
        public void CellOf_UsesDivisionThenFloor_LikeR14()
        {
            float cellSize = 0.3f;
            var p = new Vector2(-7.77f, 12.34f);
            long expected = SpatialHash.KeyOf(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
            Assert.AreEqual(expected, SpatialHash.CellOf(p, cellSize));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_InvalidCellSize_Throws(float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialHash(cellSize));
        }

        [Test]
        public void QueryCells_ReturnsPayloadsOfOverlappedCellsOnly()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(1, new Vector2(0.5f, 0.5f));
            hash.Insert(2, new Vector2(2.5f, 0.5f));
            hash.Insert(3, new Vector2(-1.5f, -1.5f));
            hash.Insert(4, new Vector2(9f, 9f));
            var result = new List<int> { 99 };

            hash.QueryCells(new Vector2(1f, 1f), 1.2f, result);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, result);
        }

        [Test]
        public void QueryCells_OrderIsRowsThenColumnsThenInsertion()
        {
            var hash = new SpatialHash(1f);
            hash.Insert(5, new Vector2(1.5f, 1.5f));
            hash.Insert(4, new Vector2(0.5f, 1.5f));
            hash.Insert(3, new Vector2(1.5f, 0.5f));
            hash.Insert(2, new Vector2(0.5f, 0.5f));
            hash.Insert(1, new Vector2(0.6f, 0.6f));
            var result = new List<int>();

            hash.QueryCells(new Vector2(1f, 1f), 0.9f, result);

            CollectionAssert.AreEqual(new[] { 2, 1, 3, 4, 5 }, result);
        }

        [Test]
        public void QueryCells_HugeRadius_ReturnsEverything()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(1, new Vector2(-1000f, 5f));
            hash.Insert(2, new Vector2(1000f, -5f));
            var result = new List<int>();

            hash.QueryCells(Vector2.zero, 1e7f, result);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result);
        }

        [Test]
        public void QueryCells_NegativeRadius_ReturnsNothing()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(1, Vector2.zero);
            var result = new List<int> { 7 };

            hash.QueryCells(Vector2.zero, -1f, result);

            Assert.IsEmpty(result);
        }

        [Test]
        public void QueryCells_NullOutput_Throws()
        {
            var hash = new SpatialHash(2f);
            Assert.Throws<ArgumentNullException>(() => hash.QueryCells(Vector2.zero, 1f, null));
        }

        [Test]
        public void Clear_RemovesEverything_AndHashIsReusable()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(1, new Vector2(0.5f, 0.5f));
            hash.Insert(2, new Vector2(50f, 50f));
            hash.Clear();
            var result = new List<int>();

            hash.QueryCells(Vector2.zero, 1e6f, result);
            Assert.IsEmpty(result);
            Assert.AreEqual(0, hash.Count);

            hash.Insert(3, new Vector2(-50f, -50f));
            hash.QueryCells(new Vector2(-50f, -50f), 0.1f, result);
            CollectionAssert.AreEqual(new[] { 3 }, result);
            Assert.AreEqual(1, hash.Count);
        }

        [Test]
        public void CellSize_IsConstructorValue()
        {
            Assert.AreEqual(3.5f, new SpatialHash(3.5f).CellSize);
        }
    }
}
