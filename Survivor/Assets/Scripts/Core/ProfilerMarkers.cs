using Unity.Profiling;

namespace Survivor.Core
{
    /// Measurement markers shared by the loop, strategies and the stats collector (rule R16).
    public static class ProfilerMarkers
    {
        /// Whole simulation step.
        public static readonly ProfilerMarker Step = new ProfilerMarker("Survivor.Step");

        /// Pool acquire/release.
        public static readonly ProfilerMarker Pool = new ProfilerMarker("Survivor.Pool");

        /// Target queries, including the BuildIndex monster body sync.
        public static readonly ProfilerMarker Targeting = new ProfilerMarker("Survivor.Targeting");

        /// Spatial index rebuild.
        public static readonly ProfilerMarker SpatialIndex = new ProfilerMarker("Survivor.SpatialIndex");

        /// Monster tick group.
        public static readonly ProfilerMarker TickMonsters = new ProfilerMarker("Survivor.Tick.Monsters");

        /// Projectile tick group.
        public static readonly ProfilerMarker TickProjectiles = new ProfilerMarker("Survivor.Tick.Projectiles");

        /// Collision detection, including engine sync and Physics2D.Simulate.
        public static readonly ProfilerMarker Collision = new ProfilerMarker("Survivor.Collision");
    }
}
