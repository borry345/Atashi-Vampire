namespace Survivor.Core
{
    /// What the current strategy selection needs from Unity physics. Single owner: ColliderActivationPolicy (R19).
    public readonly struct PhysicsBodyRequirements
    {
        /// Collision == Engine || Target == Physics2DQuery.
        public readonly bool MonsterColliders;

        /// Collision == Engine.
        public readonly bool AllColliders;

        /// Target == Physics2DQuery.
        public readonly bool SyncMonstersAtBuildIndex;

        /// Collision == Engine.
        public readonly bool SimulateAtCollision;

        /// Creates a requirement set; normally obtained from ColliderActivationPolicy.Resolve.
        public PhysicsBodyRequirements(bool monsterColliders, bool allColliders, bool syncMonstersAtBuildIndex, bool simulateAtCollision)
        {
            MonsterColliders = monsterColliders;
            AllColliders = allColliders;
            SyncMonstersAtBuildIndex = syncMonstersAtBuildIndex;
            SimulateAtCollision = simulateAtCollision;
        }

        /// AllColliders || (layer == Monster && MonsterColliders).
        public bool CollidersEnabledFor(CollisionLayer layer)
        {
            return AllColliders || (layer == CollisionLayer.Monster && MonsterColliders);
        }
    }

    /// Decides which Unity physics bodies the strategy selection needs (rule R19).
    public static class ColliderActivationPolicy
    {
        /// Maps a strategy selection to its physics body requirements.
        public static PhysicsBodyRequirements Resolve(in StrategySelection s)
        {
            bool engine = s.Collision == CollisionStrategyKind.EngineCollider2D;
            bool physicsTarget = s.Target == TargetProviderKind.Physics2DQuery;
            return new PhysicsBodyRequirements(
                monsterColliders: engine || physicsTarget,
                allColliders: engine,
                syncMonstersAtBuildIndex: physicsTarget,
                simulateAtCollision: engine);
        }
    }
}
