namespace Survivor.Core
{
    /// (rev 2) Registration hook for Unity physics bodies; implemented by Strategies, called by EntityLifecycle.
    public interface IPhysicsBodyRegistry
    {
        /// Teleports rb/transform to body.Position; enables the collider if required (R19).
        void Register(CollisionBody body);

        /// Disables the collider (no Exit callbacks: callbacksOnDisable = false).
        void Unregister(CollisionBody body);
    }
}
