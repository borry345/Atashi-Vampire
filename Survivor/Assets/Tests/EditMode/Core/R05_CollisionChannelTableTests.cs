using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R05_CollisionChannelTableTests
    {
        private static readonly object[] Channels =
        {
            new object[] { CollisionChannel.PlayerProjectileVsMonster, CollisionLayer.PlayerProjectile, CollisionLayer.Monster },
            new object[] { CollisionChannel.PlayerOrbVsMonster, CollisionLayer.PlayerOrb, CollisionLayer.Monster },
            new object[] { CollisionChannel.PlayerZoneVsMonster, CollisionLayer.PlayerZone, CollisionLayer.Monster },
            new object[] { CollisionChannel.PlayerBodyVsMonster, CollisionLayer.PlayerBody, CollisionLayer.Monster },
            new object[] { CollisionChannel.EnemyProjectileVsPlayerBody, CollisionLayer.EnemyProjectile, CollisionLayer.PlayerBody },
            new object[] { CollisionChannel.EnemyZoneVsPlayerBody, CollisionLayer.EnemyZone, CollisionLayer.PlayerBody },
            new object[] { CollisionChannel.PlayerMagnetVsGem, CollisionLayer.PlayerMagnet, CollisionLayer.Gem },
            new object[] { CollisionChannel.PlayerBodyVsGem, CollisionLayer.PlayerBody, CollisionLayer.Gem },
            new object[] { CollisionChannel.PlayerBodyVsChest, CollisionLayer.PlayerBody, CollisionLayer.Chest },
        };

        [TestCaseSource(nameof(Channels))]
        public void TryGetChannel_FindsChannelInBothOrders(CollisionChannel expected, CollisionLayer a, CollisionLayer b)
        {
            Assert.IsTrue(CollisionChannelTable.TryGetChannel(a, b, out CollisionChannel ch, out bool xIsA));
            Assert.AreEqual(expected, ch);
            Assert.IsTrue(xIsA);

            Assert.IsTrue(CollisionChannelTable.TryGetChannel(b, a, out ch, out xIsA));
            Assert.AreEqual(expected, ch);
            Assert.IsFalse(xIsA);

            CollisionChannelTable.GetLayers(expected, out CollisionLayer gotA, out CollisionLayer gotB);
            Assert.AreEqual(a, gotA);
            Assert.AreEqual(b, gotB);
        }

        [Test]
        public void OnlyNineChannels_ProduceAPair()
        {
            int ordered = 0;
            for (int x = 0; x <= (int)CollisionLayer.Chest; x++)
            {
                for (int y = 0; y <= (int)CollisionLayer.Chest; y++)
                {
                    if (CollisionChannelTable.TryGetChannel((CollisionLayer)x, (CollisionLayer)y, out _, out _))
                    {
                        ordered++;
                    }
                }
            }

            Assert.AreEqual(18, ordered);
            Assert.AreEqual(9, CollisionChannelTable.ChannelCount);
        }

        [Test]
        public void NonChannelPairs_ReturnFalse()
        {
            Assert.IsFalse(CollisionChannelTable.TryGetChannel(CollisionLayer.Monster, CollisionLayer.Monster, out _, out _));
            Assert.IsFalse(CollisionChannelTable.TryGetChannel(CollisionLayer.PlayerBody, CollisionLayer.PlayerBody, out _, out _));
            Assert.IsFalse(CollisionChannelTable.TryGetChannel(CollisionLayer.Gem, CollisionLayer.Chest, out _, out _));
            Assert.IsFalse(CollisionChannelTable.TryGetChannel(CollisionLayer.PlayerMagnet, CollisionLayer.Monster, out _, out _));
            Assert.IsFalse(CollisionChannelTable.TryGetChannel(CollisionLayer.PlayerProjectile, CollisionLayer.EnemyProjectile, out _, out _));
        }

        [Test]
        public void GetLayers_UndefinedChannelThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CollisionChannelTable.GetLayers((CollisionChannel)9, out _, out _));
        }
    }
}
