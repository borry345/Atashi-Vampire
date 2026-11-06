using Survivor.Core;

namespace Survivor.Simulation
{
    // Opens/closes the step and tick-group profiler markers across the three drivers and closes whatever is open on abort.
    internal sealed class StepMarkerTracker
    {
        private bool _stepOpen;
        private bool _monstersOpen;
        private bool _projectilesOpen;

        internal void BeginStep()
        {
            ProfilerMarkers.Step.Begin();
            _stepOpen = true;
        }

        internal void EndStep()
        {
            if (_stepOpen)
            {
                ProfilerMarkers.Step.End();
                _stepOpen = false;
            }
        }

        internal void BeginMonsters()
        {
            ProfilerMarkers.TickMonsters.Begin();
            _monstersOpen = true;
        }

        internal void EndMonsters()
        {
            if (_monstersOpen)
            {
                ProfilerMarkers.TickMonsters.End();
                _monstersOpen = false;
            }
        }

        internal void BeginProjectiles()
        {
            ProfilerMarkers.TickProjectiles.Begin();
            _projectilesOpen = true;
        }

        internal void EndProjectiles()
        {
            if (_projectilesOpen)
            {
                ProfilerMarkers.TickProjectiles.End();
                _projectilesOpen = false;
            }
        }

        // Innermost first so nested markers are closed in a valid order.
        internal void EndAll()
        {
            EndProjectiles();
            EndMonsters();
            EndStep();
        }
    }
}
