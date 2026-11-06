using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using static Survivor.Tests.PlayMode.Collision.CollisionPmUtil;

namespace Survivor.Tests.PlayMode.Collision
{
    /// E1/E2 harness skeleton (5c.5, C10): the same scripted body sequence through the self strategy (colliders off) and the
    /// engine strategy (all colliders on, Discrete / Continuous) must yield identical CollisionEvent lists (full compare key).
    /// The real E1/E2 run the game scenario after wave 4.
    public sealed class CollisionEquivalenceTests : CollisionPmFixture
    {
        private static readonly CollisionLayer[] SpawnLayers =
        {
            CollisionLayer.Monster, CollisionLayer.Monster, CollisionLayer.Monster, CollisionLayer.PlayerProjectile,
            CollisionLayer.PlayerOrb, CollisionLayer.PlayerZone, CollisionLayer.EnemyProjectile, CollisionLayer.EnemyZone,
            CollisionLayer.Gem, CollisionLayer.Gem, CollisionLayer.Chest,
        };

        // seed, bodies, steps, arena half size
        [TestCase(1UL, 60, 600, 6f)]
        [TestCase(2UL, 120, 300, 8f)]
        [TestCase(3UL, 30, 600, 3f)]
        public void Self_Equals_EngineDiscrete_E1(ulong seed, int bodyCount, int steps, float arena)
        {
            CollisionPmWorld self = CollisionPmWorld.Self();
            ScenarioLog selfLog = Scenario.Run(self, seed, bodyCount, steps, arena);
            foreach (CollisionBody body in self.Registered)
            {
                Assert.IsFalse(body.Host.GetComponent<CircleCollider2D>().enabled, "self strategy: colliders stay off (C10)");
            }

            DestroyAll();
            ScenarioLog engineLog = Scenario.Run(CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete), seed, bodyCount, steps, arena);

            AssertSameEvents(selfLog, engineLog, "self", "engine/Discrete");
        }

        [TestCase(1UL, 60, 600, 6f)]
        [TestCase(2UL, 120, 300, 8f)]
        public void EngineDiscrete_Equals_EngineContinuous_E2(ulong seed, int bodyCount, int steps, float arena)
        {
            ScenarioLog discrete = Scenario.Run(CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete), seed, bodyCount, steps, arena);
            DestroyAll();
            ScenarioLog continuous = Scenario.Run(CollisionPmWorld.Engine(CollisionDetectionMode2D.Continuous), seed, bodyCount, steps, arena);

            AssertSameEvents(discrete, continuous, "engine/Discrete", "engine/Continuous");
        }

        private static void AssertSameEvents(ScenarioLog x, ScenarioLog y, string xName, string yName)
        {
            Assert.That(x.Events.Count, Is.GreaterThan(50), "scenario must produce events");
            int count = Math.Min(x.Events.Count, y.Events.Count);
            for (int i = 0; i < count; i++)
            {
                if (x.Events[i].CompareTo(y.Events[i]) != 0 || x.Events[i].Kind != y.Events[i].Kind)
                {
                    Assert.Fail($"first mismatch at #{i}: {xName}={x.Events[i]} ({x.Describe(x.Events[i])}) "
                        + $"{yName}={y.Events[i]} ({y.Describe(y.Events[i])})\n"
                        + $"{xName} history: {x.History(x.Events[i])}\n{yName} history: {y.History(x.Events[i])}\n"
                        + $"{xName} history: {x.History(y.Events[i])}\n{yName} history: {y.History(y.Events[i])}");
                }
            }

            if (x.Events.Count != y.Events.Count)
            {
                ScenarioLog longer = x.Events.Count > y.Events.Count ? x : y;
                Assert.Fail($"event counts differ: {xName}={x.Events.Count} {yName}={y.Events.Count}; first extra "
                    + $"{longer.Events[count]} ({longer.Describe(longer.Events[count])})");
            }

            Debug.Log($"[E1/E2 skeleton] {xName} vs {yName}: {x.Events.Count} events, {x.Steps} steps, identical");
        }

        /// Events of one run plus the per-step positions needed to explain a mismatch (distance vs radius sum, 5c.5 E1).
        private sealed class ScenarioLog
        {
            internal readonly List<CollisionEvent> Events = new List<CollisionEvent>();
            internal readonly List<Dictionary<EntityHandle, Vector3>> Circles = new List<Dictionary<EntityHandle, Vector3>>();
            internal int Steps;

            internal string Describe(in CollisionEvent e)
            {
                Dictionary<EntityHandle, Vector3> circles = Circles[(int)e.Step];
                if (!circles.TryGetValue(e.A, out Vector3 a) || !circles.TryGetValue(e.B, out Vector3 b))
                {
                    return "bodies not registered at that step";
                }

                float distance = Vector2.Distance(a, b);
                return $"distance={distance:R} radiusSum={a.z + b.z:R}";
            }

            /// Every event of e's pair in this log, with the pair geometry at each event step.
            internal string History(in CollisionEvent e)
            {
                var builder = new System.Text.StringBuilder();
                foreach (CollisionEvent other in Events)
                {
                    if (other.Channel == e.Channel && other.A == e.A && other.B == e.B)
                    {
                        builder.Append(other.Step).Append(' ').Append(other.Kind).Append(" (").Append(Describe(other)).Append("); ");
                    }
                }

                return builder.Length == 0 ? "(none)" : builder.ToString();
            }
        }

        /// Deterministic body script: one player body + magnet walking a fixed path, other bodies bouncing in an arena at a
        /// speed of at most their radius per step (F18 invariant), one despawn + spawn every 7 steps.
        private static class Scenario
        {
            internal static ScenarioLog Run(CollisionPmWorld world, ulong seed, int bodyCount, int steps, float arena)
            {
                var rng = new DeterministicRng(seed, 18UL);
                var log = new ScenarioLog { Steps = steps };
                var velocities = new Dictionary<CollisionBody, Vector2>();
                int nextIndex = 1;
                CollisionBody player = world.Add(Body(nextIndex++, CollisionLayer.PlayerBody, 0f, 0f, 0.5f));
                CollisionBody magnet = world.Add(Body(nextIndex++, CollisionLayer.PlayerMagnet, 0f, 0f, 1.5f));
                var movers = new List<CollisionBody>();
                for (int i = 0; i < bodyCount; i++)
                {
                    movers.Add(Spawn(world, rng, nextIndex++, arena, velocities));
                }

                for (long step = 0; step < steps; step++)
                {
                    var circles = new Dictionary<EntityHandle, Vector3>();
                    foreach (CollisionBody body in world.Registered)
                    {
                        circles[body.Id] = new Vector3(body.Position.x, body.Position.y, body.Radius);
                    }

                    log.Circles.Add(circles);
                    log.Events.AddRange(world.Step(step));

                    float angle = step * 0.02f;
                    player.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle * 1.3f)) * (arena * 0.5f);
                    magnet.Position = player.Position;
                    foreach (CollisionBody body in movers)
                    {
                        Vector2 velocity = velocities[body];
                        Vector2 next = body.Position + velocity;
                        if (Mathf.Abs(next.x) > arena)
                        {
                            velocity.x = -velocity.x;
                        }

                        if (Mathf.Abs(next.y) > arena)
                        {
                            velocity.y = -velocity.y;
                        }

                        velocities[body] = velocity;
                        body.Position += velocity;
                    }

                    if (step % 7 == 3)
                    {
                        int victim = rng.NextInt(movers.Count);
                        world.Remove(movers[victim]);
                        movers[victim] = Spawn(world, rng, nextIndex++, arena, velocities);
                    }
                }

                return log;
            }

            private static CollisionBody Spawn(CollisionPmWorld world, DeterministicRng rng, int index, float arena,
                                               Dictionary<CollisionBody, Vector2> velocities)
            {
                CollisionLayer layer = SpawnLayers[rng.NextInt(SpawnLayers.Length)];
                float radius = 0.15f + rng.NextFloat01() * 0.45f;
                bool large = false;
                if (layer == CollisionLayer.Monster && rng.NextInt(12) == 0)
                {
                    radius = 1.2f + rng.NextFloat01();
                    large = true;
                }
                else if (layer == CollisionLayer.PlayerZone || layer == CollisionLayer.EnemyZone)
                {
                    radius += 0.8f;
                }

                float x = (rng.NextFloat01() * 2f - 1f) * arena;
                float y = (rng.NextFloat01() * 2f - 1f) * arena;
                CollisionBody body = Body(index, layer, x, y, radius);
                body.IsLarge = large;
                bool stationary = layer == CollisionLayer.Gem || layer == CollisionLayer.Chest || layer == CollisionLayer.PlayerZone
                    || layer == CollisionLayer.EnemyZone;
                float angle = rng.NextFloat01() * Mathf.PI * 2f;
                float speed = stationary ? 0f : rng.NextFloat01() * radius;
                velocities[body] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                return world.Add(body);
            }
        }
    }
}
