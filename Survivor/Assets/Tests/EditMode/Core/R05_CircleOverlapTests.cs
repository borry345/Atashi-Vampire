using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R05_CircleOverlapTests
    {
        [Test]
        public void ExactTouch_CountsAsOverlap()
        {
            // Distance 3 == radius sum 3 (exact in float): boundary is inclusive (U9).
            Assert.IsTrue(CircleMath.Overlaps(new Vector2(0f, 0f), 1f, new Vector2(3f, 0f), 2f));
            Assert.IsTrue(CircleMath.Overlaps(new Vector2(-3f, -4f), 2f, new Vector2(0f, 0f), 3f));
        }

        [Test]
        public void JustBeyondTouch_DoesNotOverlap()
        {
            Assert.IsFalse(CircleMath.Overlaps(new Vector2(0f, 0f), 1f, new Vector2(3.001f, 0f), 2f));
            Assert.IsFalse(CircleMath.Overlaps(new Vector2(-10f, -10f), 0.5f, new Vector2(10f, 10f), 0.5f));
        }

        [Test]
        public void Inside_OverlapsAndIsSymmetric()
        {
            var a = new Vector2(-1.5f, 2f);
            var b = new Vector2(-1f, 2.25f);
            Assert.IsTrue(CircleMath.Overlaps(a, 0.3f, b, 0.3f));
            Assert.IsTrue(CircleMath.Overlaps(b, 0.3f, a, 0.3f));
            Assert.IsTrue(CircleMath.Overlaps(a, 0f, a, 0f));
        }
    }
}
