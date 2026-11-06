using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    // Scenario entity: monsters chase the player and despawn themselves (by command) on contact; projectiles fly straight
    // and re-aim at the nearest monster through StepContext.Targets every tick.
    public sealed class LoopPmMover : SimEntity, IUpdatable
    {
        internal const float MonsterSpeedPerStep = 0.05f;
        internal const float ProjectileSpeedPerStep = 0.2f;
        internal const float ContactDistance = 0.5f;

        private Vector2 _direction;
        internal int TickCount;

        public TickGroup Group => Category == EntityCategory.Monster ? TickGroup.Monsters : TickGroup.Projectiles;

        internal void Launch(Vector2 direction)
        {
            _direction = direction;
            TickCount = 0;
        }

        public void Tick(in StepContext context)
        {
            Debug.Assert(context.Step > SpawnStep);
            TickCount++;
            if (Category == EntityCategory.Monster)
            {
                Vector2 toPlayer = context.PlayerPosition - Position;
                if (toPlayer.sqrMagnitude <= ContactDistance * ContactDistance)
                {
                    context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, Id, target: Id, reason: DespawnReason.Killed));
                    return;
                }

                Position += MoveMath.DirectionOrDefault(toPlayer) * MonsterSpeedPerStep;
                return;
            }

            if (context.Targets.TryFindNearest(Position, out TargetHit hit))
            {
                _direction = MoveMath.DirectionOrDefault(hit.Position - Position);
            }

            Position += _direction * ProjectileSpeedPerStep;
        }

        protected override void OnReleased()
        {
            _direction = Vector2.zero;
        }
    }
}
