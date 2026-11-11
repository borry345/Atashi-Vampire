using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Lifecycle order 0 handler of SpawnEnemyProjectile, SpawnEnemyZone and SpawnMonsters (5.4).
    /// SpawnEnemyProjectile: DefinitionId = firing monster id, Source = firing monster.
    /// SpawnEnemyZone: DefinitionId = boss monster id, SlotIndex = index into its BossPatterns (a HazardZone pattern), Source = boss.
    /// SpawnMonsters: per monster live < cap unless IgnoreCap, Radius > 0 places it on the circle (Summon stream), else at Position.
    /// The source monster's own definition is used when it is still live (5c.3 #10), otherwise the current data.
    /// A command whose definition cannot be found is dropped and counted (dev input may be wrong).
    public sealed class MonsterCommandHandler : ISimulationCommandHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly GameDataRegistry _data;
        private readonly RngStreams _rng;
        private readonly MonsterSpawner _spawner;

        /// Creates the handler; spawner supplies the non-boss count and the per-run boss flag. Null throws ArgumentNullException.
        public MonsterCommandHandler(EntityLifecycle lifecycle, GameDataRegistry data, RngStreams rng, MonsterSpawner spawner)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        }

        /// Commands dropped because their definition (monster, projectile or zone pattern) could not be resolved.
        public int DroppedCommandCount { get; private set; }

        /// Applies one command; throws InvalidOperationException for a kind this handler does not own.
        public void Handle(in SimulationCommand command, in StepContext context)
        {
            switch (command.Kind)
            {
                case SimulationCommandKind.SpawnEnemyProjectile:
                    SpawnProjectile(command);
                    break;
                case SimulationCommandKind.SpawnEnemyZone:
                    SpawnZone(command);
                    break;
                case SimulationCommandKind.SpawnMonsters:
                    SpawnMonsters(command, context);
                    break;
                default:
                    throw new InvalidOperationException("MonsterCommandHandler does not handle " + command.Kind + ".");
            }
        }

        private void SpawnProjectile(in SimulationCommand command)
        {
            MonsterDefinition shooter = ResolveSourceDefinition(command);
            EnemyProjectileDefinition projectile = shooter?.Ranged?.Projectile;
            if (projectile == null)
            {
                DroppedCommandCount++;
                return;
            }

            Vector2 direction = command.Direction;
            _lifecycle.Spawn<EnemyProjectile>(EntityCategory.EnemyProjectile, command.Position, projectile.LifetimeSteps,
                p => p.Initialize(projectile, direction));
        }

        private void SpawnZone(in SimulationCommand command)
        {
            MonsterDefinition boss = ResolveSourceDefinition(command);
            IReadOnlyList<BossPatternDefinition> patterns = boss?.BossPatterns;
            int index = command.SlotIndex;
            if (patterns == null || index < 0 || index >= patterns.Count || patterns[index].Kind != BossPatternKind.HazardZone)
            {
                DroppedCommandCount++;
                return;
            }

            BossPatternDefinition pattern = patterns[index];
            _lifecycle.Spawn<EnemyHazardZone>(EntityCategory.EnemyZone, command.Position, pattern.DurationSteps, z => z.Initialize(pattern));
        }

        private void SpawnMonsters(in SimulationCommand command, in StepContext context)
        {
            GameData data = _data.Current;
            if (data == null || command.DefinitionId == null || !data.TryGetMonster(command.DefinitionId, out MonsterDefinition definition))
            {
                DroppedCommandCount++;
                return;
            }

            bool isBoss = definition.Behavior == MonsterBehavior.Boss;
            // R31 multiplier of this step, except for a boss's summons (x1.0); a dev command has Source = None.
            bool bossSummon = _lifecycle.TryGet(command.Source, out SimEntity source) && source is Monster summoner && summoner.IsBoss;
            float hpMultiplier = bossSummon ? 1f : (data.SpawnTable?.HpMultiplierAt(context.Step) ?? 1f);
            float playerRadius = _spawner.PlayerRadius;
            DeterministicRng summonRng = _rng.Get(RngStream.Summon);
            for (int i = 0; i < command.Count; i++)
            {
                // Refused monsters draw no angle, and once refused the rest of the command is refused too (live cannot drop here).
                if (!command.IgnoreCap && _spawner.LiveNonBossCount >= context.Rules.MonsterCap)
                {
                    return;
                }

                Vector2 position = command.Radius > 0f
                    ? SpawnGeometry.PointOnCircle(command.Position, command.Radius, summonRng.NextFloat01())
                    : command.Position;
                _lifecycle.Spawn<Monster>(EntityCategory.Monster, position, 0, m => m.Initialize(definition, isBoss, hpMultiplier, playerRadius));
                if (isBoss)
                {
                    _spawner.MarkBossSpawned();
                }
            }
        }

        private MonsterDefinition ResolveSourceDefinition(in SimulationCommand command)
        {
            if (_lifecycle.TryGet(command.Source, out SimEntity entity) && entity is Monster source && source.Definition != null
                && source.Definition.Id == command.DefinitionId)
            {
                return source.Definition;
            }

            GameData data = _data.Current;
            if (data != null && command.DefinitionId != null && data.TryGetMonster(command.DefinitionId, out MonsterDefinition definition))
            {
                return definition;
            }

            return null;
        }
    }
}
