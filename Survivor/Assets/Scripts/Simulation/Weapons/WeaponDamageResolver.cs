using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Resolve order 10: persistent weapon damage (orbs, zones) in weapon slot order, each tracker's hits in ascending target id
    /// (5.12, R10). Damage = the owning instance's current level Damage. While !Rules.WeaponsEnabled it does not call CollectHits
    /// (damage stops; Enter/Exit bookkeeping continues, TAC-F2-5).
    public sealed class WeaponDamageResolver : IStepPhaseHandler
    {
        private readonly WeaponInventory _weapons;
        private readonly Func<EntityHandle, IDamageable> _damageables;
        private readonly List<EntityHandle> _hits = new List<EntityHandle>();

        /// Creates the resolver over weapons; damageables maps a target id to its IDamageable (null = ignore). Null arguments throw.
        public WeaponDamageResolver(WeaponInventory weapons, Func<EntityHandle, IDamageable> damageables)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _damageables = damageables ?? throw new ArgumentNullException(nameof(damageables));
        }

        /// Resolve: collects and applies tracker hits; other phases do nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Resolve || !context.Rules.WeaponsEnabled)
            {
                return;
            }

            IReadOnlyList<WeaponInstance> slots = _weapons.Slots;
            for (int s = 0; s < slots.Count; s++)
            {
                WeaponInstance weapon = slots[s];
                if (weapon?.Tracker == null)
                {
                    continue;
                }

                _hits.Clear();
                weapon.Tracker.CollectHits(context.Step, _hits);
                float damage = weapon.LevelDefinition.Damage;
                for (int i = 0; i < _hits.Count; i++)
                {
                    _damageables(_hits[i])?.ApplyDamage(damage, context.Step);
                }
            }

            _hits.Clear();
        }
    }
}
