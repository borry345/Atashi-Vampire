using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Player projectile (W1/E1 straight, W4 homing). Moves in ProjectileTick; damage and pierce come from the level definition
    /// captured at spawn. One-shot contact on channel 0 (TAC-F6-9): the ids already hit (generation included) block re-hits.
    public sealed class PlayerProjectile : SimEntity, IUpdatable
    {
        private readonly List<EntityHandle> _hitTargets = new List<EntityHandle>();
        private int _damagingHits;

        /// Tick group of player and enemy projectiles.
        public TickGroup Group => TickGroup.Projectiles;

        /// Weapon id that fired the projectile.
        public string WeaponId { get; private set; }

        /// Weapon slot that fired the projectile.
        public int SlotIndex { get; private set; }

        /// Damage per hit (level definition at spawn).
        public float Damage { get; private set; }

        /// Unit travel direction; homing projectiles re-aim it every tick.
        public Vector2 Direction { get; private set; }

        /// Distance travelled per step.
        public float SpeedPerStep { get; private set; }

        /// True for W4: re-targets the nearest monster every tick, keeps the last direction when none exists (TAC-F6-3).
        public bool IsHoming { get; private set; }

        /// Extra monsters the projectile may hit; consumed on hit number PierceCount + 1.
        public int PierceCount { get; private set; }

        /// Number of live monsters damaged so far; Enters on already-dead targets are recorded but not counted (TAC-F6-11).
        public int HitCount => _damagingHits;

        /// Target of the latest successful homing re-aim (dev visualization); default = none. Non-homing projectiles never write it.
        public EntityHandle LastTargetId { get; internal set; }

        /// Moves one step: homing re-aims at the nearest monster first (TAC-F6-3), then Position += Direction * SpeedPerStep.
        public void Tick(in StepContext context)
        {
            // Entities spawned in Lifecycle of step s first tick in s + 1 (5.4).
            Debug.Assert(context.Step > SpawnStep, "PlayerProjectile ticked in its spawn step.");
            if (IsHoming && context.Targets.TryFindNearest(Position, out TargetHit hit))
            {
                LastTargetId = hit.Id;
                Vector2 toTarget = hit.Position - Position;
                if (toTarget.x != 0f || toTarget.y != 0f)
                {
                    Direction = ProjectileWeapon.ToUnit(toTarget);
                }
            }

            Position += Direction * SpeedPerStep;
        }

        internal void Initialize(string weaponId, int slotIndex, float damage, Vector2 direction, float speedPerStep, float radius,
                                 bool homing, int pierceCount)
        {
            WeaponId = weaponId;
            SlotIndex = slotIndex;
            Damage = damage;
            Direction = direction;
            SpeedPerStep = speedPerStep;
            IsHoming = homing;
            LastTargetId = default;
            PierceCount = pierceCount < 0 ? 0 : pierceCount;
            _hitTargets.Clear();
            _damagingHits = 0;
            Body.Radius = radius;
            Body.IsLarge = false;
        }

        // False when target was already hit by this projectile; records it otherwise.
        internal bool TryRecordHit(EntityHandle target)
        {
            if (_hitTargets.Contains(target))
            {
                return false;
            }

            _hitTargets.Add(target);
            return true;
        }

        // Counts one hit that damaged a live target; only these use up the pierce budget (TAC-F6-11).
        internal void CountDamagingHit()
        {
            _damagingHits++;
        }

        internal bool IsSpent => _damagingHits >= PierceCount + 1;

        /// Drops per-flight state before the instance returns to the pool.
        protected override void OnReleased()
        {
            _hitTargets.Clear();
            _damagingHits = 0;
            WeaponId = null;
        }
    }
}
