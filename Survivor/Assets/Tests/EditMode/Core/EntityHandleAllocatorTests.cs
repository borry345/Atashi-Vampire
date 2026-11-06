using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class EntityHandleAllocatorTests
    {
        [Test]
        public void FirstAllocations_AreSequentialAtGenerationOne()
        {
            var allocator = new EntityHandleAllocator();
            Assert.AreEqual(new EntityHandle(1, 1), allocator.Allocate());   // the player is always 1:1
            Assert.AreEqual(new EntityHandle(2, 1), allocator.Allocate());   // the player magnet is 2:1
            Assert.AreEqual(new EntityHandle(3, 1), allocator.Allocate());
            Assert.AreEqual(3, allocator.LiveCount);
        }

        [Test]
        public void ReusedIndex_HasGenerationOneHigher()
        {
            // TAC-0-9.
            var allocator = new EntityHandleAllocator();
            allocator.Allocate();
            EntityHandle second = allocator.Allocate();
            allocator.Free(second);
            EntityHandle reused = allocator.Allocate();
            Assert.AreEqual(second.Index, reused.Index);
            Assert.AreEqual(second.Generation + 1, reused.Generation);
            Assert.AreNotEqual(second, reused);
        }

        [Test]
        public void FreedIndices_AreReusedLastInFirstOut()
        {
            var allocator = new EntityHandleAllocator();
            for (int i = 0; i < 5; i++)
            {
                allocator.Allocate();
            }

            allocator.Free(new EntityHandle(3, 1));
            allocator.Free(new EntityHandle(5, 1));
            Assert.AreEqual(new EntityHandle(5, 2), allocator.Allocate());
            Assert.AreEqual(new EntityHandle(3, 2), allocator.Allocate());
            Assert.AreEqual(new EntityHandle(6, 1), allocator.Allocate());
        }

        [Test]
        public void IsLive_TracksAllocationAndFree()
        {
            var allocator = new EntityHandleAllocator();
            EntityHandle id = allocator.Allocate();
            Assert.IsTrue(allocator.IsLive(id));
            allocator.Free(id);
            Assert.IsFalse(allocator.IsLive(id));
            EntityHandle next = allocator.Allocate();
            Assert.IsTrue(allocator.IsLive(next));
            Assert.IsFalse(allocator.IsLive(id));                  // stale generation
            Assert.IsFalse(allocator.IsLive(EntityHandle.None));
            Assert.IsFalse(allocator.IsLive(new EntityHandle(99, 1)));  // never issued
            Assert.AreEqual(1, allocator.LiveCount);
        }

        [Test]
        public void FreeOfNonLiveId_Throws()
        {
            var allocator = new EntityHandleAllocator();
            EntityHandle id = allocator.Allocate();
            allocator.Free(id);
            Assert.Throws<InvalidOperationException>(() => allocator.Free(id));                 // double free
            Assert.Throws<InvalidOperationException>(() => allocator.Free(EntityHandle.None));
            Assert.Throws<InvalidOperationException>(() => allocator.Free(new EntityHandle(7, 1))); // never issued
            EntityHandle reused = allocator.Allocate();
            Assert.Throws<InvalidOperationException>(() => allocator.Free(new EntityHandle(reused.Index, reused.Generation + 1)));
            Assert.AreEqual(1, allocator.LiveCount);
        }

        [Test]
        public void Reset_RestartsAtOneOneAndGenerationsReturnToOne()
        {
            // D26: generation uniqueness holds within one run only; reset makes same-seed reruns issue the same ids.
            var allocator = new EntityHandleAllocator();
            EntityHandle first = allocator.Allocate();
            EntityHandle second = allocator.Allocate();
            allocator.Free(second);
            allocator.Allocate();

            allocator.Reset();
            Assert.AreEqual(0, allocator.LiveCount);
            Assert.IsFalse(allocator.IsLive(first));
            Assert.AreEqual(new EntityHandle(1, 1), allocator.Allocate());
            Assert.AreEqual(new EntityHandle(2, 1), allocator.Allocate());
        }

        [Test]
        public void SameOperationSequence_GivesSameIds()
        {
            var a = new EntityHandleAllocator();
            var b = new EntityHandleAllocator();
            var rng = new DeterministicRng(3UL, 1UL);
            var liveA = new System.Collections.Generic.List<EntityHandle>();
            var liveB = new System.Collections.Generic.List<EntityHandle>();
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
