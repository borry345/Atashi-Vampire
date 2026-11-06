using System;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R07_SpawnPointTests
    {
        [Test]
        public void ZeroAngle_IsOnPositiveX()
        {
            Vector2 p = SpawnGeometry.PointOnCircle(new Vector2(3f, -2f), 20f, 0f);
            Assert.AreEqual(23f, p.x);
            Assert.AreEqual(-2f, p.y);
        }

        [TestCase(0.25f, 0f, 20f)]
        [TestCase(0.5f, -20f, 0f)]
        [TestCase(0.75f, 0f, -20f)]
        public void QuarterTurns_LandOnAxes(float u, float expectedX, float expectedY)
        {
            Vector2 p = SpawnGeometry.PointOnCircle(Vector2.zero, 20f, u);
            Assert.AreEqual(expectedX, p.x, 1e-4f);
            Assert.AreEqual(expectedY, p.y, 1e-4f);
        }

        [Test]
        public void Point_IsAtRadiusFromCenter()
        {
            var center = new Vector2(-100f, 250f);
            var rng = new DeterministicRng(7UL, 1UL);
            for (int i = 0; i < 100; i++)
            {
                Vector2 p = SpawnGeometry.PointOnCircle(center, GameRules.SpawnRadius, rng.NextFloat01());
                Assert.AreEqual(GameRules.SpawnRadius, (p - center).magnitude, 1e-3f);
            }
        }

        [Test]
        public void SameInput_GivesSameBits()
        {
            Vector2 a = SpawnGeometry.PointOnCircle(new Vector2(1.5f, 2.5f), 20f, 0.123456f);
            Vector2 b = SpawnGeometry.PointOnCircle(new Vector2(1.5f, 2.5f), 20f, 0.123456f);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.x), BitConverter.SingleToInt32Bits(b.x));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.y), BitConverter.SingleToInt32Bits(b.y));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnGeometry.PointOnCircle(Vector2.zero, 20f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnGeometry.PointOnCircle(Vector2.zero, 20f, 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnGeometry.PointOnCircle(Vector2.zero, 20f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnGeometry.PointOnCircle(Vector2.zero, -1f, 0.5f));
        }
    }
}
