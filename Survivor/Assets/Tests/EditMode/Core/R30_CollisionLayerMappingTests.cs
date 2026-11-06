using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R30_CollisionLayerMappingTests
    {
        [Test]
        public void UnityLayer_IsEightPlusLayerValue()
        {
            Assert.AreEqual(8, CollisionLayerMapping.UnityLayerOf(CollisionLayer.PlayerBody));
            Assert.AreEqual(10, CollisionLayerMapping.UnityLayerOf(CollisionLayer.Monster));
            Assert.AreEqual(17, CollisionLayerMapping.UnityLayerOf(CollisionLayer.Chest));
            for (int layer = 0; layer <= (int)CollisionLayer.Chest; layer++)
            {
                Assert.AreEqual(8 + layer, CollisionLayerMapping.UnityLayerOf((CollisionLayer)layer));
            }
        }

        [Test]
        public void MaskOf_IsSingleBitOfUnityLayer()
        {
            Assert.AreEqual(1 << 10, CollisionLayerMapping.MaskOf(CollisionLayer.Monster));
            Assert.AreEqual(1 << 8, CollisionLayerMapping.MaskOf(CollisionLayer.PlayerBody));
            Assert.AreEqual(1 << 17, CollisionLayerMapping.MaskOf(CollisionLayer.Chest));
        }

        [Test]
        public void UndefinedLayer_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CollisionLayerMapping.UnityLayerOf((CollisionLayer)10));
        }
    }
}
