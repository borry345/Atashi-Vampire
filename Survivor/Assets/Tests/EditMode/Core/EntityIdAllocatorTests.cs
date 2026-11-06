using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class EntityIdAllocatorTests
    {
        [Test]
        public void FirstAllocations_AreSequentialAtGenerationOne()
        {
            var allocator = new EntityIdAllocator();
            Assert.AreEqual(new EntityId(1, 1), allocator.Allocate());   // the player is always 1:1
            Assert.AreEqual(new EntityId(2, 1), allocator.Allocate());   // the player magnet is 2:1
            Assert.AreEqual(new EntityId(3, 1), allocator.Allocate());
            Assert.AreEqual(3, allocator.LiveCount);
        }

        [Test]
        public void ReusedIndex_HasGenerationOneHigher()
        {
            // TAC-0-9.
            var allocator = new EntityIdAllocator();
            allocator.Allocate();
            EntityId second = allocator.Allocate();
            allocator.Free(second);
            EntityId reused = allocator.Allocate();
            Assert.AreEqual(second.Index, reused.Index);
            Assert.AreEqual(second.Generation + 1, reused.Generation);
            Assert.AreNotEqual(second, reused);
        }

        [Test]
        public void FreedIndices_AreReusedLastInFirstOut()
        {
            var allocator = new EntityIdAllocator();
            for (int i = 0; i < 5; i++)
            {
                allocator.Allocate();
            }

            allocator.Free(new EntityId(3, 1));
            allocator.Free(new EntityId(5, 1));
            Assert.AreEqual(new EntityId(5, 2), allocator.Allocate());
            Assert.AreEqual(new EntityId(3, 2), allocator.Allocate());
            Assert.AreEqual(new EntityId(6, 1), allocator.Allocate());
        }

        [Test]
        public void IsLive_TracksAllocationAndFree()
        {
            var allocator = new EntityIdAllocator();
            EntityId id = allocator.Allocate();
            Assert.IsTrue(allocator.IsLive(id));
            allocator.Free(id);
            Assert.IsFalse(allocator.IsLive(id));
            EntityId next = allocator.Allocate();
            Assert.IsTrue(allocator.IsLive(next));
            Assert.IsFalse(allocator.IsLive(id));                  // stale generation
            Assert.IsFalse(allocator.IsLive(EntityId.None));
            Assert.IsFalse(allocator.IsLive(new EntityId(99, 1)));  // never issued
            Assert.AreEqual(1, allocator.LiveCount);
        }

        [Test]
        public void FreeOfNonLiveId_Throws()
        {
            var allocator = new EntityIdAllocator();
            EntityId id = allocator.Allocate();
            allocator.Free(id);
            Assert.Throws<InvalidOperationException>(() => allocator.Free(id));                 // double free
            Assert.Throws<InvalidOperationException>(() => allocator.Free(EntityId.None));
            Assert.Throws<InvalidOperationException>(() => allocator.Free(new EntityId(7, 1))); // never issued
            EntityId reused = allocator.Allocate();
            Assert.Throws<InvalidOperationException>(() => allocator.Free(new EntityId(reused.Index, reused.Generation + 1)));
            Assert.AreEqual(1, allocator.LiveCount);
        }

        [Test]
        public void Reset_RestartsAtOneOneAndGenerationsReturnToOne()
        {
            // D26: generation uniqueness holds within one run only; reset makes same-seed reruns issue the same ids.
            var allocator = new EntityIdAllocator();
            EntityId first = allocator.Allocate();
            EntityId second = allocator.Allocate();
            allocator.Free(second);
            allocator.Allocate();

            allocator.Reset();
            Assert.AreEqual(0, allocator.LiveCount);
            Assert.IsFalse(allocator.IsLive(first));
            Assert.AreEqual(new EntityId(1, 1), allocator.Allocate());
            Assert.AreEqual(new EntityId(2, 1), allocator.Allocate());
        }

        [Test]
        public void SameOperationSequence_GivesSameIds()
        {
            var a = new EntityIdAllocator();
            var b = new EntityIdAllocator();
            var rng = new DeterministicRng(3UL, 1UL);
            var liveA = new System.Collections.Generic.List<EntityId>();
            var liveB = new System.Collections.Generic.List<EntityId>();
            for (int i = 0; i < 2000; i++)
            {
                bool free = liveA.Count > 0 && rng.Chance(0.45f);
                if (free)
                {
                    int k = rng.NextInt(liveA.Count);
                    a.Free(liveA[k]);
                    b.Free(liveB[k]);
                    liveA.RemoveAt(k);
                    liveB.RemoveAt(k);
                }
                else
                {
                    liveA.Add(a.Allocate());
                    liveB.Add(b.Allocate());
                    Assert.AreEqual(liveA[liveA.Count - 1], liveB[liveB.Count - 1]);
                }
            }

            Assert.AreEqual(a.LiveCount, b.LiveCount);
            Assert.AreEqual(liveA.Count, a.LiveCount);
        }
    }
}
