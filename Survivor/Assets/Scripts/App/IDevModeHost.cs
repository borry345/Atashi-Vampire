using System;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Strategies;

namespace Survivor.App
{
    /// Everything dev mode may touch (5.15); implemented by GameBootstrap. App never references Survivor.Dev.
    public interface IDevModeHost
    {
        /// Runtime rules shared by the whole simulation.
        SimulationRules Rules { get; }

        /// Step clock and run gate.
        SimulationClock Clock { get; }

        /// Between-steps command queue (applied in the next Begin).
        PendingCommandQueue Commands { get; }

        /// Current game data.
        GameDataRegistry Data { get; }

        /// Run reset and the read-only view of the run.
        RunSession Session { get; }

        /// Monster spatial index (visualization, read only).
        MonsterSpatialIndex SpatialIndex { get; }

        /// Pool/tick/collision/target selection applied at the next run start.
        StrategySelection NextRunSelection { get; set; }

        /// Wraps the next run's collision strategy (null = none).
        Func<ICollisionStrategy, ICollisionStrategy> NextRunCollisionDecorator { get; set; }

        /// Input of the next run (null = keyboard); bound once at run start, never null-checked per step.
        IInputSource InputOverride { get; set; }

        /// Spawn policy of the next run (null = spawn table); bound once at run start, never null-checked per step.
        ISpawnPolicy SpawnPolicyOverride { get; set; }

        /// Master seed of the next run (null = a fresh seed per run).
        ulong? FixedSeed { get; set; }

        /// MonsterSpawner.BossSpawnedThisRun as is.
        bool BossSpawnedThisRun { get; }

        /// Builds the provider for kind and queues a SwitchTargetProvider command with the shared requirements callback (TAC-F16-5).
        void RequestTargetProviderSwitch(TargetProviderKind kind);

        /// Resets the run with the next selection, applies the requirements and opens the gate (from Title, Result or Paused).
        void StartNewRun();

        /// Raised when a run ends by death or clear (after the best record was handled).
        event Action<RunOutcome> RunEnded;
    }
}
