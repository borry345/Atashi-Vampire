using System;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.EditMode.Spatial
{
    /// Shared helpers for spatial tests: snapshot filling, index rebuild and bitwise hit comparison.
    internal static class SpatialTestUtil
    {
        /// Appends a monster with index id (generation 1) to snapshot.
        public static void Add(MonsterSnapshotBuffer snapshot, int id, float x, float y, float radius = 0.3f, bool isLarge = false)
        {
            snapshot.Add(new MonsterSnapshot
            {
                Id = new EntityHandle(id, 1),
                Position = new Vector2(x, y),
                Radius = radius,
                IsLarge = isLarge,
            });
        }

        /// Runs BuildIndex on index with Required = true.
        public static void Rebuild(MonsterSpatialIndex index)
        {
            index.Required = true;
            index.OnPhase(SimulationPhase.BuildIndex, default);
        }

        /// Empty string when both results are bitwise identical, otherwise a description of the difference.
        public static string Difference(bool foundA, in TargetHit a, bool foundB, in TargetHit b)
        {
            if (foundA != foundB)
            {
                return $"found differs: A={foundA} B={foundB}";
            }

            if (!foundA)
            {
                return string.Empty;
            }

            bool same = a.Id == b.Id
                && Bits(a.Position.x) == Bits(b.Position.x)
                && Bits(a.Position.y) == Bits(b.Position.y)
                && Bits(a.DistanceSquared) == Bits(b.DistanceSquared);
            if (same)
            {
                return string.Empty;
            }

            return $"A=({a.Id}, ({a.Position.x:R}, {a.Position.y:R}), {a.DistanceSquared:R}) "
                 + $"B=({b.Id}, ({b.Position.x:R}, {b.Position.y:R}), {b.DistanceSquared:R})";
        }

        /// Raw IEEE 754 bits of value.
        public static int Bits(float value)
        {
            return BitConverter.SingleToInt32Bits(value);
        }
    }
}
