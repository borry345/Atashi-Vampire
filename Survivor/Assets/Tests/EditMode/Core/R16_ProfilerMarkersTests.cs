using NUnit.Framework;
using Survivor.Core;
using Unity.Profiling;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R16_ProfilerMarkersTests
    {
        [Test]
        public void AllMarkers_CanBeginAndEnd()
        {
            ProfilerMarker[] markers =
            {
                ProfilerMarkers.Step, ProfilerMarkers.Pool, ProfilerMarkers.Targeting, ProfilerMarkers.SpatialIndex,
                ProfilerMarkers.TickMonsters, ProfilerMarkers.TickProjectiles, ProfilerMarkers.Collision,
            };
            foreach (ProfilerMarker marker in markers)
            {
                Assert.DoesNotThrow(() =>
                {
                    marker.Begin();
                    marker.End();
                });
            }
        }
    }
}
