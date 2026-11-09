using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Flow
{
    // Deterministic scripted movement (a function of the step and the simulation state): stand still, walk a square, or walk to
    // the point Seek returns (lowest-id gem) and fall back to the square.
    internal sealed class FlowPmInput : IInputSource
    {
        internal bool Still;
        internal Func<Vector2?> Seek;

        public MoveCommand ReadMove(long step, Vector2 currentPosition)
        {
            if (Still)
            {
                return MoveCommand.None;
            }

            Vector2? target = Seek?.Invoke();
            if (target.HasValue)
            {
                Vector2 toTarget = target.Value - currentPosition;
                return toTarget.sqrMagnitude < 1e-6f ? MoveCommand.None : new MoveCommand(toTarget.normalized);
            }

            int leg = (int)(step / 90 % 4);
            Vector2 direction = leg == 0 ? Vector2.right : leg == 1 ? Vector2.up : leg == 2 ? Vector2.left : Vector2.down;
            return new MoveCommand(direction);
        }
    }

    // No normal spawns and no boss (the flow test drives level-ups and death through pending commands).
    internal sealed class FlowPmNoSpawnPolicy : ISpawnPolicy
    {
        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
        }

        public bool BossSpawnDue(long step)
        {
            return false;
        }
    }

    // Keeps `target` live monsters (5c.5 "N=200 유지"): up to `perStep` new ones per step, cycling through the ids. No boss.
    internal sealed class FlowPmCrowdPolicy : ISpawnPolicy
    {
        private readonly int _target;
        private readonly int _perStep;
        private readonly string[] _ids;
        private int _next;

        internal FlowPmCrowdPolicy(int target, int perStep, params string[] ids)
        {
            _target = target;
            _perStep = perStep;
            _ids = ids;
        }

        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
            int missing = Math.Min(_perStep, _target - liveMonsterCount);
            for (int i = 0; i < missing; i++)
            {
                monsterIdsOut.Add(_ids[_next]);
                _next = (_next + 1) % _ids.Length;
            }
        }

        public bool BossSpawnDue(long step)
        {
            return false;
        }
    }

    // Collision handler that must never be installed (the router already has a handler on every channel).
    internal sealed class FlowPmNullHandler : ICollisionEventHandler
    {
        public void Handle(in CollisionEvent e, in StepContext context)
        {
        }
    }

    // Phase handler that must never be installed (each probed order is already taken).
    internal sealed class FlowPmNullPhase : IStepPhaseHandler
    {
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
        }
    }
}
