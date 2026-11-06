using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Tick
{
    /// F17 A: per-projectile FixedUpdate between SimulationDriverMid (0) and SimulationDriverLate (1000); enabled only while registered.
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class ProjectileSelfTicker : MonoBehaviour
    {
        private IUpdatable _owner;
        private IStepClockReader _clock;

        /// Updatable this ticker drives, or null while unbound.
        public IUpdatable Owner => _owner;

        internal void Bind(IUpdatable owner, IStepClockReader clock)
        {
            _owner = owner;
            _clock = clock;
        }

        internal void Unbind()
        {
            _owner = null;
            _clock = null;
        }

        private void FixedUpdate()
        {
            if (_clock == null || !_clock.StepActive)
            {
                return;
            }

            _owner.Tick(_clock.CurrentContext);
        }
    }
}
