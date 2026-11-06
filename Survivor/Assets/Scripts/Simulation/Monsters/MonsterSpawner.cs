using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Lifecycle order 30: normal spawns of the bound ISpawnPolicy and the once-per-run boss. The cap (non-boss count, R20) and
    /// placement on the spawn circle (R7, Spawn stream) are applied here, not by the policy. Calls EntityLifecycle directly.
    public sealed class MonsterSpawner : IStepPhaseHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly GameDataRegistry _data;
        private readonly RngStreams _rng;
        private readonly List<string> _requests = new List<string>();
        private ISpawnPolicy _policy;
        private bool _bossWarningLogged;

        /// Creates the spawner and subscribes to lifecycle to count live non-boss monsters; null throws ArgumentNullException.
        public MonsterSpawner(EntityLifecycle lifecycle, GameDataRegistry data, RngStreams rng)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _lifecycle.Spawned += OnSpawned;
            _lifecycle.Despawning += OnDespawning;
        }

        /// True once a boss was spawned in this run (by this spawner or a dev SpawnBossNow / SpawnMonstersNow command).
        public bool BossSpawnedThisRun { get; private set; }

        /// Live Monster-category entities that are not the boss (the R20 cap counts these).
        public int LiveNonBossCount { get; private set; }

        /// Policy bound for the current run.
        public ISpawnPolicy Policy => _policy;

        /// Run start (RunSession.Reset): binds the run's policy (table or dev override) and clears the per-run boss flag and
        /// boss warning. Throws ArgumentNullException for null.
        public void Bind(ISpawnPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            BossSpawnedThisRun = false;
            _bossWarningLogged = false;
        }

        /// Lifecycle: normal spawns in request order while live < cap (the rest of the step is dropped without drawing angles),
        /// then the boss when the policy says it is due and no boss spawned yet this run.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Lifecycle)
            {
                return;
            }

            GameData data = _data.Current;
            if (data == null)
            {
                return;
            }

            DeterministicRng spawnRng = _rng.Get(RngStream.Spawn);
            _requests.Clear();
            _policy.CollectSpawns(context, LiveNonBossCount, spawnRng, _requests);
            for (int i = 0; i < _requests.Count; i++)
            {
                if (LiveNonBossCount >= context.Rules.MonsterCap)
                {
                    break;
                }

                // A dev policy may name a monster the current data does not have; such a request is skipped without a draw.
                if (!data.TryGetMonster(_requests[i], out MonsterDefinition definition))
                {
                    continue;
                }

                SpawnOnCircle(definition, context, spawnRng);
            }

            _requests.Clear();
            if (!BossSpawnedThisRun && _policy.BossSpawnDue(context.Step))
            {
                SpawnBoss(data, context, spawnRng);
            }
        }

        // Shared with MonsterCommandHandler (dev SpawnBossNow / SpawnMonstersNow with the boss id): one boss per run.
        internal void MarkBossSpawned()
        {
            BossSpawnedThisRun = true;
        }

        private void SpawnBoss(GameData data, in StepContext context, DeterministicRng spawnRng)
        {
            string bossId = data.SpawnTable.BossMonsterId;
            if (bossId == null || !data.TryGetMonster(bossId, out MonsterDefinition boss))
            {
                if (!_bossWarningLogged)
                {
                    _bossWarningLogged = true;
                    LoopLog.Warning("Boss not spawned: the spawn table has no valid boss definition, so this run cannot be cleared.");
                }

                return;
            }

            Monster spawned = SpawnOnCircle(boss, context, spawnRng);
            LoopLog.Info("Boss spawned: " + boss.Id + " id=" + spawned.Id + " step=" + context.Step.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private Monster SpawnOnCircle(MonsterDefinition definition, in StepContext context, DeterministicRng spawnRng)
        {
            bool isBoss = definition.Behavior == MonsterBehavior.Boss;
            var position = SpawnGeometry.PointOnCircle(context.PlayerPosition, GameRules.SpawnRadius, spawnRng.NextFloat01());
            Monster monster = _lifecycle.Spawn<Monster>(EntityCategory.Monster, position, 0, m => m.Initialize(definition, isBoss));
            if (isBoss)
            {
                BossSpawnedThisRun = true;
            }

            return monster;
        }

        private void OnSpawned(SimEntity entity)
        {
            if (CountsTowardCap(entity))
            {
                LiveNonBossCount++;
            }
        }

        private void OnDespawning(SimEntity entity)
        {
            if (CountsTowardCap(entity))
            {
                LiveNonBossCount--;
            }
        }

        private static bool CountsTowardCap(SimEntity entity)
        {
            return entity.Category == EntityCategory.Monster && !(entity is Monster monster && monster.IsBoss);
        }
    }
}
