using Survivor.Core;

namespace Survivor.Data
{
    /// One object kind checked by the tunneling invariant: its collision layer, speed (units per second) and radius.
    public readonly struct MovingObjectSpec
    {
        /// Where the values come from (e.g. "W1.levels[0]", "M2.charge", "playerStats").
        public readonly string Source;

        /// Collision layer of the object (R5 channels decide stage-2 pairs).
        public readonly CollisionLayer Layer;

        /// Maximum speed in units per second.
        public readonly float SpeedPerSecond;

        /// Collision radius in world units.
        public readonly float Radius;

        /// Creates a spec.
        public MovingObjectSpec(string source, CollisionLayer layer, float speedPerSecond, float radius)
        {
            Source = source;
            Layer = layer;
            SpeedPerSecond = speedPerSecond;
            Radius = radius;
        }

        /// Distance moved in one step (speed / 60, R3).
        public float SpeedPerStep => StepMath.PerSecondToPerStep(SpeedPerSecond);

        /// True when the stage-1 invariant v/60 <= r holds.
        public bool SatisfiesInvariant => SpeedPerStep <= Radius;
    }
}
