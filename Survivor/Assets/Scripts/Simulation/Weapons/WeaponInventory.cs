using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// The player's weapon slots (GameRules.MaxWeaponSlots, fixed order). WeaponTick order 0: ticks each slot in slot order
    /// (fire commands, orb positions, orb count reconcile). Written only by PlayerProgressionApplier (A11).
    /// Subscribes to EntityLifecycle.Despawned to drop children and tracker records of despawned entities (TAC-0-10).
    public sealed class WeaponInventory : IStepPhaseHandler
    {
        private readonly WeaponInstance[] _slots = new WeaponInstance[GameRules.MaxWeaponSlots];
        private readonly List<string> _consumedByEvolution = new List<string>();
        private readonly PlayerEntity _player;
        private readonly PassiveInventory _passives;
        private readonly EntityLifecycle _lifecycle;

        /// Creates empty slots for player (command Source = player body id), reading P1 from passives; subscribes to
        /// lifecycle.Despawned. Null arguments throw ArgumentNullException.
        public WeaponInventory(PlayerEntity player, PassiveInventory passives, EntityLifecycle lifecycle)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _lifecycle.Despawned += OnDespawned;
        }

        /// Slot contents in slot order; null for an empty slot.
        public IReadOnlyList<WeaponInstance> Slots => _slots;

        /// Number of occupied slots.
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// Ids of original weapons replaced by evolution this run, in evolution order (D5: never offered as "new" again).
        public IReadOnlyList<string> ConsumedByEvolution => _consumedByEvolution;

        /// True when a slot holds weaponId.
        public bool Owns(string weaponId)
        {
            return IndexOf(weaponId) >= 0;
        }

        /// Slot index holding weaponId, or -1.
        public int IndexOf(string weaponId)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && string.Equals(_slots[i].Id, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// True with the instance when index is a valid occupied slot.
        public bool TryGetSlot(int index, out WeaponInstance instance)
        {
            instance = index >= 0 && index < _slots.Length ? _slots[index] : null;
            return instance != null;
        }

        /// WeaponTick: every occupied slot in slot order (deterministic command Sequence, 5.4); other phases do nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.WeaponTick)
            {
                return;
            }

            var args = new WeaponTickArgs(_player.Body.Id, _passives.BonusOf(PassiveStat.AttackSpeed), _lifecycle.Collisions);
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i]?.Tick(context, args);
            }
        }

        /// Run reset: empties every slot and the evolution record (children are gone after DespawnAll).
        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            _consumedByEvolution.Clear();
        }

        internal int FirstFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        internal void SetSlot(int index, WeaponInstance instance)
        {
            _slots[index] = instance;
        }

        internal void AddConsumedByEvolution(string weaponId)
        {
            if (!_consumedByEvolution.Contains(weaponId))
            {
                _consumedByEvolution.Add(weaponId);
            }
        }

        internal void RefreshAttackSpeed(float attackSpeedBonus)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i]?.RefreshAttackSpeed(attackSpeedBonus);
            }
        }

        // True when instance is still the weapon in its slot (events of orbs/zones whose weapon was replaced are ignored).
        internal bool IsCurrent(WeaponInstance instance)
        {
            return instance != null && instance.SlotIndex >= 0 && instance.SlotIndex < _slots.Length
                && ReferenceEquals(_slots[instance.SlotIndex], instance);
        }

        private void OnDespawned(SimEntity entity, DespawnReason reason)
        {
            EntityHandle id = entity.Id;
            if (entity is OrbitOrb orb)
            {
                ReleaseChild(orb.Owner, orb, orb.Overlaps);
            }
            else if (entity is DamageZone zone)
            {
                ReleaseChild(zone.Owner, zone, zone.Overlaps);
            }

            // A despawned target: drop its cooldown records and its place in every live child's overlap list.
            for (int i = 0; i < _slots.Length; i++)
            {
                WeaponInstance weapon = _slots[i];
                if (weapon == null)
                {
                    continue;
                }

                weapon.Tracker?.Forget(id);
                IReadOnlyList<SimEntity> children = weapon.Children;
                for (int c = 0; c < children.Count; c++)
                {
                    if (children[c] is OrbitOrb childOrb)
                    {
                        childOrb.Overlaps.Remove(id);
                    }
                    else if (children[c] is DamageZone childZone)
                    {
                        childZone.Overlaps.Remove(id);
                    }
                }
            }
        }

        // Unregistering an orb/zone emits no Exit: its live overlaps are replayed as Exits so the tracker's overlap counts stay true.
        private static void ReleaseChild(WeaponInstance owner, SimEntity child, List<EntityHandle> overlaps)
        {
            if (owner == null)
            {
                return;
            }

            owner.RemoveChild(child);
            for (int i = 0; i < overlaps.Count; i++)
            {
                owner.Tracker?.OnExit(overlaps[i]);
            }

            overlaps.Clear();
        }
    }
}
