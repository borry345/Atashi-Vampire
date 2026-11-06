using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;

namespace Survivor.Tests.EditMode.Spatial
{
    public sealed class SpatialHashTargetProviderTests
    {
        [Test]
        public void Constructor_NullIndex_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SpatialHashTargetProvider(null));
        }

        [Test]
        public void OnSnapshotBuilt_ForeignBuffer_Throws()
        {
            var index = new MonsterSpatialIndex(2f, new MonsterSnapshotBuffer());
            var provider = new SpatialHashTargetProvider(index);

            Assert.Throws<ArgumentException>(() => provider.OnSnapshotBuilt(new MonsterSnapshotBuffer()));
        }
    }
}
