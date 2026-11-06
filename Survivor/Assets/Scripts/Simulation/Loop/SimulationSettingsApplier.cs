using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Applies the time settings of the fixed-step loop only; Physics2D settings belong to PhysicsSettingsApplier (5.3, A12).
    public static class SimulationSettingsApplier
    {
        /// Sets Time.fixedDeltaTime = 1/60 and Time.maximumDeltaTime = 3 steps (0.05 s at 1x), capping catch-up at 3 steps per frame.
        public static void Apply()
        {
            Time.fixedDeltaTime = StepMath.StepSeconds;
            Time.maximumDeltaTime = StepMath.MaxCatchUpSteps * StepMath.StepSeconds;
        }
    }
}
