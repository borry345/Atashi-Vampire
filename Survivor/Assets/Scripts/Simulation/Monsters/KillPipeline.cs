using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Resolve order 30: confirms this step's kills in ascending Id order and is the only place the kill count grows (R24).
    /// Normal monster: SpawnGem -> one Drop-stream chest draw (always, even at chance 0) -> SpawnChest if true -> Despawn(Killed).
    /// Boss: no drops and no chest draw; BossKilledThisStep is raised for the step outcome gate, then Despawn(Killed).
    public sealed class KillPipeline : IStepPhaseHandler
    {
        private readonly MonsterRegistry _registry;
        private readonly GameDataRegistry _data;
        private readonly RngStreams _rng;

        /// Creates the pipeline; null arguments throw ArgumentNullException.
        public KillPipeline(MonsterRegistry registry, GameDataRegistry data, RngStreams rng)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// Kills confirmed in this run (bosses and summons included).
        public int KillCount { get; private set; }

        /// True when the boss was killed in the current step; cleared at the start of every Resolve order 30.
        public bool BossKilledThisStep { get; private set; }

        /// Run reset: kill count 0, boss flag cleared.
        public void Reset()
        {
            KillCount = 0;
            BossKilledThisStep = false;
        }

        /// Resolve: processes every monster whose health reached 0 in this step; commands use the killed monster as Source.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.Resolve)
            {
                return;
            }

            BossKilledThisStep = false;
            IReadOnlyList<SimEntity> monsters = _registry.Monsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (!(monsters[i] is Monster monster) || monster.KillConfirmed || monster.DiedAtStep != context.Step)
                {
                    continue;
                }

                monster.ConfirmKill();
                if (KillRules.CountsAsKill(DespawnReason.Killed))
                {
                    KillCount++;
                }

                if (monster.IsBoss)
                {
                    BossKilledThisStep = true;
                }
                else
                {
                    EnqueueDrops(monster, context);
                }

                context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, monster.Id, target: monster.Id,
                    reason: DespawnReason.Killed));
            }
        }

        private void EnqueueDrops(Monster monster, in StepContext context)
        {
            int xp = _data.Current.PlayerStats.Pickups.GemXpValue;
            context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, monster.Id, position: monster.Position, count: xp));

            // Chance() draws even when the chance is 0, so the Drop stream advances by exactly one per normal kill (TAC-F4-2).
            if (_rng.Get(RngStream.Drop).Chance(context.Rules.ChestDropChance))
            {
                context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, monster.Id, position: monster.Position));
            }
        }
    }
}
