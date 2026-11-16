using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Dev;
using UnityEngine;

namespace Survivor.Tests.Dev
{
    /// Plain C# checks of the wave 5b parts that need no scene: R28 (CsvWriter), R27 and the frame statistics, the presets
    /// (TAC-F10-4), the start guard (5.15.2 step 2), the measurement file layout from an in-memory record (TAC-F10-3, 5.15.3,
    /// TAC-F19-14 header), the collision log rows and the logging decorator.
    public sealed class MeasurementPureTests
    {
        // "해당 없음" / "미도달" as escapes so the check does not depend on this file's encoding.
        private const string NotApplicableText = "해당 없음";
        private const string NotReachedText = "미도달";

        private static readonly string[] ExpectedHeaderKeys =
        {
            "schema", "timestampUtc", "label", "group", "buildType", "unityVersion", "cpu", "gpu", "ramMB", "os",
            "vSyncCount", "targetFrameRate", "scriptingBackend", "incrementalGC", "pool", "target", "tick", "collision", "ccdMode",
            "monsterCountN", "monsterCapDisabled", "despawnDistance", "despawnFormula", "seed", "hpMultipliers", "cellSize",
            "stepSeconds", "durationSteps", "warmupSteps", "nReachedStep", "playerInvulnerable", "weaponLoadout", "chestDropChance",
            "spawnStopHoldN", "levelUpEnabled", "autoPathRadius", "autoPathSpeed", "timeScale", "overlayHidden", "visualizersOff",
            "collisionLogOff", "valid", "invalidReason",
        };

        private static readonly string[] ExpectedBodyColumns =
        {
            "frames", "steps", "slowmoFrames", "avgFps", "onePercentLowFps", "worstFrameMs", "gcAllocPerFrameBytes",
            "monsterCountLimit", "monsterCountAvg", "monsterCountMax",
            "step_msPerStep", "step_msPerFrame", "pool_msPerStep", "pool_msPerFrame", "targeting_msPerStep", "targeting_msPerFrame",
            "spatialIndex_msPerStep", "spatialIndex_msPerFrame", "tickMonsters_msPerStep", "tickMonsters_msPerFrame",
            "tickProjectiles_msPerStep", "tickProjectiles_msPerFrame", "collision_msPerStep", "collision_msPerFrame",
            "memTotalUsedMB_avg", "memTotalUsedMB_max", "memTotalReservedMB_avg", "memTotalReservedMB_max", "activeGems_avg",
            "activeGems_max",
        };

        private CultureInfo _culture;
        private string _tempRoot;

        [SetUp]
        public void UseCommaDecimalCulture()
        {
            // R28: output must not follow the machine culture; de-DE writes 1,5 for 1.5.
            _culture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            _tempRoot = Path.Combine(Path.GetTempPath(), "SurvivorDevTests_" + TestContext.CurrentContext.Test.MethodName);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [TearDown]
        public void RestoreCultureAndDeleteFiles()
        {
            Thread.CurrentThread.CurrentCulture = _culture;
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void CsvWriter_EscapesAndFormatsInvariant()
        {
            Assert.AreEqual("plain", CsvWriter.Escape("plain"));
            Assert.AreEqual(string.Empty, CsvWriter.Escape(null));
            Assert.AreEqual("\"a,b\"", CsvWriter.Escape("a,b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", CsvWriter.Escape("say \"hi\""));
            Assert.AreEqual("\"two\nlines\"", CsvWriter.Escape("two\nlines"));
            Assert.AreEqual("\"cr\rhere\"", CsvWriter.Escape("cr\rhere"));
            Assert.AreEqual("1.5", CsvWriter.Number(1.5d));
            Assert.AreEqual("0.1", CsvWriter.Number(0.1d), "R round-trips");
            Assert.AreEqual("2.25", CsvWriter.Number(2.25f));
            Assert.AreEqual("1234567", CsvWriter.Number(1234567L), "no group separator");
            Assert.AreEqual("1.50", CsvWriter.Number(1.5d, "0.00"));
            Assert.AreEqual("NA", CsvWriter.Number((double?)null));
            Assert.AreEqual("true", CsvWriter.Bool(true));
            Assert.AreEqual("false", CsvWriter.Bool(false));
            var builder = new StringBuilder();
            CsvWriter.AppendRow(builder, new[] { "a", "b,c", "" });
            Assert.AreEqual("a,\"b,c\",\r\n", builder.ToString());
        }

        [Test]
        public void CsvWriter_BomOnlyOnNewFiles_CrLfLines()
        {
            string path = Path.Combine(_tempRoot, "sub", "x.csv");
            CsvWriter.AppendToFile(path, "h1,h2\r\n", "1,2\r\n");
            CsvWriter.AppendToFile(path, "h1,h2\r\n", "3,4\r\n");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, new[] { bytes[0], bytes[1], bytes[2] }, "BOM on the new file");
            Assert.AreEqual(1, CountBom(bytes), "no BOM when appending");
            Assert.AreEqual("h1,h2\r\n1,2\r\n3,4\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "header row once");
            string fresh = Path.Combine(_tempRoot, "y.csv");
            CsvWriter.WriteNewFile(fresh, "a\r\n");
            CsvWriter.WriteNewFile(fresh, "b\r\n");
            Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'b', 13, 10 }, File.ReadAllBytes(fresh), "new file replaces, with BOM");
        }

        [Test]
        public void R27_SessionSumsOverCounts_ZeroDenominatorIsNa()
        {
            Assert.AreEqual(2.5d, MeasurementMath.PerStepMs(10d, 4));
            Assert.AreEqual(3d, MeasurementMath.PerFrameMs(9d, 3));
            Assert.IsNull(MeasurementMath.PerStepMs(10d, 0));
            Assert.IsNull(MeasurementMath.PerFrameMs(10d, 0));
            Assert.AreEqual("NA", CsvWriter.Number(MeasurementMath.PerStepMs(10d, 0)));
            Assert.AreEqual(60d, MeasurementMath.AverageFps(120, 2d));
            Assert.IsNull(MeasurementMath.AverageFps(0, 0d));

            // Frame A: 1 step, 2 ms; frame B: 3 steps, 3 ms. R27 = 5 / 4 = 1.25 per step (the ratio mean would be 1.5).
            var frames = new[] { Frame(0, 16d, 1, 2d), Frame(1, 16d, 3, 3d) };
            MeasurementStats stats = MeasurementStats.FromFrames(frames);
            Assert.AreEqual(4L, stats.Steps);
            Assert.AreEqual(2L, stats.Frames);
            Assert.AreEqual(1L, stats.SlowmoFrames, "frames with 3 steps");
            Assert.AreEqual(1.25d, stats.MarkerPerStepMs[0]);
            Assert.AreEqual(2.5d, stats.MarkerPerFrameMs[0]);
            Assert.IsNull(MeasurementStats.FromFrames(new MeasurementFrame[0]).MarkerPerStepMs[0], "no frames -> NA");
        }

        [Test]
        public void FrameStats_OnePercentLowWorstFrameAndNaChannels()
        {
            var ms = new List<double>();
            for (int i = 0; i < 198; i++)
            {
                ms.Add(10d);
            }

            ms.Add(50d);
            ms.Add(40d);
            Assert.AreEqual(2, MeasurementMath.SlowestCount(200));
            Assert.AreEqual(1, MeasurementMath.SlowestCount(100));
            Assert.AreEqual(2, MeasurementMath.SlowestCount(101));
            Assert.AreEqual(1, MeasurementMath.SlowestCount(1));
            Assert.AreEqual(0, MeasurementMath.SlowestCount(0));
            Assert.AreEqual(1000d / 45d, MeasurementMath.OnePercentLowFps(ms).Value, 1e-9);
            Assert.AreEqual(50d, MeasurementMath.WorstFrameMs(ms));
            Assert.IsNull(MeasurementMath.OnePercentLowFps(new double[0]));
            Assert.IsNull(MeasurementMath.WorstFrameMs(new double[0]));

            var markers = new double[StatsCollector.MarkerCount];
            markers[1] = double.NaN;
            var frames = new[]
            {
                new MeasurementFrame(0, 20d, 1, 100d, 2d * 1048576d, double.NaN, 10, 3, markers),
                new MeasurementFrame(1, 20d, 1, 300d, 4d * 1048576d, double.NaN, 20, 5, markers),
            };
            MeasurementStats stats = MeasurementStats.FromFrames(frames);
            Assert.AreEqual(50d, stats.AvgFps.Value, 1e-9, "2 frames / 0.04 s");
            Assert.AreEqual(200d, stats.GcAllocPerFrameBytes);
            Assert.AreEqual(3d, stats.MemUsedAvgMB);
            Assert.AreEqual(4d, stats.MemUsedMaxMB);
            Assert.IsNull(stats.MemReservedAvgMB, "unavailable counter -> NA");
            Assert.IsNull(stats.MemReservedMaxMB);
            Assert.AreEqual(0d, stats.MarkerPerStepMs[0], "available marker with 0 ms stays a number");
            Assert.IsNull(stats.MarkerPerStepMs[1], "unavailable marker -> NA");
            Assert.AreEqual(15d, stats.MonsterCountAvg);
            Assert.AreEqual(20d, stats.MonsterCountMax);
            Assert.AreEqual(4d, stats.ActiveGemsAvg);
            Assert.AreEqual(5d, stats.ActiveGemsMax);
        }

        [Test]
        public void Presets_SixteenPlusA0_WithFixedLoadoutAndNoChests()
        {
            IReadOnlyList<MeasurementPreset> all = MeasurementPresets.All;
            var labels = new List<string>();
            foreach (MeasurementPreset p in all)
            {
                labels.Add(p.Label);
            }

            CollectionAssert.AreEqual(new[] { "A0", "A1", "A2", "A3", "A4", "A5", "A6", "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "C1", "C2" }, labels);
            StrategySelection baseline = StrategySelection.Baseline;
            AssertSelection(baseline, Get("A0").Selection);
            AssertSelection(baseline, Get("A1").Selection);
            AssertSelection(With(baseline, pool: PoolStrategyKind.CustomPool), Get("A2").Selection);
            AssertSelection(With(baseline, target: TargetProviderKind.SpatialHash), Get("A3").Selection);
            AssertSelection(With(baseline, tick: TickStrategyKind.CentralTick), Get("A4").Selection);
            AssertSelection(With(baseline, collision: CollisionStrategyKind.SelfSquaredDistance), Get("A5").Selection);
            AssertSelection(StrategySelection.ShippingDefault, Get("A6").Selection);
            int[] n = { 50, 100, 200, 400 };
            for (int i = 0; i < 4; i++)
            {
                MeasurementPreset brute = Get("B" + (i + 1));
                MeasurementPreset hash = Get("B" + (i + 5));
                AssertSelection(baseline, brute.Selection);
                AssertSelection(With(baseline, target: TargetProviderKind.SpatialHash), hash.Selection);
                Assert.AreEqual(n[i], brute.DefaultN);
                Assert.AreEqual(n[i], hash.DefaultN);
                Assert.IsFalse(brute.NEditable);
                Assert.AreEqual(n[i], brute.ResolveN(999), "B keeps its table N");
            }

            AssertSelection(With(baseline, pool: PoolStrategyKind.UnityObjectPool), Get("C1").Selection);
            AssertSelection(With(baseline, target: TargetProviderKind.Physics2DQuery), Get("C2").Selection);
            Assert.AreEqual(400, Get("A0").DefaultN);
            Assert.AreEqual(200, Get("A3").DefaultN);
            Assert.AreEqual(300, Get("C1").ResolveN(300), "A0 / A / C take the user's N");
            Assert.AreEqual(200, Get("A1").ResolveN(0), "no N -> default");
            foreach (MeasurementPreset p in all)
            {
                Assert.AreEqual(0f, p.ChestDropChance, p.Label);
                Assert.AreEqual("W1:5;W2:5;W3:5;W4:5", p.WeaponLoadoutText, p.Label);
                Assert.AreEqual(4, p.WeaponLoadout.Count, p.Label);
                for (int w = 0; w < 4; w++)
                {
                    Assert.AreEqual("W" + (w + 1), p.WeaponLoadout[w].WeaponId, p.Label);
                    Assert.AreEqual(5, p.WeaponLoadout[w].Level, p.Label);
                }

                Assert.AreEqual(CollisionDetectionMode2D.Discrete, p.Selection.EngineDetectionMode, p.Label);
                Assert.IsNull(MeasurementGuard.Check(false, false, 1f, false, p), p.Label + " passes the guard");
            }

            Assert.AreEqual(45f, MeasurementPresets.DespawnDistanceFor(50));
            Assert.AreEqual(45f, MeasurementPresets.DespawnDistanceFor(400));
            Assert.AreEqual(67.5f, MeasurementPresets.DespawnDistanceFor(900), 1e-4f);
            Assert.AreEqual(SpawnGeometry.DespawnDistanceFor(1600), MeasurementPresets.DespawnDistanceFor(1600));
            Assert.AreEqual("45", MeasurementPresets.DespawnFormulaFor(400));
            Assert.AreEqual("45*sqrt(N/400)", MeasurementPresets.DespawnFormulaFor(401));
            Assert.IsNull(MeasurementPresets.Find("Z9"));
        }

        [Test]
        public void Guard_RefusesEachConditionWithItsCause()
        {
            MeasurementPreset ok = Get("A1");
            Assert.IsNull(MeasurementGuard.Check(false, false, 1f, false, ok));
            StringAssert.Contains("Visualization", MeasurementGuard.Check(true, false, 1f, false, ok));
            StringAssert.Contains("Collision log", MeasurementGuard.Check(false, true, 1f, false, ok));
            StringAssert.Contains("x2", MeasurementGuard.Check(false, false, 2f, false, ok));
            StringAssert.Contains("x0.25", MeasurementGuard.Check(false, false, 0.25f, false, ok));
            StringAssert.Contains("One-step", MeasurementGuard.Check(false, false, 1f, true, ok));
            var noLoadout = new MeasurementPreset("X", "A", StrategySelection.Baseline, 10, true, null, string.Empty, 0f);
            StringAssert.Contains("loadout", MeasurementGuard.Check(false, false, 1f, false, noLoadout));
            StringAssert.Contains("loadout", MeasurementGuard.Check(false, false, 1f, false, null));
            var chests = new MeasurementPreset("X", "A", StrategySelection.Baseline, 10, true, MeasurementPresets.FixedLoadout,
                MeasurementPresets.FixedLoadoutText, 0.02f);
            StringAssert.Contains("chest", MeasurementGuard.Check(false, false, 1f, false, chests));
            StringAssert.Contains("Visualization", MeasurementGuard.Check(true, true, 2f, true, chests), "first condition wins");
        }

        [Test]
        public void Exporter_HeaderKeysAndBodyColumns_FixedOrder()
        {
            CollectionAssert.AreEqual(ExpectedHeaderKeys, MeasurementCsvExporter.HeaderKeys);
            CollectionAssert.AreEqual(ExpectedBodyColumns, MeasurementCsvExporter.BodyColumns);
            Assert.AreEqual(43, MeasurementCsvExporter.HeaderKeys.Count);
            Assert.AreEqual(30, MeasurementCsvExporter.BodyColumns.Count);
            CollectionAssert.AreEqual(new[] { "frame", "unscaledDtMs", "steps", "gcBytes", "memUsedMB", "liveMonsters", "activeGems",
                "step_ms", "pool_ms", "targeting_ms", "spatialIndex_ms", "tickMonsters_ms", "tickProjectiles_ms", "collision_ms" },
                MeasurementCsvExporter.FrameColumns);
        }

        [Test]
        public void Exporter_MeasurementText_HeaderBlockEmptyRowBody()
        {
            MeasurementRecord record = SampleRecord();
            string text = MeasurementCsvExporter.BuildMeasurementText(record);
            Assert.IsFalse(text.Replace("\r\n", string.Empty).Contains("\n"), "every line ends in \\r\\n");
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.AreEqual(43 + 1 + 2 + 1, lines.Length, "43 header rows, empty row, column row, value row, trailing end");
            var header = new Dictionary<string, string>();
            for (int i = 0; i < 43; i++)
            {
                string[] kv = SplitCsv(lines[i]);
                Assert.AreEqual(2, kv.Length, lines[i]);
                Assert.AreEqual(ExpectedHeaderKeys[i], kv[0]);
                header[kv[0]] = kv[1];
            }

            Assert.AreEqual(string.Empty, lines[43], "empty row ends the header block");
            CollectionAssert.AreEqual(ExpectedBodyColumns, SplitCsv(lines[44]));
            string[] body = SplitCsv(lines[45]);
            Assert.AreEqual(30, body.Length);
            Assert.AreEqual("survivor-measurement-1", header["schema"]);
            Assert.AreEqual("2026-11-16T01:02:03Z", header["timestampUtc"]);
            Assert.AreEqual("A3", header["label"]);
            Assert.AreEqual("Intel(R), 8 cores", header["cpu"], "a comma in a value is quoted and read back");
            Assert.AreEqual("InstantiateDestroy", header["pool"]);
            Assert.AreEqual("SpatialHash", header["target"]);
            Assert.AreEqual("Discrete", header["ccdMode"]);
            Assert.AreEqual("67.5", header["despawnDistance"]);
            Assert.AreEqual("45*sqrt(N/400)", header["despawnFormula"]);
            Assert.AreEqual("1.0;1.5;2.2;3.0", header["hpMultipliers"]);
            Assert.AreEqual("2", header["cellSize"]);
            Assert.AreEqual("0.016666667", header["stepSeconds"]);
            Assert.AreEqual("300", header["warmupSteps"]);
            Assert.AreEqual("NA", header["nReachedStep"], "N never reached");
            Assert.AreEqual("W1:5;W2:5;W3:5;W4:5", header["weaponLoadout"]);
            Assert.AreEqual("0", header["chestDropChance"]);
            Assert.AreEqual("6", header["autoPathRadius"]);
            Assert.AreEqual("1", header["timeScale"]);
            Assert.AreEqual("false", header["valid"]);
            Assert.AreEqual("overlay opened (F1)", header["invalidReason"]);
            Assert.AreEqual("2", body[0], "frames");
            Assert.AreEqual(NotApplicableText, body[7], "monsterCountLimit outside A0");
            Assert.AreEqual("NA", body[Array.IndexOf(ExpectedBodyColumns, "memTotalReservedMB_avg")], "memTotalReservedMB_avg unavailable");

            record.Valid = true;
            Assert.AreEqual(string.Empty, SplitCsv(MeasurementCsvExporter.BuildMeasurementText(record).Split(new[] { "\r\n" }, StringSplitOptions.None)[42])[1],
                "invalidReason is empty for a valid session");
            Assert.AreEqual(73, SplitCsv(MeasurementCsvExporter.BuildSummaryColumnRow().TrimEnd('\r', '\n')).Length);
            Assert.AreEqual(73, SplitCsv(MeasurementCsvExporter.BuildSummaryRow(record).TrimEnd('\r', '\n')).Length);
        }

        [Test]
        public void Exporter_MonsterCountLimitAndHpMultipliers()
        {
            Assert.AreEqual(NotApplicableText, MeasurementCsvExporter.NotApplicable, "the source literal is the intended Hangul");
            Assert.AreEqual(NotReachedText, MeasurementCsvExporter.NotReached);
            Assert.AreEqual(NotApplicableText, MeasurementCsvExporter.MonsterCountLimit("A", 200, 30d));
            Assert.AreEqual(NotApplicableText, MeasurementCsvExporter.MonsterCountLimit("B", 50, 30d));
            Assert.AreEqual("800", MeasurementCsvExporter.MonsterCountLimit("A0", 800, 54.9d));
            Assert.AreEqual(NotReachedText, MeasurementCsvExporter.MonsterCountLimit("A0", 400, 55d));
            Assert.AreEqual("NA", MeasurementCsvExporter.MonsterCountLimit("A0", 400, null));
            Assert.AreEqual("1.0;1.5;2.2;3.0", MeasurementCsvExporter.FormatHpMultipliers(new[] { 1f, 1.5f, 2.2f, 3f }));
            Assert.AreEqual("2.25;1.0", MeasurementCsvExporter.FormatHpMultipliers(new[] { 2.25f, 1f }), "no truncation");
            Assert.AreEqual(string.Empty, MeasurementCsvExporter.FormatHpMultipliers(new float[0]), "no segment -> empty");
            Assert.AreEqual("NA", MeasurementCsvExporter.FormatHpMultipliers((Survivor.Data.SpawnTableDefinition)null));
        }

        [Test]
        public void Exporter_WritesThreeFiles_SummaryOnlyForValid()
        {
            var exporter = new MeasurementCsvExporter(_tempRoot);
            MeasurementRecord invalid = SampleRecord();
            var frames = new[] { Frame(0, 16d, 1, 0.5d), Frame(1, 17d, 2, 0.25d) };
            string path = exporter.Export(invalid, frames);
            Assert.AreEqual(Path.Combine(_tempRoot, "measurements", "measurement_20261116T010203Z.csv"), path);
            Assert.IsTrue(File.Exists(path));
            string framesPath = Path.Combine(_tempRoot, "measurements", "frames_20261116T010203Z.csv");
            Assert.IsTrue(File.Exists(framesPath));
            string summaryPath = Path.Combine(_tempRoot, "measurements", "summary.csv");
            Assert.IsFalse(File.Exists(summaryPath), "invalid sessions are not summarized");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(0xEF, bytes[0]);
            string[] frameLines = File.ReadAllText(framesPath, Encoding.UTF8).Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.AreEqual(4, frameLines.Length, "column row, 2 frames, trailing end");
            Assert.AreEqual("1,17,2,NA,4,10,3,0.25,0,0,0,0,0,0", frameLines[2]);

            MeasurementRecord valid = SampleRecord();
            valid.Valid = true;
            exporter.Export(valid, frames);
            valid.TimestampUtc = valid.TimestampUtc.AddSeconds(1);
            exporter.Export(valid, frames);
            byte[] summary = File.ReadAllBytes(summaryPath);
            Assert.AreEqual(1, CountBom(summary));
            string[] rows = Encoding.UTF8.GetString(summary, 3, summary.Length - 3).Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.AreEqual(4, rows.Length, "column row + 2 rows + trailing end");
            Assert.AreEqual("schema", SplitCsv(rows[0])[0]);
            Assert.AreEqual("frames", SplitCsv(rows[0])[43]);
            Assert.AreEqual("A3", SplitCsv(rows[1])[2]);
        }

        [Test]
        public void CollisionLog_RowsAreEventStrings_FileName()
        {
            Assert.AreEqual("collision_4242_SelfSquaredDistance_Discrete.csv",
                CollisionEventLog.FileName(4242UL, CollisionStrategyKind.SelfSquaredDistance, CollisionDetectionMode2D.Discrete));
            Assert.AreEqual("collision_18446744073709551615_EngineCollider2D_Continuous.csv",
                CollisionEventLog.FileName(ulong.MaxValue, CollisionStrategyKind.EngineCollider2D, CollisionDetectionMode2D.Continuous));
            var log = new CollisionEventLog();
            var a = new CollisionEvent(120, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster, new EntityHandle(1, 1), new EntityHandle(17, 3));
            var b = new CollisionEvent(121, CollisionEventKind.Exit, CollisionChannel.PlayerBodyVsMonster, new EntityHandle(1, 1), new EntityHandle(17, 3));
            log.Append(new[] { a, b });
            Assert.AreEqual("step,channel,a,b,kind\r\n120,PlayerBodyVsMonster,1:1,17:3,Enter\r\n121,PlayerBodyVsMonster,1:1,17:3,Exit\r\n",
                log.BuildText());
            string path = log.Export(_tempRoot, 7UL, CollisionStrategyKind.EngineCollider2D, CollisionDetectionMode2D.Discrete);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(1, CountBom(bytes));
            Assert.AreEqual(log.BuildText(), Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }

        [Test]
        public void LoggingStrategy_DelegatesKindAndCopiesEvents()
        {
            var inner = new FakeCollisionStrategy();
            var log = new CollisionEventLog();
            var logging = new LoggingCollisionStrategy(inner, log);
            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, logging.Kind, "Kind is the inner strategy's");
            inner.Next.Add(new CollisionEvent(5, CollisionEventKind.Enter, CollisionChannel.PlayerMagnetVsGem, new EntityHandle(1, 1), new EntityHandle(2, 1)));
            IReadOnlyList<CollisionEvent> events = logging.CollectEvents(5);
            Assert.AreSame(inner.Next, events, "the inner list is returned unchanged");
            Assert.AreEqual(1, log.Events.Count);
            inner.Next.Clear();
            Assert.AreEqual(1, log.Events.Count, "the log holds a copy");
            Assert.AreEqual(5L, inner.LastStep);
            logging.Clear();
            Assert.AreEqual(1, inner.Clears);
            Assert.Throws<ArgumentNullException>(() => new LoggingCollisionStrategy(null, log));
            Assert.Throws<ArgumentNullException>(() => new LoggingCollisionStrategy(inner, null));
        }

        private static MeasurementPreset Get(string label)
        {
            MeasurementPreset preset = MeasurementPresets.Find(label);
            Assert.IsNotNull(preset, label);
            return preset;
        }

        private static StrategySelection With(StrategySelection s, PoolStrategyKind? pool = null, TargetProviderKind? target = null,
                                              TickStrategyKind? tick = null, CollisionStrategyKind? collision = null)
        {
            return new StrategySelection(pool ?? s.Pool, target ?? s.Target, tick ?? s.Tick, collision ?? s.Collision);
        }

        private static void AssertSelection(StrategySelection expected, StrategySelection actual)
        {
            Assert.AreEqual(expected.Pool, actual.Pool);
            Assert.AreEqual(expected.Target, actual.Target);
            Assert.AreEqual(expected.Tick, actual.Tick);
            Assert.AreEqual(expected.Collision, actual.Collision);
            Assert.AreEqual(expected.SpatialHashCellSize, actual.SpatialHashCellSize);
        }

        private static MeasurementFrame Frame(long index, double dtMs, int steps, double stepMarkerMs)
        {
            var markers = new double[StatsCollector.MarkerCount];
            markers[0] = stepMarkerMs;
            return new MeasurementFrame(index, dtMs, steps, double.NaN, 4d * 1048576d, double.NaN, 10, 3, markers);
        }

        private static MeasurementRecord SampleRecord()
        {
            MeasurementPreset preset = MeasurementPresets.Find("A3");
            var markers = new double[StatsCollector.MarkerCount];
            return new MeasurementRecord
            {
                TimestampUtc = new DateTime(2026, 11, 16, 1, 2, 3, DateTimeKind.Utc),
                Label = preset.Label,
                Group = preset.Group,
                BuildType = "Development",
                UnityVersion = "6000.3.28f1",
                Cpu = "Intel(R), 8 cores",
                Gpu = "GPU",
                RamMB = 16384,
                Os = "Windows",
                VSyncCount = 0,
                TargetFrameRate = -1,
                ScriptingBackend = "Mono",
                IncrementalGC = true,
                Selection = preset.Selection,
                MonsterCountN = 900,
                MonsterCapDisabled = true,
                DespawnDistance = MeasurementPresets.DespawnDistanceFor(900),
                DespawnFormula = MeasurementPresets.DespawnFormulaFor(900),
                Seed = 4242UL,
                HpMultipliers = MeasurementCsvExporter.FormatHpMultipliers(new[] { 1f, 1.5f, 2.2f, 3f }),
                DurationSteps = 3600,
                NReachedStep = -1,
                PlayerInvulnerable = true,
                WeaponLoadout = preset.WeaponLoadoutText,
                ChestDropChance = 0f,
                SpawnStopHoldN = true,
                LevelUpEnabled = false,
                AutoPathRadius = 6f,
                AutoPathSpeed = 4f,
                TimeScale = 1f,
                OverlayHidden = false,
                VisualizersOff = true,
                CollisionLogOff = true,
                Valid = false,
                InvalidReason = "overlay opened (F1)",
                Stats = MeasurementStats.FromFrames(new[]
                {
                    new MeasurementFrame(0, 16d, 1, 0d, 1048576d, double.NaN, 900, 2, markers),
                    new MeasurementFrame(1, 16d, 1, 0d, 1048576d, double.NaN, 900, 2, markers),
                }),
            };
        }

        // Minimal RFC 4180 split of one row (quotes, doubled quotes, commas inside quotes).
        private static string[] SplitCsv(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private static int CountBom(byte[] bytes)
        {
            int count = 0;
            for (int i = 0; i + 2 < bytes.Length; i++)
            {
                if (bytes[i] == 0xEF && bytes[i + 1] == 0xBB && bytes[i + 2] == 0xBF)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class FakeCollisionStrategy : ICollisionStrategy
        {
            public readonly List<CollisionEvent> Next = new List<CollisionEvent>();
            public long LastStep = -1;
            public int Clears;

            public CollisionStrategyKind Kind => CollisionStrategyKind.EngineCollider2D;

            public void Register(CollisionBody body)
            {
            }

            public void Unregister(CollisionBody body)
            {
            }

            public void NotifyRadiusChanged(CollisionBody body)
            {
            }

            public IReadOnlyList<CollisionEvent> CollectEvents(long step)
            {
                LastStep = step;
                return Next;
            }

            public void Clear()
            {
                Clears++;
            }
        }
    }
}
