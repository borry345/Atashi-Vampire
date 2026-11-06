using UnityEngine;

namespace Survivor.Simulation
{
    /// Resets SimulationClock.StepsThisFrame once per frame, after the frame's FixedUpdates and before any other Update.
    [DefaultExecutionOrder(-10000)]
    public sealed class SimulationFrameCounter : MonoBehaviour
    {
        private SimulationClock _clock;

        /// Steps the clock ran in the FixedUpdates of the latest frame (captured just before the reset).
        public int LastFrameSteps { get; private set; }

        /// Binds the clock whose per-frame counter this component resets; null throws ArgumentNullException.
        public void Initialize(SimulationClock clock)
        {
            _clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
        }

        private void Update()
        {
            if (_clock == null)
            {
                return;
            }

            // FixedUpdate runs before Update in the player loop, so readers in Update/LateUpdate would always see 0 after the reset.
            LastFrameSteps = _clock.StepsThisFrame;
            _clock.ResetStepsThisFrame();
        }
    }
}
