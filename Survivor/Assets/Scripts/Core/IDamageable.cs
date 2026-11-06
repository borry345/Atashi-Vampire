namespace Survivor.Core
{
    /// <summary>
    /// An entity that can take weapon damage. Implemented by Monster (Simulation); consumed by the player's
    /// hit handler (channel 0) and persistent-damage resolver through a Func&lt;EntityHandle, IDamageable&gt; lookup
    /// (TECH 5.12, rev 16).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>False once health reached 0; stays false until the entity is despawned and reused.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Subtracts amount from health at step. A call while !IsAlive is ignored by the implementation
        /// (extra damage in the same step is dropped; the kill is confirmed once by KillPipeline).
        /// amount must be finite and >= 0, otherwise ArgumentOutOfRangeException.
        /// </summary>
        void ApplyDamage(float amount, long step);
    }
}
