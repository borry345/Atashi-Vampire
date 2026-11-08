using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Target provider C: Physics2D.OverlapCircle over the monster colliders, then the exact choice on snapshot positions (TAC-F16-3).
    /// Returns exactly what BruteForceTargetProvider returns (TAC-F16-1); needs MonsterColliders and SyncMonstersAtBuildIndex (R19).
    public sealed class Physics2DTargetProvider : ITargetProvider
    {
        private const float InitialRadius = 4f;

        // Float headroom on the re-query radius; the collider radius already makes the overlap a superset of "center distance <= R".
        private const float RequeryMargin = 1e-3f;

        private readonly PhysicsBodySynchronizer _bodies;
        private readonly List<Collider2D> _results = new List<Collider2D>();
        private ContactFilter2D _filter;
        private MonsterSnapshotBuffer _snapshot;

        /// Creates a provider over the monster bodies of bodies; throws ArgumentNullException when bodies is null and
        /// ArgumentOutOfRangeException when monsterLayerMask is 0.
        public Physics2DTargetProvider(PhysicsBodySynchronizer bodies, int monsterLayerMask)
        {
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            if (monsterLayerMask == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monsterLayerMask), monsterLayerMask, "Monster layer mask must not be empty.");
            }

            _filter = new ContactFilter2D();
            _filter.SetLayerMask(monsterLayerMask);
            _filter.useTriggers = true;
        }

        /// TargetProviderKind.Physics2DQuery.
        public TargetProviderKind Kind => TargetProviderKind.Physics2DQuery;

        /// Keeps the frozen snapshot of this step for TryFindNearest; null forgets it. Does not sync bodies (the synchronizer does, order 5).
        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
            _snapshot = snapshot;
        }

        /// Nearest monster to point by squared center distance on snapshot positions, ties by smaller Id (R6); false when there is
        /// no monster, no snapshot yet or no enabled monster collider. Never throws.
        public bool TryFindNearest(Vector2 point, out TargetHit hit)
        {
            using (ProfilerMarkers.Targeting.Auto())
            {
                hit = default;
                MonsterSnapshotBuffer snapshot = _snapshot;
                if (snapshot == null || snapshot.Count == 0)
                {
                    return false;
                }

                float limit = FarthestCornerDistance(snapshot.Bounds, point);
                if (float.IsNaN(limit) || float.IsInfinity(limit))
                {
                    return false;
                }

                // Widen from InitialRadius by doubling up to the farthest snapshot corner until some monster collider shows up.
                var best = new Best();
                float radius = InitialRadius;
                while (true)
                {
                    Query(point, radius);
                    Consider(snapshot, point, ref best);
                    if (best.Found)
                    {
                        break;
                    }

                    if (radius >= limit)
                    {
                        return false;
                    }

                    radius = Mathf.Min(radius * 2f, limit);
                }

                // Every monster whose center lies within sqrt(bestD2) overlaps this circle, so this pass sees every equal-or-closer
                // candidate. R6 is a strict total order, so the choice does not depend on OverlapCircle's (unspecified) result order.
                Query(point, Mathf.Sqrt(best.DistanceSquared) + RequeryMargin);
                Consider(snapshot, point, ref best);

                hit = new TargetHit(best.Id, best.Position, best.DistanceSquared);
                return true;
            }
        }

        private void Query(Vector2 point, float radius)
        {
            Physics2D.OverlapCircle(point, radius, _filter, _results);
        }

        // Same squared distance expression and IsBetter call as BruteForceTargetProvider, on snapshot positions (TAC-F16-1).
        private void Consider(MonsterSnapshotBuffer snapshot, Vector2 point, ref Best best)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (!_bodies.TryGetBody(_results[i], out CollisionBody body) || !snapshot.TryGetIndex(body.Id, out int index))
                {
                    continue;
                }

                ref readonly MonsterSnapshot entry = ref snapshot[index];
                float d2 = (entry.Position - point).sqrMagnitude;
                if (!best.Found || TargetOrdering.IsBetter(d2, entry.Id, best.DistanceSquared, best.Id))
                {
                    best.Found = true;
                    best.DistanceSquared = d2;
                    best.Id = entry.Id;
                    best.Position = entry.Position;
                }
            }

            _results.Clear();
        }

        private static float FarthestCornerDistance(Rect bounds, Vector2 point)
        {
            float dx = Mathf.Max(Mathf.Abs(bounds.xMin - point.x), Mathf.Abs(bounds.xMax - point.x));
            float dy = Mathf.Max(Mathf.Abs(bounds.yMin - point.y), Mathf.Abs(bounds.yMax - point.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private struct Best
        {
            public bool Found;
            public float DistanceSquared;
            public EntityHandle Id;
            public Vector2 Position;
        }
    }
}
