using System;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class MonsterSnapshotBufferTests
    {
        private static MonsterSnapshot Snap(int index, int generation, float x, float y, float radius, bool isLarge = false)
        {
            return new MonsterSnapshot
            {
                Id = new EntityHandle(index, generation), Position = new Vector2(x, y), Radius = radius, IsLarge = isLarge,
            };
        }

        [Test]
        public void Empty_HasZeroCountRadiusAndBounds()
        {
            var buffer = new MonsterSnapshotBuffer();
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0f, buffer.MaxHashedRadius);
            Assert.AreEqual(default(Rect), buffer.Bounds);
            Assert.IsFalse(buffer.TryGetIndex(new EntityHandle(1, 1), out int index));
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void Add_TracksCountBoundsAndMaxHashedRadius()
        {
            var buffer = new MonsterSnapshotBuffer();
            buffer.Add(Snap(3, 1, -5f, 2f, 0.4f));
            buffer.Add(Snap(4, 2, 10f, -8f, 0.9f));
            buffer.Add(Snap(9, 1, 1f, 20f, 2.5f, isLarge: true));

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(0.9f, buffer.MaxHashedRadius);
            Assert.AreEqual(Rect.MinMaxRect(-5f, -8f, 10f, 20f), buffer.Bounds);
            Assert.AreEqual(new EntityHandle(4, 2), buffer[1].Id);
            Assert.AreEqual(new Vector2(1f, 20f), buffer[2].Position);
        }

        [Test]
        public void Add_RejectsNonAscendingIds()
        {
            var buffer = new MonsterSnapshotBuffer();
            buffer.Add(Snap(5, 2, 0f, 0f, 0.5f));
            Assert.Throws<InvalidOperationException>(() => buffer.Add(Snap(5, 2, 0f, 0f, 0.5f)));
            Assert.Throws<InvalidOperationException>(() => buffer.Add(Snap(5, 1, 0f, 0f, 0.5f)));
            Assert.Throws<InvalidOperationException>(() => buffer.Add(Snap(4, 9, 0f, 0f, 0.5f)));
            buffer.Add(Snap(5, 3, 0f, 0f, 0.5f));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void TryGetIndex_FindsByIdIncludingGeneration()
        {
            var buffer = new MonsterSnapshotBuffer();
            for (int i = 1; i <= 1000; i++)
            {
                buffer.Add(Snap(i * 2, 1, i, -i, 0.5f));
            }

            Assert.AreEqual(1000, buffer.Count);
            Assert.IsTrue(buffer.TryGetIndex(new EntityHandle(2, 1), out int first));
            Assert.AreEqual(0, first);
            Assert.IsTrue(buffer.TryGetIndex(new EntityHandle(2000, 1), out int last));
            Assert.AreEqual(999, last);
            Assert.IsTrue(buffer.TryGetIndex(new EntityHandle(778, 1), out int mid));
            Assert.AreEqual(388, mid);
            Assert.IsFalse(buffer.TryGetIndex(new EntityHandle(777, 1), out _));
            Assert.IsFalse(buffer.TryGetIndex(new EntityHandle(778, 2), out _));
        }

        [Test]
        public void Indexer_OutOfRangeThrows()
        {
            var buffer = new MonsterSnapshotBuffer();
            buffer.Add(Snap(1, 1, 0f, 0f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = buffer[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = buffer[-1]; });
        }

        [Test]
        public void Clear_EmptiesAndAllowsLowerIdsAgain()
        {
            var buffer = new MonsterSnapshotBuffer();
            buffer.Add(Snap(7, 1, 3f, 3f, 0.8f));
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0f, buffer.MaxHashedRadius);
            Assert.AreEqual(default(Rect), buffer.Bounds);
            buffer.Add(Snap(1, 1, 0f, 0f, 0.2f));
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(0.2f, buffer.MaxHashedRadius);
        }
    }
}
