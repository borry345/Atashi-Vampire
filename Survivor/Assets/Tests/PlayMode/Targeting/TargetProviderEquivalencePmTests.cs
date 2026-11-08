using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Tests.PlayMode.Collision;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Targeting
{
    /// TAC-F16-1 PM: BruteForce (A), SpatialHash (B, four cell sizes) and Physics2D (C) return bitwise identical results
    /// over real monster bodies. Layout distributions follow the EditMode A=B test.
    public sealed class TargetProviderEquivalencePmTests : CollisionPmFixture
    {
        private static readonly int[] RandomCounts = { 1, 50, 400 };

        private TargetingPmWorld _world;
        private List<MonsterSnapshot> _monsters;
        private int _queries;
        private int _found;
        private long _overlapCalls;

        [SetUp]
        public void SetUp()
        {
            _world = new TargetingPmWorld(CollisionPmUtil.PhysicsTargetRequirements());
            _monsters = new List<MonsterSnapshot>();
            _queries = 0;
            _found = 0;
            _overlapCalls = 0;
        }

        [Test]
        public void RandomLayouts_200_AEqualsBEqualsC()
        {
            var rng = new DeterministicRng(0x5EED_F163UL, 4UL);
            var stopwatch = Stopwatch.StartNew();
            var queries = new List<Vector2>();
            const int layouts = 200;
            for (int layout = 0; layout < layouts; layout++)
            {
                var center = new Vector2(Range(rng, -2000f, 2000f), Range(rng, -2000f, 2000f));
                float spread = Range(rng, 0.5f, 60f);
                FillRandom(rng, RandomCounts[layout % RandomCounts.Length], center, spread);
                _world.Load(_monsters);

                queries.Clear();
                queries.Add(_monsters[rng.NextInt(_monsters.Count)].Position);
                float boundaryCell = TargetingPmWorld.CellSizes[rng.NextInt(TargetingPmWorld.CellSizes.Length)];
                queries.Add(new Vector2(
                    Mathf.FloorToInt(center.x / boundaryCell) * boundaryCell,
                    Mathf.FloorToInt(center.y / boundaryCell) * boundaryCell));
                queries.Add(center + new Vector2(Range(rng, -1.5f, 1.5f) * spread, Range(rng, -1.5f, 1.5f) * spread));
                queries.Add(center + new Vector2(Range(rng, -1e5f, 1e5f), Range(rng, -1e5f, 1e5f)));
                queries.Add(new Vector2(-Range(rng, 0f, 3000f), -Range(rng, 0f, 3000f)));
                queries.Add(new Vector2(Range(rng, -3000f, 3000f), Range(rng, -3000f, 3000f)));
                CompareAll(queries, $"random layout {layout}");
            }

            Report("random", layouts, stopwatch);
        }

        [Test]
        public void GridLayouts_EquidistantTies_AEqualsBEqualsC()
        {
            var rng = new DeterministicRng(0x6121_D003UL, 5UL);
            var stopwatch = Stopwatch.StartNew();
            var queries = new List<Vector2>();
            float[] steps = { 2f, 1f, 0.5f, 3f };
            int layouts = 0;
            foreach (float step in steps)
            {
                foreach (bool withLarge in new[] { false, true })
                {
                    var positions = new List<Vector2>();
                    for (int i = -5; i <= 5; i++)
                    {
                        for (int j = -5; j <= 5; j++)
                        {
                            positions.Add(new Vector2(i * step, j * step));
                        }
                    }

                    Shuffle(rng, positions);
                    _monsters.Clear();
                    int id = 0;
                    foreach (Vector2 position in positions)
                    {
                        id += 1 + rng.NextInt(2);
                        bool isLarge = withLarge && rng.Chance(0.15f);
                        _monsters.Add(TargetingPmWorld.Monster(id, 1 + rng.NextInt(3), position.x, position.y, isLarge ? 4f : 0.4f, isLarge));
                    }

                    _world.Load(_monsters);
                    queries.Clear();
                    float half = step * 0.5f;
                    for (int qi = -13; qi <= 13; qi++)
                    {
                        for (int qj = -13; qj <= 13; qj++)
                        {
                            queries.Add(new Vector2(qi * half, qj * half));
                        }
                    }

                    CompareAll(queries, $"grid step {step} large {withLarge}");
                    layouts++;
                }
            }

            Report("grid", layouts, stopwatch);
        }

        [Test]
        public void LayoutsWithLargeMonsters_200_AEqualsBEqualsC()
        {
            var rng = new DeterministicRng(0xB055_0003UL, 6UL);
            var stopwatch = Stopwatch.StartNew();
            var queries = new List<Vector2>();
            const int layouts = 200;
            for (int layout = 0; layout < layouts; layout++)
            {
                var center = new Vector2(Mathf.Round(Range(rng, -300f, 300f)), Mathf.Round(Range(rng, -300f, 300f)));
                _monsters.Clear();
                int id = 0;
                int count = 1 + rng.NextInt(30);
                Vector2 last = center;
                for (int i = 0; i < count; i++)
                {
                    id += 1 + rng.NextInt(3);
                    bool isLarge = rng.Chance(0.2f);
                    Vector2 position;
                    int shape = rng.NextInt(3);
                    if (shape == 0)
                    {
                        // Integer offsets give exact squared-distance ties against integer query points.
                        position = center + new Vector2(rng.NextInt(21) - 10, rng.NextInt(21) - 10);
                    }
                    else if (shape == 1)
                    {
                        position = last;
                    }
                    else
                    {
                        position = center + new Vector2(Range(rng, -40f, 40f), Range(rng, -40f, 40f));
                    }

                    _monsters.Add(TargetingPmWorld.Monster(id, 1, position.x, position.y, isLarge ? Range(rng, 1.01f, 6f) : Range(rng, 0.2f, 1f), isLarge));
                    last = position;
                }

                _world.Load(_monsters);
                queries.Clear();
                queries.Add(center);
                queries.Add(_monsters[rng.NextInt(_monsters.Count)].Position);
                for (int q = 0; q < 6; q++)
                {
                    queries.Add(center + new Vector2(rng.NextInt(31) - 15, rng.NextInt(31) - 15));
                }

                queries.Add(center + new Vector2(Range(rng, -5000f, 5000f), Range(rng, -5000f, 5000f)));
                CompareAll(queries, $"large layout {layout}");
            }

            Report("large", layouts, stopwatch);
        }

        private void FillRandom(DeterministicRng rng, int count, Vector2 center, float spread)
        {
            _monsters.Clear();
            int id = 0;
            Vector2 last = center;
            for (int i = 0; i < count; i++)
            {
                id += 1 + rng.NextInt(3);
                bool isLarge = rng.Chance(0.06f);
                Vector2 position = i > 0 && rng.Chance(0.1f)
                    ? last
                    : center + new Vector2(Range(rng, -spread, spread), Range(rng, -spread, spread));
                float radius = isLarge ? Range(rng, 1.5f, 6f) : Range(rng, 0.2f, 1f);
                _monsters.Add(TargetingPmWorld.Monster(id, 1 + rng.NextInt(4), position.x, position.y, radius, isLarge));
                last = position;
            }
        }

        private void CompareAll(List<Vector2> queries, string label)
        {
            foreach (Vector2 query in queries)
            {
                string difference = _world.Compare(query, out bool foundA);
                _queries++;
                if (foundA)
                {
                    _found++;
                }

                if (difference.Length > 0)
                {
                    Assert.Fail($"{label}, query ({query.x:R}, {query.y:R}), monsters {_world.Snapshot.Count}: {difference}");
                }

                _overlapCalls += _world.CountOverlapCalls(query);
            }
        }

        private void Report(string batch, int layouts, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            Assert.AreEqual(_queries, _found);
            TestContext.WriteLine(
                $"[equivalence-pm] batch={batch} layouts={layouts} queries={_queries} found={_found} " +
                $"avgOverlapCalls={(double)_overlapCalls / _queries:F3} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }

        private static float Range(DeterministicRng rng, float min, float max)
        {
            return min + (max - min) * rng.NextFloat01();
        }

        private static void Shuffle(DeterministicRng rng, List<Vector2> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
