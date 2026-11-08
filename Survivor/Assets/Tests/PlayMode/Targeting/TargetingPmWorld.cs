using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Strategies;
using Survivor.Tests.PlayMode.Collision;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Targeting
{
    /// Real monster bodies plus providers A, B (one per cell size) and C over one snapshot, stepped like BuildIndex
    /// (synchronizer order 5, index order 10, then OnSnapshotBuilt from the driver, 5.07).
    internal sealed class TargetingPmWorld
    {
        internal static readonly float[] CellSizes = { StrategySelection.DefaultSpatialHashCellSize, 0.5f, 1f, 3.7f };

        private const float InitialRadius = 4f;
        private const float RequeryMargin = 1e-3f;

        private readonly List<GameObject> _hosts = new List<GameObject>();
        private readonly List<CollisionBody> _registered = new List<CollisionBody>();
        private readonly List<Collider2D> _probe = new List<Collider2D>();
        private readonly MonsterSpatialIndex[] _indices;
        private ContactFilter2D _probeFilter;

        internal TargetingPmWorld(PhysicsBodyRequirements requirements)
        {
            Bodies = new PhysicsBodySynchronizer(requirements);
            Snapshot = new MonsterSnapshotBuffer();
            BruteForce = new BruteForceTargetProvider();
            _indices = new MonsterSpatialIndex[CellSizes.Length];
            Hashed = new SpatialHashTargetProvider[CellSizes.Length];
            for (int i = 0; i < CellSizes.Length; i++)
            {
                _indices[i] = new MonsterSpatialIndex(CellSizes[i], Snapshot) { Required = true };
                Hashed[i] = new SpatialHashTargetProvider(_indices[i]);
            }

            Physics = new Physics2DTargetProvider(Bodies, MonsterMask);
            _probeFilter = new ContactFilter2D();
            _probeFilter.SetLayerMask(MonsterMask);
            _probeFilter.useTriggers = true;
        }

        internal static int MonsterMask => CollisionLayerMapping.MaskOf(CollisionLayer.Monster);

        internal PhysicsBodySynchronizer Bodies { get; }

        internal MonsterSnapshotBuffer Snapshot { get; }

        internal BruteForceTargetProvider BruteForce { get; }

        internal SpatialHashTargetProvider[] Hashed { get; }

        internal Physics2DTargetProvider Physics { get; }

        internal IReadOnlyList<CollisionBody> Registered => _registered;

        /// Replaces every registered monster body with monsters (ascending Id), reusing host GameObjects, then steps.
        internal void Load(IReadOnlyList<MonsterSnapshot> monsters)
        {
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                Bodies.Unregister(_registered[i]);
            }

            _registered.Clear();
            Snapshot.Clear();
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterSnapshot monster = monsters[i];
                if (i == _hosts.Count)
                {
                    _hosts.Add(new GameObject(CollisionPmUtil.NamePrefix + "_Target_" + i));
                }

                var body = new CollisionBody
                {
                    Id = monster.Id,
                    Layer = CollisionLayer.Monster,
                    Position = monster.Position,
                    Radius = monster.Radius,
                    IsLarge = monster.IsLarge,
                    Host = _hosts[i],
                };
                Bodies.Register(body);
                _registered.Add(body);
                Snapshot.Add(monster);
            }

            Step();
        }

        /// Rebuilds the snapshot from the registered bodies' current Position, then runs the BuildIndex order and OnSnapshotBuilt.
        internal void Step()
        {
            Snapshot.Clear();
            foreach (CollisionBody body in _registered)
            {
                Snapshot.Add(new MonsterSnapshot { Id = body.Id, Position = body.Position, Radius = body.Radius, IsLarge = body.IsLarge });
            }

            Bodies.OnPhase(SimulationPhase.BuildIndex, default);
            for (int i = 0; i < _indices.Length; i++)
            {
                _indices[i].OnPhase(SimulationPhase.BuildIndex, default);
                Hashed[i].OnSnapshotBuilt(Snapshot);
            }

            BruteForce.OnSnapshotBuilt(Snapshot);
            Physics.OnSnapshotBuilt(Snapshot);
        }

        /// Empty when A, every B and C agree bitwise at point; otherwise a description of the first difference.
        internal string Compare(Vector2 point, out bool foundA)
        {
            foundA = BruteForce.TryFindNearest(point, out TargetHit a);
            for (int i = 0; i < Hashed.Length; i++)
            {
                bool foundB = Hashed[i].TryFindNearest(point, out TargetHit b);
                string differenceB = Difference(foundA, a, foundB, b);
                if (differenceB.Length > 0)
                {
                    return $"B(cellSize {CellSizes[i]:R}): {differenceB}";
                }
            }

            bool foundC = Physics.TryFindNearest(point, out TargetHit c);
            string differenceC = Difference(foundA, a, foundC, c);
            return differenceC.Length > 0 ? "C: " + differenceC : string.Empty;
        }

        /// OverlapCircle calls C makes for point (widening stages + one re-query), counted with the same filter and schedule.
        internal int CountOverlapCalls(Vector2 point)
        {
            if (Snapshot.Count == 0)
            {
                return 0;
            }

            Rect bounds = Snapshot.Bounds;
            float dx = Mathf.Max(Mathf.Abs(bounds.xMin - point.x), Mathf.Abs(bounds.xMax - point.x));
            float dy = Mathf.Max(Mathf.Abs(bounds.yMin - point.y), Mathf.Abs(bounds.yMax - point.y));
            float limit = Mathf.Sqrt(dx * dx + dy * dy);
            float radius = InitialRadius;
            int calls = 0;
            while (true)
            {
                calls++;
                Physics2D.OverlapCircle(point, radius, _probeFilter, _probe);
                bool any = false;
                foreach (Collider2D collider in _probe)
                {
                    any |= Bodies.TryGetBody(collider, out CollisionBody body) && Snapshot.TryGetIndex(body.Id, out _);
                }

                _probe.Clear();
                if (any)
                {
                    return calls + 1;
                }

                if (radius >= limit)
                {
                    return calls;
                }

                radius = Mathf.Min(radius * 2f, limit);
            }
        }

        /// Empty when both results are bitwise equal (bool, Id, Position bits, DistanceSquared bits).
        internal static string Difference(bool foundA, TargetHit a, bool foundOther, TargetHit other)
        {
            if (foundA != foundOther)
            {
                return $"found {foundA} vs {foundOther}";
            }

            if (!foundA)
            {
                return string.Empty;
            }

            if (a.Id != other.Id
                || Bits(a.Position.x) != Bits(other.Position.x)
                || Bits(a.Position.y) != Bits(other.Position.y)
                || Bits(a.DistanceSquared) != Bits(other.DistanceSquared))
            {
                return $"A {a.Id} ({a.Position.x:R}, {a.Position.y:R}) d2 {a.DistanceSquared:R} vs " +
                       $"{other.Id} ({other.Position.x:R}, {other.Position.y:R}) d2 {other.DistanceSquared:R}";
            }

            return string.Empty;
        }

        internal static MonsterSnapshot Monster(int index, int generation, float x, float y, float radius, bool isLarge = false)
        {
            return new MonsterSnapshot { Id = new EntityHandle(index, generation), Position = new Vector2(x, y), Radius = radius, IsLarge = isLarge };
        }

        private static int Bits(float value)
        {
            return BitConverter.SingleToInt32Bits(value);
        }
    }
}
