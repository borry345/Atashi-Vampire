using System;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R17_MoveMathTests
    {
        [TestCase(1, 0, 1f, 0f)]
        [TestCase(-1, 0, -1f, 0f)]
        [TestCase(0, 1, 0f, 1f)]
        [TestCase(0, -1, 0f, -1f)]
        [TestCase(0, 0, 0f, 0f)]
        public void AxisInputs_GiveAxisVectors(int x, int y, float ex, float ey)
        {
            Vector2 d = MoveMath.ToUnitDirection(x, y);
            Assert.AreEqual(ex, d.x);
            Assert.AreEqual(ey, d.y);
        }

        [TestCase(1, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        [TestCase(-1, -1)]
        public void Diagonals_UseLiteralComponent(int x, int y)
        {
            Vector2 d = MoveMath.ToUnitDirection(x, y);
            int literalBits = BitConverter.SingleToInt32Bits(0.70710677f);
            Assert.AreEqual(literalBits, BitConverter.SingleToInt32Bits(Math.Abs(d.x)));
            Assert.AreEqual(literalBits, BitConverter.SingleToInt32Bits(Math.Abs(d.y)));
            Assert.AreEqual(Math.Sign(x), Math.Sign(d.x));
            Assert.AreEqual(Math.Sign(y), Math.Sign(d.y));
        }

        [Test]
        public void DiagonalAndAxisStepLengths_DifferByAtMostOneMillionth()
        {
            // TAC-F1-2.
            float diagonal = MoveMath.ToUnitDirection(1, 1).magnitude;
            float axis = MoveMath.ToUnitDirection(1, 0).magnitude;
            Assert.LessOrEqual(Math.Abs(diagonal - axis), 1e-6f);
        }

        [TestCase(2, 0)]
        [TestCase(0, -2)]
        [TestCase(int.MinValue, 0)]
        public void OutOfRangeInput_Throws(int x, int y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MoveMath.ToUnitDirection(x, y));
        }

        [Test]
        public void DirectionOrDefault_ZeroGivesRight()
        {
            Assert.AreEqual(Vector2.right, MoveMath.DirectionOrDefault(Vector2.zero));
        }

        [Test]
        public void DirectionOrDefault_NonZeroIsUnchanged()
        {
            var up = new Vector2(0f, 1f);
            Assert.AreEqual(up, MoveMath.DirectionOrDefault(up));

            var tiny = new Vector2(1e-30f, 0f);
            Vector2 result = MoveMath.DirectionOrDefault(tiny);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(tiny.x), BitConverter.SingleToInt32Bits(result.x));
            Assert.AreEqual(0f, result.y);
        }
    }
}
