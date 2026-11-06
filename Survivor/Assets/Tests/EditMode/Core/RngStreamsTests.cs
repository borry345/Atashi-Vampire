using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class RngStreamsTests
    {
        [Test]
        public void Get_ReturnsSameInstanceUntilReseed()
        {
            var streams = new RngStreams(42UL);
            DeterministicRng spawn = streams.Get(RngStream.Spawn);
            Assert.AreSame(spawn, streams.Get(RngStream.Spawn));
            streams.Reseed(42UL);
            Assert.AreNotSame(spawn, streams.Get(RngStream.Spawn));
        }

        [Test]
        public void StreamValues_AreDocumented()
        {
            Assert.AreEqual(1, (int)RngStream.Spawn);
            Assert.AreEqual(2, (int)RngStream.Summon);
            Assert.AreEqual(3, (int)RngStream.Drop);
            Assert.AreEqual(4, (int)RngStream.Chest);
            Assert.AreEqual(5, (int)RngStream.LevelUp);
        }

        [Test]
        public void UsingOneStream_DoesNotShiftAnother()
        {
            var a = new RngStreams(2026UL);
            var b = new RngStreams(2026UL);
            DeterministicRng levelUp = b.Get(RngStream.LevelUp);
            for (int i = 0; i < 100; i++)
            {
                levelUp.NextUInt();
            }

            DeterministicRng spawnA = a.Get(RngStream.Spawn);
            DeterministicRng spawnB = b.Get(RngStream.Spawn);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(spawnA.NextUInt(), spawnB.NextUInt());
            }
        }

        [Test]
        public void Streams_DifferFromEachOther()
        {
            var streams = new RngStreams(7UL);
            var first = new uint[5];
            for (int s = 1; s <= 5; s++)
            {
                first[s - 1] = streams.Get((RngStream)s).NextUInt();
            }

            CollectionAssert.AllItemsAreUnique(first);
        }

        [Test]
        public void Reseed_RestartsFromTheNewSeed()
        {
            var streams = new RngStreams(1UL);
            streams.Get(RngStream.Drop).NextUInt();
            streams.Reseed(99UL);
            Assert.AreEqual(99UL, streams.MasterSeed);

            var fresh = new RngStreams(99UL);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(fresh.Get(RngStream.Drop).NextUInt(), streams.Get(RngStream.Drop).NextUInt());
            }
        }

        [Test]
        public void SameMasterSeed_GivesSameStreams()
        {
            var a = new RngStreams(0xDEADBEEFUL);
            var b = new RngStreams(0xDEADBEEFUL);
            for (int s = 1; s <= 5; s++)
            {
                for (int i = 0; i < 20; i++)
                {
                    Assert.AreEqual(a.Get((RngStream)s).NextUInt(), b.Get((RngStream)s).NextUInt());
                }
            }
        }

        [TestCase(0)]
        [TestCase(6)]
        public void UndefinedStream_Throws(int value)
        {
            var streams = new RngStreams(1UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => streams.Get((RngStream)value));
        }
    }
}
