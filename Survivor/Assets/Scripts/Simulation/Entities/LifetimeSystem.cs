using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Lifecycle order 10: despawns every entity whose ExpireStep has been reached, in ascending Id order (5.4).
    /// Entities without a lifetime (ExpireStep = long.MaxValue) are skipped.
    public sealed class LifetimeSystem : IStepPhaseHandler
    {
        private static readonly Comparison<SimEntity> ById = (a, b) => a.Id.CompareTo(b.Id);

        private readonly EntityLifecycle _lifecycle;
        private readonly List<SimEntity> _expired = new List<SimEntity>();

        /// Creates the system over lifecycle; null throws ArgumentNullException.
        public LifetimeSystem(EntityLifecycle lifecycle)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        /// Lifecycle: Despawn(LifetimeExpired) for every entity with context.Step >= ExpireStep; other phases do nothing.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Lifecycle)
            {
                return;
            }

            IReadOnlyList<SimEntity> active = _lifecycle.ActiveEntities;
            for (int i = 0; i < active.Count; i++)
            {
                SimEntity entity = active[i];
                if (entity.ExpireStep != long.MaxValue && context.Step >= entity.ExpireStep)
                {
                    _expired.Add(entity);
                }
            }

            if (_expired.Count == 0)
            {
                return;
            }

            _expired.Sort(ById);
            try
            {
                for (int i = 0; i < _expired.Count; i++)
                {
                    _lifecycle.Despawn(_expired[i].Id, DespawnReason.LifetimeExpired);
                }
            }
            finally
            {
                _expired.Clear();
            }
        }
    }
}
