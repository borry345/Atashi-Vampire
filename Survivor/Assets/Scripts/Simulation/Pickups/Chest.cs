namespace Survivor.Simulation
{
    /// Chest dropped by a killed monster (2%). No lifetime and no despawn rule (SPEC F4); opened on body contact by PickupSystem.
    public sealed class Chest : SimEntity
    {
        // Spawn initialize callback.
        internal void Initialize(float radius)
        {
            Body.Radius = radius;
            Body.IsLarge = false;
        }

        /// Nothing to clear: a chest carries no state besides its body.
        protected override void OnReleased()
        {
        }
    }
}
