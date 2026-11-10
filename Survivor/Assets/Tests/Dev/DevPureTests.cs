using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Dev;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.Dev
{
    /// Plain C# checks of the dev parts that need no scene: activation, auto path geometry, spawn policy output, R26, time
    /// scale clamp / maximumDeltaTime and the status formatter (TAC-F19-1, 5.5, 5.15, 5.15.1, TAC-F19-6, TAC-F19-12).
    public sealed class DevPureTests
    {
        private const float Tolerance = 1e-6f;

        // Hangul display names as escapes (file encoding independent): "투사체", "연사 투사체", "공격 속도".
        private const string W1Name = "투사체";
        private const string E1Name = "연사 투사체";
        private const string P1Name = "공격 속도";

        [TestCase("--dev")]
        [TestCase("--DEV")]
        [TestCase("--Dev")]
        public void HasDevArgument_IgnoresCase(string argument)
        {
            Assert.IsTrue(DevModeActivation.HasDevArgument(new[] { "Survivor.exe", argument }));
            Assert.IsTrue(DevModeActivation.IsRequested(new[] { "Survivor.exe", argument }));
        }

        [Test]
        public void HasDevArgument_FalseWithoutTheExactSwitch()
        {
            Assert.IsFalse(DevModeActivation.HasDevArgument(null));
            Assert.IsFalse(DevModeActivation.HasDevArgument(new string[0]));
            Assert.IsFalse(DevModeActivation.HasDevArgument(new[] { "-dev", "--developer", "dev", "--dev=1" }));
        }

        [Test]
        public void AutoPath_TargetFollowsTheFormula()
        {
            var path = new AutoPathInputSource(new Vector2(1f, 2f), 6f, 4f, 0.5f);
            for (long s = 0; s < 1000; s += 37)
            {
                double phi = 0.5 + 4.0 / 6.0 * (s + 1) / 60.0;
                Vector2 expected = new Vector2(1f + (float)(6 * Math.Cos(phi)), 2f + (float)(6 * Math.Sin(phi)));
                Vector2 actual = path.TargetAt(s);
                Assert.AreEqual(expected.x, actual.x, 1e-5f, "x at step " + s);
                Assert.AreEqual(expected.y, actual.y, 1e-5f, "y at step " + s);
            }
        }

        [Test]
        public void AutoPath_MoveIsUnitDirectionWithCappedSpeed()
        {
            var path = new AutoPathInputSource(Vector2.zero, 6f, 4f, 0f);
            Vector2 target = path.TargetAt(10);

            MoveCommand far = path.ReadMove(10, target + new Vector2(-30f, 0f));
            Assert.AreEqual(1f, far.Direction.magnitude, 1e-5f);
            Assert.AreEqual(4f, far.SpeedOverride, Tolerance, "far: full speed");

            MoveCommand near = path.ReadMove(10, target + new Vector2(0f, 0.01f));
            Assert.AreEqual(0.01f * 60f, near.SpeedOverride, 1e-4f, "near: distance x 60 so the step lands on the target");
            Assert.AreEqual(-1f, near.Direction.y, 1e-4f);

            MoveCommand there = path.ReadMove(10, target);
            Assert.AreEqual(Vector2.zero, there.Direction);
            Assert.AreEqual(0f, there.SpeedOverride);
        }

        [Test]
        public void AutoPath_PlayerStartingOnTheCircleStaysOnIt()
        {
            // DevController's layout: center (-r, 0), start angle 0, the player spawns at the origin (on the circle).
            var path = new AutoPathInputSource(new Vector2(-6f, 0f), 6f, 4f, 0f);
            Vector2 position = Vector2.zero;
            for (long s = 0; s < 3600; s++)
            {
                MoveCommand move = path.ReadMove(s, position);
                position += move.Direction * StepMath.PerSecondToPerStep(move.SpeedOverride);
                Assert.AreEqual(6f, (position - new Vector2(-6f, 0f)).magnitude, 1e-3f, "radius at step " + s);
            }
        }

        [Test]
        public void AutoPath_RejectsNonPositiveRadiusOrSpeed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoPathInputSource(Vector2.zero, 0f, 4f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoPathInputSource(Vector2.zero, 6f, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutoPathInputSource(Vector2.zero, float.PositiveInfinity, 4f, 0f));
        }

        [Test]
        public void SpawnPolicy_StopSpawnsAsksNothingFromTheTable()
        {
            var table = new FixedTable("M1", "M2");
            var policy = new DevSpawnPolicy(table, true, null, 0, null);
            var ids = new List<string>();
            policy.CollectSpawns(default, 0, null, ids);
            CollectionAssert.IsEmpty(ids);
            Assert.AreEqual(0, table.Calls);
            Assert.IsFalse(policy.BossSpawnDue(GameRules.BossSpawnStep));
        }

        [Test]
        public void SpawnPolicy_OnlySpeciesRenamesTableSpawns()
        {
            var policy = new DevSpawnPolicy(new FixedTable("M1", "M2", "M3"), false, "M2", 0, null);
            var ids = new List<string> { "kept" };
            policy.CollectSpawns(default, 10, null, ids);
            CollectionAssert.AreEqual(new[] { "kept", "M2", "M2", "M2" }, ids);
        }

        [Test]
        public void SpawnPolicy_HoldTopsUpToN()
        {
            var hold = new DevSpawnPolicy(null, true, null, 100, "M1");
            var ids = new List<string>();
            hold.CollectSpawns(default, 95, null, ids);
            Assert.AreEqual(5, ids.Count);
            CollectionAssert.AreEqual(new[] { "M1", "M1", "M1", "M1", "M1" }, ids);

            ids.Clear();
            hold.CollectSpawns(default, 100, null, ids);
            CollectionAssert.IsEmpty(ids, "already at N");
            hold.CollectSpawns(default, 130, null, ids);
            CollectionAssert.IsEmpty(ids, "above N");

            var withTable = new DevSpawnPolicy(new FixedTable("M2", "M3"), false, null, 10, "M1");
            ids.Clear();
            withTable.CollectSpawns(default, 5, null, ids);
            CollectionAssert.AreEqual(new[] { "M2", "M3", "M1", "M1", "M1" }, ids, "table spawns count toward the hold");
        }

        [Test]
        public void SpawnPolicy_RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DevSpawnPolicy(null, true, null, -1, "M1"));
            Assert.Throws<ArgumentNullException>(() => new DevSpawnPolicy(null, false, null, 0, null));
            Assert.Throws<ArgumentException>(() => new DevSpawnPolicy(null, true, null, 5, null));
        }

        [TestCase(0, 45f)]
        [TestCase(100, 45f)]
        [TestCase(400, 45f)]
        [TestCase(1600, 90f)]
        public void DespawnDistance_FollowsR26(int n, float expected)
        {
            Assert.AreEqual(expected, SpawnGeometry.DespawnDistanceFor(n), 1e-4f);
        }

        [Test]
        public void TimeScale_ClampAndMaximumDeltaTime()
        {
            Assert.AreEqual(3f, DevController.ClampTimeScale(5f));
            Assert.AreEqual(0.25f, DevController.ClampTimeScale(0.1f));
            Assert.AreEqual(1.5f, DevController.ClampTimeScale(1.5f));
            Assert.AreEqual(1f, DevController.ClampTimeScale(float.NaN));
            Assert.That(Math.Abs(DevController.MaxDeltaTimeFor(3f) - 0.05f / 3f), Is.LessThanOrEqualTo(Tolerance));
            Assert.That(Math.Abs(DevController.MaxDeltaTimeFor(1f) - 0.05f), Is.LessThanOrEqualTo(Tolerance));
            Assert.That(Math.Abs(DevController.MaxDeltaTimeFor(0.25f) - 0.2f), Is.LessThanOrEqualTo(Tolerance));
        }

        [Test]
        public void Formatter_LinesInTableOrder()
        {
            var lines = new List<string> { "stale" };
            DevStatusInput input = Input(hpMultiplier: null);
            DevStatusFormatter.Format(in input, lines);
            Assert.AreEqual("Time 01:05  Step 3900", lines[0]);
            Assert.AreEqual("Seed 1234567890 (fixed)", lines[1]);
            Assert.AreEqual("Monsters 312  Gems 85", lines[2]);
            Assert.AreEqual("Pool CustomPool  Target SpatialHash  Tick CentralTick  Collision SelfSquaredDistance  CCD Discrete  Cell 2.00", lines[3]);
            Assert.AreEqual("Speed x1.00 (actual x0.98)", lines[4]);
            Assert.AreEqual(DevStatusFormatter.HpMultiplierUnavailable, lines[5]);
            Assert.AreEqual("HP mult: n/a", lines[5]);
            Assert.AreEqual("State Playing  Gate open", lines[6]);
            Assert.AreEqual("Weapons:", lines[7]);
            Assert.AreEqual("  (none)", lines[8]);
            Assert.AreEqual("Passives:", lines[9]);
            Assert.AreEqual("  (none)", lines[10]);
            Assert.AreEqual(11, lines.Count, "stale content cleared");
            CollectionAssert.DoesNotContain(lines, "stale");
        }

        [Test]
        public void Formatter_HpMultiplierRandomSeedAndClosedGate()
        {
            var lines = new List<string>();
            DevStatusInput input = Input(hpMultiplier: 1.5f, seedFixed: false, gateRunning: false);
            DevStatusFormatter.Format(in input, lines);
            Assert.AreEqual("HP mult: x1.5", lines[5]);
            Assert.AreEqual("Seed 1234567890 (random)", lines[1]);
            Assert.AreEqual("State Playing  Gate closed (SingleStepDone)", lines[6]);
        }

        [Test]
        public void Formatter_UsesInvariantDigitsUnderGermanCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var lines = new List<string>();
                DevStatusInput input = Input(hpMultiplier: 2.5f, timeScale: 1.5f, actualSpeed: 0.75f, cellSize: 2.5f);
                DevStatusFormatter.Format(in input, lines);
                Assert.AreEqual("Speed x1.50 (actual x0.75)", lines[4]);
                Assert.AreEqual("HP mult: x2.5", lines[5], "one decimal, point separator");
                StringAssert.Contains("Cell 2.50", lines[3]);
                foreach (string line in lines)
                {
                    StringAssert.DoesNotContain(",", line, "no comma decimal separator: " + line);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void Formatter_FixedLabelsHaveNoHangul()
        {
            var lines = new List<string>();
            DevStatusInput input = Input(hpMultiplier: null);
            DevStatusFormatter.Format(in input, lines);
            foreach (string line in lines)
            {
                foreach (char ch in line)
                {
                    Assert.IsFalse(ch >= '가' && ch <= '힣', "Hangul in a fixed label: " + line);
                }
            }
        }

        [Test]
        public void Formatter_WeaponAndPassiveLinesKeepDisplayNames()
        {
            Assert.AreEqual("  " + W1Name + " Lv 3", DevStatusFormatter.WeaponLine(W1Name, "W1", 3, false));
            Assert.AreEqual("  " + E1Name + " (evolved)", DevStatusFormatter.WeaponLine(E1Name, "E1", 1, true));
            Assert.AreEqual("  W2 Lv 2", DevStatusFormatter.WeaponLine(null, "W2", 2, false), "id when the name is missing");
            Assert.AreEqual("  " + P1Name + " Lv 4", DevStatusFormatter.PassiveLine(P1Name, "P1", 4));
        }

        [Test]
        public void Formatter_NullListThrows()
        {
            DevStatusInput input = Input(hpMultiplier: null);
            Assert.Throws<ArgumentNullException>(() => DevStatusFormatter.Format(in input, null));
        }

        [Test]
        public void OverlayFont_FactoryFailureFallsBackToNull()
        {
            Assert.IsNull(DevOverlay.CreateOverlayFont(() => throw new InvalidOperationException("no OS fonts")));
            Assert.IsNull(DevOverlay.CreateOverlayFont(() => null));
            Assert.IsNull(DevOverlay.CreateOverlayFont(null));
        }

        private static DevStatusInput Input(float? hpMultiplier, bool seedFixed = true, bool gateRunning = true, float timeScale = 1f,
                                            float actualSpeed = 0.98f, float cellSize = 2f)
        {
            return new DevStatusInput(3900, 1234567890UL, seedFixed, 312, 85, PoolStrategyKind.CustomPool, TargetProviderKind.SpatialHash,
                TickStrategyKind.CentralTick, CollisionStrategyKind.SelfSquaredDistance, CollisionDetectionMode2D.Discrete, cellSize,
                timeScale, actualSpeed, hpMultiplier, FlowState.Playing, gateRunning, GateCloseReason.SingleStepDone, null, null);
        }

        // Table stand-in: appends the same ids every call and counts the calls.
        private sealed class FixedTable : ISpawnPolicy
        {
            private readonly string[] _ids;

            internal FixedTable(params string[] ids)
            {
                _ids = ids;
            }

            internal int Calls { get; private set; }

            public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
            {
                Calls++;
                monsterIdsOut.AddRange(_ids);
            }

            public bool BossSpawnDue(long step)
            {
                return true;
            }
        }
    }
}
