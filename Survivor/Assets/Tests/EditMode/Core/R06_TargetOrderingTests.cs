using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R06_TargetOrderingTests
    {
        [Test]
        public void SmallerSquaredDistance_Wins()
        {
            Assert.IsTrue(TargetOrdering.IsBetter(1f, new EntityId(9, 9), 2f, new EntityId(1, 1)));
            Assert.IsFalse(TargetOrdering.IsBetter(2f, new EntityId(1, 1), 1f, new EntityId(9, 9)));
        }

        [Test]
        public void EqualDistance_SmallerIndexWins()
        {
            Assert.IsTrue(TargetOrdering.IsBetter(4f, new EntityId(3, 7), 4f, new EntityId(5, 1)));
            Assert.IsFalse(TargetOrdering.IsBetter(4f, new EntityId(5, 1), 4f, new EntityId(3, 7)));
        }

        [Test]
        public void EqualDistanceAndIndex_SmallerGenerationWins()
        {
            Assert.IsTrue(TargetOrdering.IsBetter(4f, new EntityId(3, 1), 4f, new EntityId(3, 2)));
            Assert.IsFalse(TargetOrdering.IsBetter(4f, new EntityId(3, 2), 4f, new EntityId(3, 1)));
        }

        [Test]
        public void IdenticalCandidate_IsNotBetter()
        {
            Assert.IsFalse(TargetOrdering.IsBetter(4f, new EntityId(3, 1), 4f, new EntityId(3, 1)));
        }

        [Test]
        public void AnyFiniteCandidate_BeatsInitialState()
        {
            Assert.IsTrue(TargetOrdering.IsBetter(1e30f, new EntityId(400, 9), float.PositiveInfinity, EntityId.None));
        }

        [Test]
        public void ComparesSquaredDistanceBitsNotRoots()
        {
            // Two squared distances one ulp apart: the smaller wins regardless of id.
            float d2 = 2f;
            float next = System.BitConverter.Int32BitsToSingle(System.BitConverter.SingleToInt32Bits(d2) + 1);
            Assert.IsTrue(TargetOrdering.IsBetter(d2, new EntityId(9, 1), next, new EntityId(1, 1)));
        }
    }
}
