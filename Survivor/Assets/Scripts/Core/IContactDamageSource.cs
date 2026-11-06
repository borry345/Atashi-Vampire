namespace Survivor.Core
{
    /// <summary>
    /// An entity that damages the player on contact: Monster (definition ContactDamage), EnemyProjectile (Damage),
    /// EnemyHazardZone (zone damage). Consumed by PlayerDamageReceiver (channels 3, 4, 5) through a
    /// Func&lt;EntityHandle, IContactDamageSource&gt; lookup (TECH 5.12, rev 16).
    /// </summary>
    public interface IContactDamageSource
    {
        /// <summary>Damage dealt to the player by one hit (>= 0).</summary>
        float ContactDamage { get; }
    }
}
