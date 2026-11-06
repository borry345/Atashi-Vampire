using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;

namespace Survivor.Tests.EditMode.Spatial
{
    public sealed class SpatialHashSettingsTests
    {
        [Test]
        public void CellSize_DefaultsToCoreConstant()
        {
            Assert.AreEqual(StrategySelection.DefaultSpatialHashCellSize, new SpatialHashSettings().CellSize);
        }

        [Test]
        public void CellSize_CanBeChanged()
        {
            var settings = new SpatialHashSettings { CellSize = 3.25f };
            Assert.AreEqual(3.25f, settings.CellSize);
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void CellSize_Invalid_ThrowsAndKeepsPreviousValue(float value)
        {
            var settings = new SpatialHashSettings();
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.CellSize = value);
            Assert.AreEqual(StrategySelection.DefaultSpatialHashCellSize, settings.CellSize);
            Assert.IsFalse(SpatialHashSettings.IsValidCellSize(value));
        }
    }
}
