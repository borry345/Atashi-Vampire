using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// The 16 measurement presets plus the A0 sweep (TAC-F10-4, SPEC F10 measurement design): A1..A6 (all off, one strategy
    /// on, all on), B1..B8 (N 50/100/200/400 x brute force / spatial hash), C1..C2 (one engine alternative, the rest baseline).
    /// Every preset has the fixed loadout W1:5;W2:5;W3:5;W4:5 and a chest drop chance of 0; D follows R26 for the chosen N.
    public static class MeasurementPresets
    {
        /// Default N of the A0 sweep (the user raises it run by run).
        public const int A0DefaultN = 400;

        /// Default N* of A and C (the user sets it after A0).
        public const int StarDefaultN = 200;

        /// Fixed loadout text of every preset (PM default).
        public const string FixedLoadoutText = "W1:5;W2:5;W3:5;W4:5";

        /// Fixed master seed of every measured run (one seed for all 16 runs, SPEC F10).
        public const ulong Seed = 4242UL;

        /// Auto path radius / speed of every measured run (the dev determinism settings).
        public const float AutoPathRadius = 6f, AutoPathSpeed = 4f;

        /// Fixed loadout of every preset as SetLoadout entries.
        public static readonly IReadOnlyList<WeaponLoadoutEntry> FixedLoadout = new[]
        {
            new WeaponLoadoutEntry("W1", 5), new WeaponLoadoutEntry("W2", 5), new WeaponLoadoutEntry("W3", 5), new WeaponLoadoutEntry("W4", 5),
        };

        /// A0, A1..A6, B1..B8, C1..C2 in that order.
        public static readonly IReadOnlyList<MeasurementPreset> All = Build();

        /// Preset with label (ordinal), or null.
        public static MeasurementPreset Find(string label)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Label, label, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }

            return null;
        }

        /// R26 despawn distance for n (SpawnGeometry.DespawnDistanceFor).
        public static float DespawnDistanceFor(int n)
        {
            return SpawnGeometry.DespawnDistanceFor(n);
        }

        /// The formula R26 used for n: "45" (n <= 400) or "45*sqrt(N/400)".
        public static string DespawnFormulaFor(int n)
        {
            return n <= GameRules.MonsterCapDefault ? "45" : "45*sqrt(N/400)";
        }

        private static IReadOnlyList<MeasurementPreset> Build()
        {
            StrategySelection baseline = StrategySelection.Baseline;
            var list = new List<MeasurementPreset>
            {
                Make("A0", "A0", baseline, A0DefaultN, true),
                Make("A1", "A", baseline, StarDefaultN, true),
                Make("A2", "A", With(baseline, pool: PoolStrategyKind.CustomPool), StarDefaultN, true),
                Make("A3", "A", With(baseline, target: TargetProviderKind.SpatialHash), StarDefaultN, true),
                Make("A4", "A", With(baseline, tick: TickStrategyKind.CentralTick), StarDefaultN, true),
                Make("A5", "A", With(baseline, collision: CollisionStrategyKind.SelfSquaredDistance), StarDefaultN, true),
                Make("A6", "A", StrategySelection.ShippingDefault, StarDefaultN, true),
            };

            int[] counts = { 50, 100, 200, 400 };
            int index = 1;
            foreach (TargetProviderKind target in new[] { TargetProviderKind.BruteForce, TargetProviderKind.SpatialHash })
            {
                foreach (int n in counts)
                {
                    list.Add(Make("B" + index, "B", With(baseline, target: target), n, false));
                    index++;
                }
            }

            list.Add(Make("C1", "C", With(baseline, pool: PoolStrategyKind.UnityObjectPool), StarDefaultN, true));
            list.Add(Make("C2", "C", With(baseline, target: TargetProviderKind.Physics2DQuery), StarDefaultN, true));
            return list;
        }

        private static MeasurementPreset Make(string label, string group, StrategySelection selection, int n, bool editable)
        {
            return new MeasurementPreset(label, group, selection, n, editable, FixedLoadout, FixedLoadoutText, 0f);
        }

        private static StrategySelection With(StrategySelection s, PoolStrategyKind? pool = null, TargetProviderKind? target = null,
                                              TickStrategyKind? tick = null, CollisionStrategyKind? collision = null)
        {
            return new StrategySelection(pool ?? s.Pool, target ?? s.Target, tick ?? s.Tick, collision ?? s.Collision,
                s.EngineDetectionMode, s.SpatialHashCellSize);
        }
    }
}
