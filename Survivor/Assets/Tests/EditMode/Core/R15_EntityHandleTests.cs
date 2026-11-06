using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R15_EntityHandleTests
    {
        [Test]
        public void ToString_IsIndexColonGeneration()
        {
            Assert.AreEqual("17:3", new EntityHandle(17, 3).ToString());
            Assert.AreEqual("1:1", new EntityHandle(1, 1).ToString());
            Assert.AreEqual("0:0", EntityHandle.None.ToString());
        }

        [Test]
        public void None_IsDefaultAndInvalid()
        {
            Assert.AreEqual(default(EntityHandle), EntityHandle.None);
            Assert.IsFalse(EntityHandle.None.IsValid);
            Assert.IsTrue(new EntityHandle(1, 1).IsValid);
        }

        [Test]
        public void Packed_PutsIndexHighAndGenerationLow()
        {
            Assert.AreEqual((17L << 32) | 3L, new EntityHandle(17, 3).Packed);
            Assert.AreEqual(0L, EntityHandle.None.Packed);
        }

        [Test]
        public void Equality_RequiresIndexAndGeneration()
        {
            var a = new EntityHandle(5, 2);
            Assert.IsTrue(a == new EntityHandle(5, 2));
            Assert.IsTrue(a != new EntityHandle(5, 3));
            Assert.IsTrue(a != new EntityHandle(6, 2));
            Assert.IsTrue(a.Equals((object)new EntityHandle(5, 2)));
            Assert.IsFalse(a.Equals("5:2"));

            var set = new HashSet<EntityHandle> { a, new EntityHandle(5, 3) };
            Assert.IsTrue(set.Contains(new EntityHandle(5, 2)));
            Assert.AreEqual(2, set.Count);
        }
    }
}
