using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Second driver: closes Tick.Monsters, BuildIndex (+ TargetProviderSlot.OnSnapshotBuilt), WeaponTick, (central) ProjectileTick;
    /// opens Tick.Projectiles (5.3). Does nothing when no step is active.
    [DefaultExecutionOrder(0)]
    public sealed class SimulationDriverMid : MonoBehaviour
    {
        private SimulationClock _clock;
        private SimulationPipeline _pipeline;
        private Func<ITickStrategy> _currentTicks;
        private MonsterSnapshotBuffer _snapshot;
        private TargetProviderSlot _targets;

        /// Binds the loop objects; snapshot is the buffer MonsterRegistry fills. Null arguments throw ArgumentNullException.
        public void Initialize(SimulationClock clock, SimulationPipeline pipeline, Func<ITickStrategy> currentTicks,
                               MonsterSnapshotBuffer snapshot, TargetProviderSlot targets)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _currentTicks = currentTicks ?? throw new ArgumentNullException(nameof(currentTicks));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        private void FixedUpdate()
        {
            if (_clock == null || !_clock.StepActive)
            {
                return;
            }

            StepContext context = _clock.CurrentContext;
            SimulationPhase phase = SimulationPhase.BuildIndex;
            try
            {
                _clock.Markers.EndMonsters();

                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, context);
                // After every BuildIndex handler (registry 0, physics sync 5, spatial index 10), 5c.3 #9.
                _targets.OnSnapshotBuilt(_snapshot);

                phase = SimulationPhase.WeaponTick;
                _clock.SetPhase(phase);
                _pipeline.RunPhase(phase, context);

                phase = SimulationPhase.ProjectileTick;
                _clock.SetPhase(phase);
                _clock.Markers.BeginProjectiles();
                _currentTicks().RunGroup(TickGroup.Projectiles, context);
                _pipeline.RunPhase(phase, context);
            }
            catch (Exception exception)
            {
                LoopLog.PhaseError(phase, context.Step, exception);
                _clock.AbortStep();
            }
        }
    }
}
