namespace Survivor.Core
{
    /// Object pool strategy (F9).
    public enum PoolStrategyKind { InstantiateDestroy = 0, CustomPool = 1, UnityObjectPool = 2 }

    /// Nearest-target provider strategy (F16).
    public enum TargetProviderKind { BruteForce = 0, SpatialHash = 1, Physics2DQuery = 2 }

    /// Update strategy for monsters and projectiles (F17).
    public enum TickStrategyKind { PerObjectFixedUpdate = 0, CentralTick = 1 }

    /// Collision detection strategy (F18).
    public enum CollisionStrategyKind { EngineCollider2D = 0, SelfSquaredDistance = 1 }
}
