using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.EditMode.Spatial
{
    /// TAC-F16-1: BruteForce (A) and SpatialHash (B) return bitwise identical results for the same snapshot and query.
    public sealed class TargetProviderEquivalenceTests
    {
        private static readonly float[] CellSizes = { StrategySelection.DefaultSpatialHashCellSize, 0.5f, 1f, 3.7f };

        private MonsterSnapshotBuffer _snapshot;
        private BruteForceTargetProvider _bruteForce;
        private MonsterSpatialIndex[] _indices;
        private SpatialHashTargetProvider[] _hashed;
        private int _queries;
        private int _found;

        [SetUp]
        public void SetUp()
        {
            _snapshot = new MonsterSnapshotBuffer();
            _bruteForce = new BruteForceTargetProvider();
            _indices = new MonsterSpatialIndex[CellSizes.Length];
            _hashed = new SpatialHashTargetProvider[CellSizes.Length];
            for (int i = 0; i < CellSizes.Length; i++)
            {
                _indices[i] = new MonsterSpatialIndex(CellSizes[i], _snapshot) { Required = true };
                _hashed[i] = new SpatialHashTargetProvider(_indices[i]);
            }

            _queries = 0;
            _found = 0;
        }

        [Test]
        public void RandomLayouts_10000_AEqualsB()
        {
            var rng = new DeterministicRng(0x5EED_F161UL, 1UL);
            var stopwatch = Stopwatch.StartNew();
            var queries = new List<Vector2>();
            for (int layout = 0; layout < 10000; layout++)
            {
                var center = new Vector2(Range(rng, -2000f, 2000f), Range(rng, -2000f, 2000f));
                float spread = Range(rng, 0.5f, 60f);
                FillRandom(rng, rng.NextInt(48), center, spread);
                BuildAll();

                queries.Clear();
                if (_snapshot.Count > 0)
                {
                    queries.Add(_snapshot[rng.NextInt(_snapshot.Count)].Position);
                }

                float boundaryCell = CellSizes[rng.NextInt(CellSizes.Length)];
                queries.Add(new Vector2(
                    Mathf.FloorToInt(center.x / boundaryCell) * boundaryCell,
                    Mathf.FloorToInt(center.y / boundaryCell) * boundaryCell));
                queries.Add(center + new Vector2(Range(rng, -1.5f, 1.5f) * spread, Range(rng, -1.5f, 1.5f) * spread));
                queries.Add(center + new Vector2(Range(rng, -1e5f, 1e5f), Range(rng, -1e5f, 1e5f)));
                queries.Add(new Vector2(-Range(rng, 0f, 3000f), -Range(rng, 0f, 3000f)));
                queries.Add(new Vector2(Range(rng, -3000f, 3000f), Range(rng, -3000f, 3000f)));
                CompareAll(queries, $"random layout {layout}");
            }

            Report("random", 10000, stopwatch);
        }

        [Test]
        public void GridLayouts_EquidistantTies_AEqualsB()
        {
            var rng = new DeterministicRng(0x6121_D000UL, 2UL);
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
                    _snapshot.Clear();
                    int id = 0;
                    foreach (Vector2 position in positions)
                    {
                        id += 1 + rng.NextInt(2);
                        bool isLarge = withLarge && rng.Chance(0.15f);
                        AddMonster(id, 1 + rng.NextInt(3), position, isLarge ? 4f : 0.4f, isLarge);
                    }

                    BuildAll();
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
        public void LayoutsWithLargeMonsters_AEqualsB()
        {
            var rng = new DeterministicRng(0xB055_0001UL, 3UL);
            var stopwatch = Stopwatch.StartNew();
            var queries = new List<Vector2>();
            for (int layout = 0; layout < 1000; layout++)
            {
                var center = new Vector2(Mathf.Round(Range(rng, -300f, 300f)), Mathf.Round(Range(rng, -300f, 300f)));
                _snapshot.Clear();
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

                    AddMonster(id, 1, position, isLarge ? Range(rng, 1.01f, 6f) : Range(rng, 0.2f, 1f), isLarge);
                    last = position;
                }

                BuildAll();
                queries.Clear();
                queries.Add(center);
                queries.Add(_snapshot[rng.NextInt(_snapshot.Count)].Position);
                for (int q = 0; q < 6; q++)
                {
                    queries.Add(center + new Vector2(rng.NextInt(31) - 15, rng.NextInt(31) - 15));
                }

                queries.Add(center + new Vector2(Range(rng, -5000f, 5000f), Range(rng, -5000f, 5000f)));
                CompareAll(queries, $"large layout {layout}");
            }

            Report("large", 1000, stopwatch);
        }

        private void FillRandom(DeterministicRng rng, int count, Vector2 center, float spread)
        {
            _snapshot.Clear();
            int id = 0;
            Vector2 last = center;
            for (int i = 0; i < count; i++)
            {
                id += 1 + rng.NextInt(3);
                bool isLarge = rng.Chance(0.06f);
                Vector2 position = i > 0 && rng.Chance(0.1f)
                    ? last
                    : center + new Vector2(Range(rng, -spread, spread), Range(rng, -spread, spread));
                AddMonster(id, 1 + rng.NextInt(4), position, isLarge ? Range(rng, 1.5f, 6f) : Range(rng, 0.2f, 1f), isLarge);
                last = position;
            }
        }

        private void AddMonster(int index, int generation, Vector2 position, float radius, bool isLarge)
        {
            _snapshot.Add(new MonsterSnapshot
            {
                Id = new EntityHandle(index, generation),
                Position = position,
                Radius = radius,
                IsLarge = isLarge,
            });
        }

        private void BuildAll()
        {
            for (int i = 0; i < _indices.Length; i++)
            {
                _indices[i].OnPhase(SimulationPhase.BuildIndex, default);
                _hashed[i].OnSnapshotBuilt(_snapshot);
            }

            _bruteForce.OnSnapshotBuilt(_snapshot);
        }

        private void CompareAll(List<Vector2> queries, string label)
        {
            foreach (Vector2 query in queries)
            {
                bool foundA = _bruteForce.TryFindNearest(query, out TargetHit a);
                _queries++;
                if (foundA)
                {
                    _found++;
                }

                for (int i = 0; i < _hashed.Length; i++)
                {
                    bool foundB = _hashed[i].TryFindNearest(query, out TargetHit b);
                    string difference = SpatialTestUtil.Difference(foundA, a, foundB, b);
                    if (difference.Length > 0)
                    {
                        Assert.Fail($"{label}, cellSize {CellSizes[i]:R}, query ({query.x:R}, {query.y:R}), monsters {_snapshot.Count}: {difference}");
                    }
                }
            }
        }

        private void Report(string batch, int layouts, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            Assert.Greater(_found, 0);
            TestContext.WriteLine(
                $"[equivalence] batch={batch} layouts={layouts} queries={_queries} found={_found} " +
                $"cellSizes={CellSizes.Length} comparisons={_queries * CellSizes.Length} elapsedMs={stopwatch.ElapsedMilliseconds}");
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
