using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Weapon-side collision events (5.9), registered by the composition root on channels 0, 1 and 2.
    /// Channel 0 Enter: one-shot projectile hit - damage, and on hit number PierceCount + 1 MarkConsumed + Despawn(Consumed)
    /// (TAC-F6-9); events of a consumed projectile are ignored. A target already dead at Enter is recorded only - no damage,
    /// no pierce use, the projectile flies on (TAC-F6-11). Channels 1/2 Enter/Exit: the owning weapon instance's tracker
    /// OnEnter/OnExit (TAC-F6-6); orbs/zones whose weapon was replaced are ignored.
    public sealed class PlayerHitHandler : ICollisionEventHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly WeaponInventory _weapons;
        private readonly Func<EntityHandle, IDamageable> _damageables;

        /// Creates the handler; damageables maps a monster id to its IDamageable (null = ignore the event). Null arguments throw.
        public PlayerHitHandler(EntityLifecycle lifecycle, WeaponInventory weapons, Func<EntityHandle, IDamageable> damageables)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _damageables = damageables ?? throw new ArgumentNullException(nameof(damageables));
        }

        /// Handles one event of channel 0, 1 or 2 (A = projectile/orb/zone, B = monster); other channels are ignored.
        public void Handle(in CollisionEvent e, in StepContext context)
        {
            switch (e.Channel)
            {
                case CollisionChannel.PlayerProjectileVsMonster:
                    HandleProjectile(e, context);
                    break;
                case CollisionChannel.PlayerOrbVsMonster:
                case CollisionChannel.PlayerZoneVsMonster:
                    HandlePersistent(e, context);
                    break;
            }
        }

        private void HandleProjectile(in CollisionEvent e, in StepContext context)
        {
            if (e.Kind != CollisionEventKind.Enter || !_lifecycle.TryGet(e.A, out SimEntity entity)
                || !(entity is PlayerProjectile projectile) || projectile.IsConsumed)
            {
                return;
            }

            IDamageable target = _damageables(e.B);
            if (target == null || !projectile.TryRecordHit(e.B))
            {
                return;
            }

            if (!target.IsAlive)
            {
                return;
            }

            target.ApplyDamage(projectile.Damage, context.Step);
            projectile.CountDamagingHit();
            if (projectile.IsSpent)
            {
                projectile.MarkConsumed();
                context.Commands.Enqueue(new SimulationCommand(
                    SimulationCommandKind.Despawn, projectile.Id, target: projectile.Id, reason: DespawnReason.Consumed));
            }
        }

        private void HandlePersistent(in CollisionEvent e, in StepContext context)
        {
            if (!_lifecycle.TryGet(e.A, out SimEntity entity) || entity.IsConsumed)
            {
                return;
            }

            WeaponInstance owner;
            List<EntityHandle> overlaps;
            if (entity is OrbitOrb orb)
            {
                owner = orb.Owner;
                overlaps = orb.Overlaps;
            }
            else if (entity is DamageZone zone)
            {
                owner = zone.Owner;
                overlaps = zone.Overlaps;
            }
            else
            {
                return;
            }

            if (!_weapons.IsCurrent(owner) || owner.Tracker == null)
            {
                return;
            }

            if (e.Kind == CollisionEventKind.Enter)
            {
                if (!overlaps.Contains(e.B))
                {
                    overlaps.Add(e.B);
                    owner.Tracker.OnEnter(e.B, context.Step);
                }
            }
            else if (overlaps.Remove(e.B))
            {
                owner.Tracker.OnExit(e.B);
            }
        }
    }
}
