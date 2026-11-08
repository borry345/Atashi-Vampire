using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// One W3 zone. Static, despawned by LifetimeSystem after durationSteps (TAC-F6-5); damage is applied by the owning weapon
    /// instance's tracker (channel 2), so several zones of one weapon share one cooldown record per target (TAC-F6-6).
    public sealed class DamageZone : SimEntity
    {
        /// Weapon id of the owning instance.
        public string WeaponId { get; private set; }

        /// Weapon slot of the owning instance.
        public int SlotIndex { get; private set; }

        internal WeaponInstance Owner { get; private set; }

        // Monsters this entity overlaps now. Despawning it emits no Exit, so WeaponInventory replays these as tracker Exits.
        internal List<EntityHandle> Overlaps { get; } = new List<EntityHandle>();

        internal void Initialize(WeaponInstance owner, float radius)
        {
            Owner = owner;
            WeaponId = owner.Id;
            SlotIndex = owner.SlotIndex;
            Overlaps.Clear();
            Body.Radius = radius;
            Body.IsLarge = false;
        }

        /// Drops the owner reference before the instance returns to the pool.
        protected override void OnReleased()
        {
            Owner = null;
            WeaponId = null;
            Overlaps.Clear();
        }
    }
}
