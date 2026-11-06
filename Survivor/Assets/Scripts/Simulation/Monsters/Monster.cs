using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Pooled monster (M1 chaser, M2 charger, M3 ranged, M4 boss). Moves toward StepContext.PlayerPosition only (TAC-F3-11);
    /// projectiles, hazard zones and summons are deferred commands (TAC-0-6). Kills are confirmed by KillPipeline.
    public sealed class Monster : SimEntity, IUpdatable, IDamageable, IContactDamageSource
    {
        private readonly MonsterBehaviorState _state = new MonsterBehaviorState();

        /// Definition captured at spawn; hot reload does not affect a live monster (5c.3 #10).
        public MonsterDefinition Definition { get; private set; }

        /// True for the boss (excluded from the cap and the despawn rule, always large).
        public bool IsBoss { get; private set; }

        /// Current health; starts at Definition.MaxHealth.
        public float Hp { get; private set; }

        /// Step in which health reached 0, or -1 while alive.
        public long DiedAtStep { get; private set; } = -1;

        /// True once KillPipeline confirmed this monster's kill (prevents a second confirmation).
        public bool KillConfirmed { get; private set; }

        /// Behavior state (charge phase, fire cooldown, pattern cooldowns).
        public MonsterBehaviorState State => _state;

        /// Tick group of every monster.
        public TickGroup Group => TickGroup.Monsters;

        /// True while health is above 0.
        public bool IsAlive => Hp > 0f;

        /// Body contact damage from the definition.
        public float ContactDamage => Definition.ContactDamage;

        // Spawn initialize callback: definition, boss flag, body size (R25) and behavior state.
        internal void Initialize(MonsterDefinition definition, bool isBoss)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            IsBoss = isBoss;
            Hp = definition.MaxHealth;
            DiedAtStep = -1;
            KillConfirmed = false;
            Body.Radius = definition.Radius;
            Body.IsLarge = MonsterSizeRules.IsLarge(definition.Radius, isBoss);
            IReadOnlyList<BossPatternDefinition> patterns = definition.BossPatterns;
            _state.Reset(SpawnStep, definition.Behavior == MonsterBehavior.Boss && patterns != null ? patterns.Count : 0);
        }

        internal void ConfirmKill()
        {
            KillConfirmed = true;
        }

        /// Subtracts amount at step; ignored once dead. Records DiedAtStep when health reaches 0.
        /// Throws ArgumentOutOfRangeException unless amount is finite and >= 0.
        public void ApplyDamage(float amount, long step)
        {
            if (!(amount >= 0f) || float.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage must be a finite, non-negative number.");
            }

            if (!IsAlive)
            {
                return;
            }

            Hp -= amount;
            if (Hp <= 0f)
            {
                DiedAtStep = step;
            }
        }

        /// Runs the behavior of Definition.Behavior for one step.
        public void Tick(in StepContext context)
        {
            Debug.Assert(context.Step > SpawnStep, "Monster.Tick must not run in the spawn step.");
            switch (Definition.Behavior)
            {
                case MonsterBehavior.Chaser:
                    Position = StepMove.Toward(Position, context.PlayerPosition, Definition.MoveSpeedPerStep);
                    break;
                case MonsterBehavior.Charger:
                    TickCharger(context);
                    break;
                case MonsterBehavior.Ranged:
                    TickRanged(context);
                    break;
                case MonsterBehavior.Boss:
                    TickBoss(context);
                    break;
                default:
                    throw new InvalidOperationException("Monster: unknown behavior " + Definition.Behavior + ".");
            }
        }

        /// Clears the definition and state before the instance returns to the pool.
        protected override void OnReleased()
        {
            Definition = null;
            IsBoss = false;
            Hp = 0f;
            DiedAtStep = -1;
            KillConfirmed = false;
            _state.Reset(0, 0);
        }

        // TAC-F3-7: Approach -> Charge (DurationSteps, fixed direction) -> Cooldown (CooldownSteps, chasing) -> Approach.
        private void TickCharger(in StepContext context)
        {
            ChargeDefinition charge = Definition.Charge;
            long step = context.Step;
            if (_state.Phase == MonsterBehaviorState.ChargePhase.Charge && step >= _state.PhaseEndStep)
            {
                _state.Phase = MonsterBehaviorState.ChargePhase.Cooldown;
                _state.PhaseEndStep = step + charge.CooldownSteps;
            }

            if (_state.Phase == MonsterBehaviorState.ChargePhase.Cooldown && step >= _state.PhaseEndStep)
            {
                _state.Phase = MonsterBehaviorState.ChargePhase.Approach;
            }

            if (_state.Phase == MonsterBehaviorState.ChargePhase.Approach)
            {
                float trigger = charge.TriggerDistance;
                if ((context.PlayerPosition - Position).sqrMagnitude <= trigger * trigger)
                {
                    _state.Phase = MonsterBehaviorState.ChargePhase.Charge;
                    _state.PhaseEndStep = step + charge.DurationSteps;
                    _state.ChargeDirection = StepMove.UnitDirection(Position, context.PlayerPosition);
                }
            }

            if (_state.Phase == MonsterBehaviorState.ChargePhase.Charge)
            {
                Position += _state.ChargeDirection * charge.ChargeSpeedPerStep;
            }
            else
            {
                Position = StepMove.Toward(Position, context.PlayerPosition, Definition.MoveSpeedPerStep);
            }
        }

        // TAC-F3-8: stop inside attack range and fire every FireCooldownSteps (first shot as soon as it is in range).
        private void TickRanged(in StepContext context)
        {
            RangedDefinition ranged = Definition.Ranged;
            float range = ranged.AttackRange;
            if ((context.PlayerPosition - Position).sqrMagnitude > range * range)
            {
                Position = StepMove.Toward(Position, context.PlayerPosition, Definition.MoveSpeedPerStep);
                return;
            }

            if (context.Step >= _state.NextFireStep)
            {
                context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnEnemyProjectile, Id,
                    position: Position, direction: StepMove.UnitDirection(Position, context.PlayerPosition), definitionId: Definition.Id));
                _state.NextFireStep = context.Step + ranged.FireCooldownSteps;
            }
        }

        // TAC-F3-9: chase, and use every pattern on its own cooldown. The cooldown is spent when the command is issued,
        // whether or not the summon is later refused by the cap.
        private void TickBoss(in StepContext context)
        {
            Position = StepMove.Toward(Position, context.PlayerPosition, Definition.MoveSpeedPerStep);
            IReadOnlyList<BossPatternDefinition> patterns = Definition.BossPatterns;
            for (int i = 0; i < _state.PatternCount; i++)
            {
                if (context.Step < _state.NextPatternStep(i))
                {
                    continue;
                }

                BossPatternDefinition pattern = patterns[i];
                if (pattern.Kind == BossPatternKind.HazardZone)
                {
                    Vector2 at = pattern.Placement == ZonePlacement.Player ? context.PlayerPosition : Position;
                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnEnemyZone, Id,
                        position: at, radius: pattern.ZoneRadius, definitionId: Definition.Id, slotIndex: i));
                }
                else
                {
                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, Id,
                        position: Position, radius: pattern.SummonRadius, definitionId: pattern.MonsterId, count: pattern.Count,
                        ignoreCap: false));
                }

                _state.SetNextPatternStep(i, context.Step + pattern.CooldownSteps);
            }
        }
    }
}
