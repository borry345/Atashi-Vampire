using System.Collections.Generic;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Dev
{
    /// Plain values copied from IDevModeHost once per OnGUI while the overlay is open (5.15.4); DevStatusFormatter turns them
    /// into the status lines. Reading these never writes simulation state.
    public readonly struct DevStatusInput
    {
        /// Clock.Step.
        public readonly long Step;

        /// Session.Rng.MasterSeed (the seed actually used by the run).
        public readonly ulong Seed;

        /// True when the run used IDevModeHost.FixedSeed (otherwise a fresh seed per run).
        public readonly bool SeedFixed;

        /// LiveMonsterCount (boss included).
        public readonly int LiveMonsters;

        /// ActiveGemCount.
        public readonly int ActiveGems;

        /// Pool strategy of the run.
        public readonly PoolStrategyKind Pool;

        /// Target provider (the kind dev last applied; may lead the real switch by one step).
        public readonly TargetProviderKind Target;

        /// Tick strategy of the run.
        public readonly TickStrategyKind Tick;

        /// Collision strategy of the run.
        public readonly CollisionStrategyKind Collision;

        /// Engine collision detection mode (CCD) the run started with.
        public readonly CollisionDetectionMode2D Ccd;

        /// Spatial hash cell size in world units.
        public readonly float CellSize;

        /// Time.timeScale set by DevController.
        public readonly float TimeScale;

        /// Steps run over the last unscaled second / 60 (5.15.1).
        public readonly float ActualSpeed;

        /// Monster HP multiplier of the current spawn segment (R31); null shows "HP mult: n/a" (wave 5a).
        public readonly float? HpMultiplier;

        /// Screen flow state.
        public readonly FlowState State;

        /// Clock.IsRunning.
        public readonly bool GateRunning;

        /// Clock.CloseReason (meaningful when the gate is closed).
        public readonly GateCloseReason CloseReason;

        /// Session.Weapons.Slots (null slots are skipped); null = none.
        public readonly IReadOnlyList<WeaponInstance> Weapons;

        /// Session.Passives.Slots (null slots are skipped); null = none.
        public readonly IReadOnlyList<PassiveInstance> Passives;

        /// Creates the input from already-read values.
        public DevStatusInput(long step, ulong seed, bool seedFixed, int liveMonsters, int activeGems,
                              PoolStrategyKind pool, TargetProviderKind target, TickStrategyKind tick, CollisionStrategyKind collision,
                              CollisionDetectionMode2D ccd, float cellSize, float timeScale, float actualSpeed, float? hpMultiplier,
                              FlowState state, bool gateRunning, GateCloseReason closeReason,
                              IReadOnlyList<WeaponInstance> weapons, IReadOnlyList<PassiveInstance> passives)
        {
            Step = step;
            Seed = seed;
            SeedFixed = seedFixed;
            LiveMonsters = liveMonsters;
            ActiveGems = activeGems;
            Pool = pool;
            Target = target;
            Tick = tick;
            Collision = collision;
            Ccd = ccd;
            CellSize = cellSize;
            TimeScale = timeScale;
            ActualSpeed = actualSpeed;
            HpMultiplier = hpMultiplier;
            State = state;
            GateRunning = gateRunning;
            CloseReason = closeReason;
            Weapons = weapons;
            Passives = passives;
        }
    }
}
