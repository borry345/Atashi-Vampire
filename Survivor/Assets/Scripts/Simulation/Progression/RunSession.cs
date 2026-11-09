using System;
using System.Globalization;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// The single run-boundary reset (TAC-F8-1, TAC-F8-2, 5.14 rev 17 order 0..9) and the read-only view of the run's objects that
    /// the flow and SimulationStateHash use. Touches no Strategies type (5c.1): the composition root builds the next run's
    /// strategies, binds the dev overrides into the policy/input suppliers, and after Reset clears the spatial index, runs the
    /// requirements callback and opens the clock.
    public sealed class RunSession
    {
        private readonly SimulationClock _clock;
        private readonly EntityLifecycle _lifecycle;
        private readonly CollisionEventRouter _router;
        private readonly TargetProviderSlot _targetSlot;
        private readonly EntityHandleAllocator _ids;
        private readonly RngStreams _rng;
        private readonly SimulationCommandBuffer _commandBuffer;
        private readonly PendingCommandQueue _pendingQueue;
        private readonly PlayerEntity _player;
        private readonly PlayerMovement _movement;
        private readonly PlayerHealth _health;
        private readonly WeaponInventory _weapons;
        private readonly PassiveInventory _passives;
        private readonly PlayerDamageReceiver _damageReceiver;
        private readonly PlayerProgressionApplier _progressionApplier;
        private readonly PickupSystem _pickupSystem;
        private readonly KillPipeline _killPipeline;
        private readonly MonsterSpawner _spawner;
        private readonly ExperienceSystem _experience;
        private readonly LevelUpSession _levelUpSession;
        private readonly StepOutcomeGate _outcomeGate;
        private readonly Func<ulong> _nextSeed;
        private readonly Func<ISpawnPolicy> _spawnPolicy;
        private readonly Func<IInputSource> _inputSource;

        /// Creates the session over every object the reset touches or the state hash reads. nextSeed returns the master seed of
        /// each run (App: a fresh seed, or the dev seed); spawnPolicy and inputSource return the run's policy and input
        /// (App: devHost.SpawnPolicyOverride ?? new TableSpawnPolicy(data), devHost.InputOverride ?? keyboard).
        /// Null arguments throw ArgumentNullException.
        public RunSession(SimulationClock clock, EntityLifecycle lifecycle, CollisionEventRouter router, TargetProviderSlot targetSlot,
                          EntityHandleAllocator ids, RngStreams rng, SimulationCommandBuffer commandBuffer,
                          PendingCommandQueue pendingQueue, PlayerEntity player, PlayerMovement movement, PlayerHealth health,
                          WeaponInventory weapons, PassiveInventory passives, PlayerDamageReceiver damageReceiver,
                          PlayerProgressionApplier progressionApplier, PickupSystem pickupSystem, KillPipeline killPipeline,
                          MonsterSpawner spawner, ExperienceSystem experience, LevelUpSession levelUpSession,
                          StepOutcomeGate outcomeGate, Func<ulong> nextSeed, Func<ISpawnPolicy> spawnPolicy,
                          Func<IInputSource> inputSource)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _targetSlot = targetSlot ?? throw new ArgumentNullException(nameof(targetSlot));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _commandBuffer = commandBuffer ?? throw new ArgumentNullException(nameof(commandBuffer));
            _pendingQueue = pendingQueue ?? throw new ArgumentNullException(nameof(pendingQueue));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            _damageReceiver = damageReceiver ?? throw new ArgumentNullException(nameof(damageReceiver));
            _progressionApplier = progressionApplier ?? throw new ArgumentNullException(nameof(progressionApplier));
            _pickupSystem = pickupSystem ?? throw new ArgumentNullException(nameof(pickupSystem));
            _killPipeline = killPipeline ?? throw new ArgumentNullException(nameof(killPipeline));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _experience = experience ?? throw new ArgumentNullException(nameof(experience));
            _levelUpSession = levelUpSession ?? throw new ArgumentNullException(nameof(levelUpSession));
            _outcomeGate = outcomeGate ?? throw new ArgumentNullException(nameof(outcomeGate));
            _nextSeed = nextSeed ?? throw new ArgumentNullException(nameof(nextSeed));
            _spawnPolicy = spawnPolicy ?? throw new ArgumentNullException(nameof(spawnPolicy));
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
        }

        /// Step clock and gate.
        public SimulationClock Clock => _clock;

        /// Entity lifecycle (live entities, current strategies).
        public EntityLifecycle Lifecycle => _lifecycle;

        /// The player.
        public PlayerEntity Player => _player;

        /// Player health.
        public PlayerHealth Health => _health;

        /// Player weapon slots.
        public WeaponInventory Weapons => _weapons;

        /// Player passive slots.
        public PassiveInventory Passives => _passives;

        /// Player level and experience.
        public ExperienceSystem Experience => _experience;

        /// Kill count (R24) and the boss-killed flag.
        public KillPipeline KillPipeline => _killPipeline;

        /// Level-up round state (UI).
        public LevelUpSession LevelUpSession => _levelUpSession;

        /// Death > clear > level-up gate and its pending outcome.
        public StepOutcomeGate OutcomeGate => _outcomeGate;

        /// Random streams of the run (reseeded by every Reset).
        public RngStreams Rng => _rng;

        /// Run reset in the 5.14 (rev 17) order: 0 player.UnregisterFromRun; 1 DespawnAll(RunReset); 2 current pool ReleaseAll,
        /// collision Clear, tick Clear; 3 ReplaceStrategies / router.ReplaceStrategy / targetSlot.Replace with next;
        /// 4 receiver, pickups, kill pipeline, level-up session, experience, outcome gate, command buffer, pending queue cleared;
        /// 5 allocator Reset, then player.RegisterForRun(next.Collision, origin) (ids 1:1 and 2:1); 6 Reseed(nextSeed());
        /// 7 spawner.Bind(spawnPolicy()), movement.SetInputSource(inputSource()); 8 clock.Reset(); 9 progressionApplier.ResetForRun().
        /// The gate stays closed (RunReset). Throws InvalidOperationException while a step is active and ArgumentException when
        /// next has a null member (both before anything changes).
        public void Reset(in RunStrategies next)
        {
            if (_clock.StepActive)
            {
                throw new InvalidOperationException("RunSession.Reset is a run-boundary operation and cannot run inside a step.");
            }

            if (next.Pool == null || next.Tick == null || next.Collision == null || next.Target == null)
            {
                throw new ArgumentException("RunSession.Reset needs all four strategies (pool " + (next.Pool != null) + ", tick "
                    + (next.Tick != null) + ", collision " + (next.Collision != null) + ", target " + (next.Target != null) + ").",
                    nameof(next));
            }

            // 0. The player is not a lifecycle entity: detach its bodies from the outgoing strategy first.
            _player.UnregisterFromRun();

            // 1-2. Despawn with the strategies the entities were registered to, then empty those strategies.
            _lifecycle.DespawnAll(DespawnReason.RunReset);
            _lifecycle.Pool.ReleaseAll();
            _lifecycle.Collisions.Clear();
            _lifecycle.Ticks.Clear();

            // 3. Strategies of the next run.
            _lifecycle.ReplaceStrategies(next.Pool, next.Tick, next.Collision);
            _router.ReplaceStrategy(next.Collision);
            _targetSlot.Replace(next.Target);

            // 4. Per-run records.
            _damageReceiver.Clear();
            _pickupSystem.Clear();
            _killPipeline.Reset();
            _levelUpSession.Reset();
            _experience.Reset();
            _outcomeGate.Reset();
            _commandBuffer.Clear();
            _pendingQueue.Clear();

            // 5. Ids restart; the player takes 1:1 (body) and 2:1 (magnet) again.
            _ids.Reset();
            _player.RegisterForRun(next.Collision, Vector2.zero);

            // 6-7. Seed and run bindings.
            ulong seed = _nextSeed();
            _rng.Reseed(seed);
            _spawner.Bind(_spawnPolicy());
            _movement.SetInputSource(_inputSource());

            // 8-9. Step 0 with the gate closed, then the starting loadout (its first shot is at clock.Step = 0, TAC-F2-1/F2-2).
            _clock.Reset();
            _progressionApplier.ResetForRun();

            LoopLog.Info("Run reset: seed " + seed.ToString(CultureInfo.InvariantCulture) + ", pool=" + next.Pool.Kind + ", tick="
                + next.Tick.Kind + ", collision=" + next.Collision.Kind + ", target=" + next.Target.Kind + ".");
        }
    }
}
