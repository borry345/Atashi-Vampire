using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// BuildIndex order -10: one monster-monster separation pass per step, after every monster moved (both tick strategies) and
    /// before the snapshot (0), body sync (5) and spatial index (10). Overlapping pairs are found from the pass-start positions,
    /// pushes are accumulated in (smaller Id, larger Id) order and applied once at the end; the boss is never pushed (5.12.1).
    public sealed class MonsterSeparation : IStepPhaseHandler
    {
        private static readonly Vector2 PlusX = new Vector2(1f, 0f);

        private readonly MonsterRegistry _registry;
        // Reused per pass; grown (doubled) only when the live monster count exceeds the capacity.
        private Vector2[] _start = new Vector2[0];
        private Vector2[] _delta = new Vector2[0];
        private float[] _radius = new float[0];
        private bool[] _isBoss = new bool[0];

        /// Creates the pass over registry's live monsters; null throws ArgumentNullException.
        public MonsterSeparation(MonsterRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// BuildIndex: runs one separation pass over MonsterRegistry.Monsters; every other phase is ignored.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.BuildIndex)
            {
                return;
            }

            IReadOnlyList<SimEntity> monsters = _registry.Monsters;
            int n = monsters.Count;
            EnsureCapacity(n);
            for (int i = 0; i < n; i++)
            {
                SimEntity monster = monsters[i];
                _start[i] = monster.Position;
                _delta[i] = Vector2.zero;
                _radius[i] = monster.Body.Radius;
                _isBoss[i] = monster is Monster m && m.IsBoss;
            }

            for (int a = 0; a < n - 1; a++)
            {
                for (int b = a + 1; b < n; b++)
                {
                    AccumulatePair(a, b);
                }
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 delta = _delta[i];
                if (delta.x != 0f || delta.y != 0f)
                {
                    monsters[i].PlaceAt(_start[i] + delta);
                }
            }
        }

        // One pair (a < b in ascending Id): strict overlap test on pass-start positions, then the push split by the boss rule.
        private void AccumulatePair(int a, int b)
        {
            float rSum = _radius[a] + _radius[b];
            Vector2 d = _start[b] - _start[a];
            float distSq = d.x * d.x + d.y * d.y;
            if (!(distSq < rSum * rSum))
            {
                return;
            }

            bool bossA = _isBoss[a];
            bool bossB = _isBoss[b];
            if (bossA && bossB)
            {
                // Two bosses only arise from dev spawns; neither is pushed.
                return;
            }

            float dist = Mathf.Sqrt(distSq);
            float depth = rSum - dist;
            bool coincident = !(dist > 0f);
            Vector2 u = coincident ? PlusX : d / dist;
            if (bossA)
            {
                _delta[b] += u * depth;
            }
            else if (bossB)
            {
                // Coincident centers push the non-boss toward +x even when it is the smaller Id.
                if (coincident)
                {
                    _delta[a] += PlusX * depth;
                }
                else
                {
                    _delta[a] -= u * depth;
                }
            }
            else
            {
                float half = depth / 2f;
                _delta[a] -= u * half;
                _delta[b] += u * half;
            }
        }

        private void EnsureCapacity(int count)
        {
            if (count <= _start.Length)
            {
                return;
            }

            int capacity = Math.Max(count, Math.Max(16, _start.Length * 2));
            _start = new Vector2[capacity];
            _delta = new Vector2[capacity];
            _radius = new float[capacity];
            _isBoss = new bool[capacity];
        }
    }
}
