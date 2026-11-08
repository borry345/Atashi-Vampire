using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Victim-side shared invulnerability for the player (5.12, TAC-F3-12), registered by the composition root on channels 3, 4, 5
    /// and as the Resolve order 20 handler. Channels 3 (body vs monster, source = B) and 5 (enemy zone vs body, source = A) keep a
    /// sorted overlap set; channel 4 Enter (enemy projectile, source = A) is a one-shot candidate and the projectile is always
    /// consumed (MarkConsumed + Despawn(Consumed)), even while invulnerable. Resolve: if Rules.PlayerTakesDamage and
    /// step >= InvulnerableUntilStep, the largest candidate damage (tie: smaller id) is applied once and
    /// InvulnerableUntilStep = step + InvulnerabilitySteps. Despawned ids leave the overlap set (no Exit on unregister).
    public sealed class PlayerDamageReceiver : ICollisionEventHandler, IStepPhaseHandler
    {
        private readonly PlayerHealth _health;
        private readonly Func<GameData> _currentData;
        private readonly Func<EntityHandle, IContactDamageSource> _contactSources;
        private readonly EntityLifecycle _lifecycle;
        private readonly List<EntityHandle> _overlaps = new List<EntityHandle>();
        private readonly List<EntityHandle> _oneShots = new List<EntityHandle>();

        /// Creates the receiver; contactSources maps an id to its IContactDamageSource (null = skip that candidate); subscribes to
        /// lifecycle.Despawned. Null arguments throw ArgumentNullException.
        public PlayerDamageReceiver(PlayerHealth health, Func<GameData> currentData,
                                    Func<EntityHandle, IContactDamageSource> contactSources, EntityLifecycle lifecycle)
        {
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
            _contactSources = contactSources ?? throw new ArgumentNullException(nameof(contactSources));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _lifecycle.Despawned += OnDespawned;
        }

        /// First step at which the player can be damaged again.
        public long InvulnerableUntilStep { get; private set; }

        /// Contact sources currently overlapping the player (channels 3 and 5), ascending id.
        public int OverlapCount => _overlaps.Count;

        /// Handles one event of channel 3, 4 or 5; other channels are ignored.
        public void Handle(in CollisionEvent e, in StepContext context)
        {
            switch (e.Channel)
            {
                case CollisionChannel.PlayerBodyVsMonster:
                    TrackOverlap(e.Kind, e.B);
                    break;
                case CollisionChannel.EnemyZoneVsPlayerBody:
                    TrackOverlap(e.Kind, e.A);
                    break;
                case CollisionChannel.EnemyProjectileVsPlayerBody:
                    if (e.Kind == CollisionEventKind.Enter)
                    {
                        ConsumeProjectile(e.A, context);
                    }

                    break;
            }
        }

        /// Resolve: applies at most one hit this step; one-shot candidates are cleared every step.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Resolve)
            {
                return;
            }

            try
            {
                if (!context.Rules.PlayerTakesDamage || context.Step < InvulnerableUntilStep)
                {
                    return;
                }

                bool found = false;
                float best = 0f;
                EntityHandle bestId = EntityHandle.None;
                Consider(_overlaps, ref found, ref best, ref bestId);
                Consider(_oneShots, ref found, ref best, ref bestId);
                if (!found)
                {
                    return;
                }

                _health.TakeDamage(best);
                InvulnerableUntilStep = context.Step + InvulnerabilitySteps();
            }
            finally
            {
                _oneShots.Clear();
            }
        }

        /// Run reset: forgets overlaps, pending candidates and invulnerability.
        public void Clear()
        {
            _overlaps.Clear();
            _oneShots.Clear();
            InvulnerableUntilStep = 0;
        }

        private void Consider(List<EntityHandle> ids, ref bool found, ref float best, ref EntityHandle bestId)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                IContactDamageSource source = _contactSources(ids[i]);
                if (source == null)
                {
                    continue;
                }

                float damage = source.ContactDamage;
                if (!found || damage > best || (damage == best && ids[i] < bestId))
                {
                    found = true;
                    best = damage;
                    bestId = ids[i];
                }
            }
        }

        private void TrackOverlap(CollisionEventKind kind, EntityHandle source)
        {
            int index = _overlaps.BinarySearch(source);
            if (kind == CollisionEventKind.Enter)
            {
                if (index < 0)
                {
                    _overlaps.Insert(~index, source);
                }
            }
            else if (index >= 0)
            {
                _overlaps.RemoveAt(index);
            }
        }

        private void ConsumeProjectile(EntityHandle projectileId, in StepContext context)
        {
            if (_lifecycle.TryGet(projectileId, out SimEntity projectile))
            {
                if (projectile.IsConsumed)
                {
                    return;
                }

                projectile.MarkConsumed();
            }

            if (!_oneShots.Contains(projectileId))
            {
                _oneShots.Add(projectileId);
            }

            context.Commands.Enqueue(new SimulationCommand(
                SimulationCommandKind.Despawn, projectileId, target: projectileId, reason: DespawnReason.Consumed));
        }

        private int InvulnerabilitySteps()
        {
            PlayerStatsDefinition stats = _currentData()?.PlayerStats;
            return stats == null ? 0 : Math.Max(0, stats.InvulnerabilitySteps);
        }

        private void OnDespawned(SimEntity entity, DespawnReason reason)
        {
            int index = _overlaps.BinarySearch(entity.Id);
            if (index >= 0)
            {
                _overlaps.RemoveAt(index);
            }

            _oneShots.Remove(entity.Id);
        }
    }
}
