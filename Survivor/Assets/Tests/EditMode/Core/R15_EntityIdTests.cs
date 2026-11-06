using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R15_EntityIdTests
    {
        [Test]
        public void ToString_IsIndexColonGeneration()
        {
            Assert.AreEqual("17:3", new EntityId(17, 3).ToString());
            Assert.AreEqual("1:1", new EntityId(1, 1).ToString());
            Assert.AreEqual("0:0", EntityId.None.ToString());
        }

        [Test]
        public void None_IsDefaultAndInvalid()
        {
            Assert.AreEqual(default(EntityId), EntityId.None);
            Assert.IsFalse(EntityId.None.IsValid);
            Assert.IsTrue(new EntityId(1, 1).IsValid);
        }

        [Test]
        public void Packed_PutsIndexHighAndGenerationLow()
        {
            Assert.AreEqual((17L << 32) | 3L, new EntityId(17, 3).Packed);
            Assert.AreEqual(0L, EntityId.None.Packed);
        }

        [Test]
        public void Equality_RequiresIndexAndGeneration()
        {
            var a = new EntityId(5, 2);
            Assert.IsTrue(a == new EntityId(5, 2));
            Assert.IsTrue(a != new EntityId(5, 3));
            Assert.IsTrue(a != new EntityId(6, 2));
            Assert.IsTrue(a.Equals((object)new EntityId(5, 2)));
            Assert.IsFalse(a.Equals("5:2"));

            var set = new HashSet<EntityId> { a, new EntityId(5, 3) };
            Assert.IsTrue(set.Contains(new EntityId(5, 2)));
            Assert.AreEqual(2, set.Count);
        }
    }
}
