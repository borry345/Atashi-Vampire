using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// One W2/E2 orb. No lifetime and no tick of its own: the owning OrbitWeapon writes its position every WeaponTick (TAC-F6-4).
    /// Damage is applied by the owning weapon instance's tracker (channel 1, TAC-F6-6).
    public sealed class OrbitOrb : SimEntity
    {
        /// Weapon id of the owning instance.
        public string WeaponId { get; private set; }

        /// Weapon slot of the owning instance.
        public int SlotIndex { get; private set; }

        /// Index i of this orb in theta_i = theta0 + w*step + 2*pi*i/orbCount.
        public int OrbIndex { get; private set; }

        internal WeaponInstance Owner { get; private set; }

        // Monsters this entity overlaps now. Despawning it emits no Exit, so WeaponInventory replays these as tracker Exits.
        internal List<EntityHandle> Overlaps { get; } = new List<EntityHandle>();

        internal void Initialize(WeaponInstance owner, int orbIndex, float radius)
        {
            Owner = owner;
            WeaponId = owner.Id;
            SlotIndex = owner.SlotIndex;
            OrbIndex = orbIndex;
            Overlaps.Clear();
            Body.Radius = radius;
            Body.IsLarge = false;
        }

        internal void MoveTo(Vector2 position)
        {
            Position = position;
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
