using Survivor.Core;

namespace Survivor.Simulation
{
    /// Core-interface strategy instances the composition root (StrategyFactory) built for the next run (5.14, TAC-F8-2 step 3).
    /// A value bundle, not an interface; RunSession.Reset hands each one to the object it replaces.
    public readonly struct RunStrategies
    {
        /// Pool strategy of the next run (EntityLifecycle.ReplaceStrategies).
        public readonly IPoolStrategy Pool;

        /// Tick strategy of the next run (EntityLifecycle.ReplaceStrategies).
        public readonly ITickStrategy Tick;

        /// Collision strategy of the next run (EntityLifecycle, CollisionEventRouter and the player registration).
        public readonly ICollisionStrategy Collision;

        /// Target provider of the next run (TargetProviderSlot.Replace).
        public readonly ITargetProvider Target;

        /// Bundles the four strategies; RunSession.Reset rejects a bundle with a null member.
        public RunStrategies(IPoolStrategy pool, ITickStrategy tick, ICollisionStrategy collision, ITargetProvider target)
        {
            Pool = pool;
            Tick = tick;
            Collision = collision;
            Target = target;
        }
    }
}
