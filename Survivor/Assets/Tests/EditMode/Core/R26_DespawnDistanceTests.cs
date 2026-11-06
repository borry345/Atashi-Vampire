using System;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R26_DespawnDistanceTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(200)]
        [TestCase(400)]
        public void UpToFourHundred_IsFortyFive(int n)
        {
            Assert.AreEqual(45f, SpawnGeometry.DespawnDistanceFor(n));
        }

        [Test]
        public void FourHundredOne_ScalesWithSquareRoot()
        {
            float d = SpawnGeometry.DespawnDistanceFor(401);
            Assert.Greater(d, 45f);
            Assert.AreEqual(45f * Mathf.Sqrt(401f / 400f), d);
            Assert.AreEqual(45.0562f, d, 1e-3f);
        }

        [Test]
        public void SixteenHundred_IsNinety()
        {
            Assert.AreEqual(90f, SpawnGeometry.DespawnDistanceFor(1600), 1e-4f);
        }

        [Test]
        public void Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnGeometry.DespawnDistanceFor(-1));
        }
    }
}
