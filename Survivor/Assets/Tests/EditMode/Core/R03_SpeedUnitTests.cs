using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R03_SpeedUnitTests
    {
        [Test]
        public void PerSecondToPerStep_DividesBySixty()
        {
            Assert.AreEqual(0.1f, StepMath.PerSecondToPerStep(6f));
            Assert.AreEqual(0f, StepMath.PerSecondToPerStep(0f));
            Assert.AreEqual(1f, StepMath.PerSecondToPerStep(60f));
        }

        [Test]
        public void DegPerSecondToRadPerStep_ConvertsDegreesToRadiansPerStep()
        {
            Assert.AreEqual(Mathf.PI / 60f, StepMath.DegPerSecondToRadPerStep(180f), 1e-7f);
            Assert.AreEqual(2f * Mathf.PI / 60f, StepMath.DegPerSecondToRadPerStep(360f), 1e-7f);
            Assert.AreEqual(0f, StepMath.DegPerSecondToRadPerStep(0f));
        }
    }
}
