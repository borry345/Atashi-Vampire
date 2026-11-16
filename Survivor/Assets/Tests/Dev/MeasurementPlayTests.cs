using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Dev;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.Dev
{
    /// Wave 5b on the real Main.unity: measurement guard refusal, a short valid session ending exactly at StopAtStep
    /// (TAC-F19-3, TAC-F19-7; the auto stop is a stub), invalidation by F1 and by the pause gate (5.15.2 step 6), the collision
    /// log export and hot reload / dump (SPEC F19 data). Every file goes to a temp folder deleted in TearDown.
    public sealed class MeasurementPlayTests : DevPmFixture
    {
        private const int TestN = 20;
        private const long TestDuration = 30;

        private DevModeRoot _root;
        private string _tempRoot;
        private int _savedVSync, _savedTargetFrameRate;
        private bool _savedRunInBackground;
        private int _autoStops;
        private long _stepAtAutoStop = -1;

        private DevController Controller => _root.Controller;

        private MeasurementSession Session => _root.Measurement;

        [SetUp]
        public void CreateTempRoot()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "SurvivorDevPm_" + TestContext.CurrentContext.Test.MethodName);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }

            _savedVSync = QualitySettings.vSyncCount;
            _savedTargetFrameRate = Application.targetFrameRate;
            _savedRunInBackground = Application.runInBackground;
            _autoStops = 0;
            _stepAtAutoStop = -1;
        }

        [TearDown]
        public void DeleteTempRoot()
        {
            QualitySettings.vSyncCount = _savedVSync;
            Application.targetFrameRate = _savedTargetFrameRate;
            Application.runInBackground = _savedRunInBackground;
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator Guard_RefusesAndLeavesEverythingAsIs()
        {
            yield return Attach();
            _root.ToggleOverlay();
            StrategySelection before = Host.NextRunSelection;
            FlowState state = Controller.State;
            _root.Visualizer.ShowGrid = true;
            Assert.IsFalse(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration));
            StringAssert.Contains("Visualization", Session.StatusText);
            AssertNothingStarted(before, state);
            Assert.IsTrue(_root.Visualizer.ShowGrid);

            _root.Visualizer.ShowGrid = false;
            Controller.SetCollisionLog(true);
            Assert.IsFalse(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration));
            StringAssert.Contains("Collision log", Session.StatusText);
            AssertNothingStarted(before, state);

            Controller.SetCollisionLog(false);
            Controller.SetTimeScale(2f);
            Assert.IsFalse(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration));
            StringAssert.Contains("x2", Session.StatusText);
            AssertNothingStarted(before, state);
            Controller.SetTimeScale(1f);
            yield return null;
            Assert.IsFalse(Directory.Exists(_tempRoot), "no file written");
        }

        [UnityTest]
        public IEnumerator ValidSession_StopsExactlyAtStopAtStep_WritesThreeFiles()
        {
            yield return Attach();
            Assert.IsTrue(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration), Session.StatusText);
            Assert.IsFalse(_root.Overlay.enabled, "overlay hidden at the start");
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.AreEqual(-1, Application.targetFrameRate);
            Assert.IsTrue(Application.runInBackground);
            Assert.IsFalse(Host.Rules.AutoPauseOnFocusLoss);
            Assert.IsFalse(Host.Rules.PlayerTakesDamage);
            Assert.IsFalse(Host.Rules.LevelUpEnabled);
            Assert.AreEqual(0f, Host.Rules.ChestDropChance);
            Assert.IsNull(Host.NextRunCollisionDecorator);
            Assert.AreEqual(4242UL, Host.Session.Rng.MasterSeed);
            Assert.AreEqual(StrategySelection.ShippingDefault.Pool, Controller.RunSelection.Pool);
            yield return WaitFor(() => !Session.IsRunning, "measurement end");

            Assert.AreEqual(1, _autoStops, "auto stop called once");
            Assert.AreEqual(Session.StopAtStep, _stepAtAutoStop, "the clock stood at StopAtStep when the stop was called");
            Assert.AreEqual(Session.StopAtStep, Host.Clock.Step, "exactly StopAtStep steps ran");
            Assert.IsFalse(Host.Clock.IsRunning);
            Assert.AreEqual(GateCloseReason.StopAtStep, Host.Clock.CloseReason);
            StatsCollector stats = Session.Stats;
            Assert.GreaterOrEqual(stats.NReachedStep, 0L);
            Assert.AreEqual(stats.NReachedStep + GameRules.MeasurementWarmupSteps + TestDuration, Session.StopAtStep);
            Assert.GreaterOrEqual(stats.WindowStartStep, stats.NReachedStep + GameRules.MeasurementWarmupSteps, "warmup excluded");
            Assert.Less(stats.WindowStartStep, stats.NReachedStep + GameRules.MeasurementWarmupSteps + StepMath.MaxCatchUpSteps);
            MeasurementRecord record = Session.LastRecord;
            Assert.AreEqual(Session.StopAtStep - stats.WindowStartStep, record.Stats.Steps, "kept steps = window start .. StopAtStep");
            Assert.AreEqual(stats.Frames.Count, record.Stats.Frames);
            Assert.IsTrue(record.Valid);

            Assert.IsTrue(_root.Overlay.enabled, "overlay shown again");
            Assert.AreEqual(_savedVSync, QualitySettings.vSyncCount, "environment restored");
            Assert.AreEqual(_savedTargetFrameRate, Application.targetFrameRate);
            Assert.AreEqual(_savedRunInBackground, Application.runInBackground);
            Assert.IsTrue(Host.Rules.AutoPauseOnFocusLoss);
            Assert.AreEqual(long.MaxValue, Host.Rules.StopAtStep);

            string folder = Path.Combine(_tempRoot, MeasurementCsvExporter.FolderName);
            string stamp = MeasurementCsvExporter.FileStamp(record.TimestampUtc);
            Assert.AreEqual(Path.Combine(folder, "measurement_" + stamp + ".csv"), Session.LastFilePath);
            Assert.IsTrue(File.Exists(Session.LastFilePath));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "frames_" + stamp + ".csv")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, MeasurementCsvExporter.SummaryFileName)), "valid -> summary row");
            var keys = new List<string>();
            Dictionary<string, string> header = ReadHeader(Session.LastFilePath, keys);
            CollectionAssert.AreEqual(MeasurementCsvExporter.HeaderKeys, keys, "every header key, in order");
            Assert.AreEqual("true", header["valid"]);
            Assert.AreEqual(string.Empty, header["invalidReason"]);
            Assert.AreEqual("A6", header["label"]);
            Assert.AreEqual("4242", header["seed"]);
            Assert.AreEqual("20", header["monsterCountN"]);
            Assert.AreEqual("45", header["despawnDistance"]);
            Assert.AreEqual("true", header["spawnStopHoldN"]);
            Assert.AreEqual("true", header["overlayHidden"]);
            Assert.AreEqual(stats.NReachedStep.ToString(System.Globalization.CultureInfo.InvariantCulture), header["nReachedStep"]);
            Assert.AreEqual(MeasurementCsvExporter.FormatHpMultipliers(Host.Data.Current.SpawnTable), header["hpMultipliers"]);
            Assert.AreEqual("1.0;1.5;2.2;3.0", header["hpMultipliers"], "TAC-F19-14: R31 values in segment order");
            Assert.AreEqual(GameRules.DefaultDespawnDistance, Host.Rules.DespawnDistance, "D set by R26 for N = 20");
            Assert.IsTrue(Host.Rules.DespawnDistanceOverridden);
        }

        [UnityTest]
        public IEnumerator Overlay_DuringMeasurement_InvalidatesAtOnce()
        {
            yield return Attach();
            Assert.IsTrue(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration), Session.StatusText);
            yield return WaitFor(() => Host.Clock.Step >= 20, "step 20");
            _root.ToggleOverlay();
            yield return WaitFor(() => !Session.IsRunning, "invalid end", 3);
            AssertInvalid("overlay opened (F1)");
            Assert.IsTrue(Host.Clock.IsRunning, "ended before StopAtStep; the run goes on");
            Assert.Less(Host.Clock.Step, GameRules.MeasurementWarmupSteps, "did not run on to StopAtStep");
        }

        [UnityTest]
        public IEnumerator PauseGate_DuringMeasurement_Invalidates()
        {
            yield return Attach();
            Assert.IsTrue(Session.TryBegin(MeasurementPresets.Find("A6"), TestN, TestDuration), Session.StatusText);
            yield return WaitFor(() => Host.Clock.Step >= 20, "step 20");
            Host.Flow.TogglePause();
            yield return WaitFor(() => !Session.IsRunning, "invalid end", 3);
            AssertInvalid("game gate closed (Paused)");
            Assert.IsTrue(_root.Overlay.enabled, "overlay shown again");
        }

        [UnityTest]
        public IEnumerator CollisionLog_RecordsTheRun_AndExports()
        {
            yield return Attach();
            Assert.IsFalse(Controller.TryExportCollisionLog(), "no log before a logged run");
            Controller.SetFixedSeed(4242UL);
            Controller.SetInvulnerable(true);
            Controller.SetLevelUpEnabled(false);
            Controller.SetCollisionLog(true);
            Assert.IsNotNull(Host.NextRunCollisionDecorator);
            Assert.IsTrue(Controller.TryStartNewRun(), Controller.LastMessage);
            CollisionEventLog log = Controller.CurrentCollisionLog;
            Assert.IsNotNull(log, "the run started with the decorator");
            yield return WaitFor(() => log.Events.Count >= 2, "collision events");
            Assert.IsTrue(Controller.TryExportCollisionLog(), Controller.LastMessage);
            string expected = Path.Combine(_tempRoot, CollisionEventLog.FileName(4242UL, Controller.RunSelection.Collision,
                Controller.RunSelection.EngineDetectionMode));
            Assert.AreEqual(expected, Controller.LastFilePath);
            int count = log.Events.Count;
            string[] lines = File.ReadAllText(expected, Encoding.UTF8).Split(new[] { "\r\n" }, StringSplitOptions.None);
            Assert.AreEqual("step,channel,a,b,kind", lines[0].TrimStart('﻿'));
            Assert.AreEqual(count, lines.Length - 2, "one row per event, then the final line end");
            for (int i = 1; i < lines.Length - 1; i++)
            {
                Assert.AreEqual(log.Events[i - 1].ToString(), lines[i]);
            }

            Controller.SetCollisionLog(false);
            Assert.IsNull(Host.NextRunCollisionDecorator, "off removes the decorator for the next run");
        }

        [UnityTest]
        public IEnumerator HotReloadAndDump_WorkThroughTheController()
        {
            yield return Attach();
            Assert.IsFalse(Controller.TryHotReload(), "no run -> refused");
            StringAssert.Contains("run in progress", Controller.LastMessage);
            Controller.SetFixedSeed(4242UL);
            Assert.IsTrue(Controller.TryStartNewRun(), Controller.LastMessage);
            int swaps = 0;
            Action<Survivor.Data.GameData> onSwap = _ => swaps++;
            Host.Data.Swapped += onSwap;
            try
            {
                Assert.IsTrue(Controller.TryHotReload(), Controller.LastMessage);
                yield return WaitFor(() => swaps == 1, "SwapData applied at a Begin");
            }
            finally
            {
                Host.Data.Swapped -= onSwap;
            }

            Assert.IsTrue(Controller.TryDumpData(), Controller.LastMessage);
            string dump = File.ReadAllText(Controller.LastFilePath, Encoding.UTF8);
            StringAssert.StartsWith(Path.Combine(_tempRoot, "dump_"), Controller.LastFilePath);
            StringAssert.Contains("# data", dump);
            StringAssert.Contains("data.SpawnTable.Segments[0].StartStep = ", dump);
            StringAssert.Contains("data.SpawnTable.Segments[0].StartSeconds = ", dump);
            StringAssert.Contains("# report", dump);
            StringAssert.Contains("playable: true", dump);
        }

        private IEnumerator Attach()
        {
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            Controller.OutputRoot = _tempRoot;
            Session.AutoStop = () =>
            {
                _autoStops++;
                _stepAtAutoStop = Host.Clock.Step;
            };
        }

        private void AssertNothingStarted(StrategySelection before, FlowState state)
        {
            Assert.IsFalse(Session.IsRunning);
            Assert.IsTrue(_root.Overlay.enabled, "the overlay stays open");
            Assert.AreEqual(state, Controller.State, "no run started");
            Assert.AreEqual(before.Pool, Host.NextRunSelection.Pool);
            Assert.AreEqual(before.Target, Host.NextRunSelection.Target);
            Assert.AreEqual(long.MaxValue, Host.Rules.StopAtStep);
            Assert.AreEqual(_savedVSync, QualitySettings.vSyncCount);
        }

        private void AssertInvalid(string reason)
        {
            MeasurementRecord record = Session.LastRecord;
            Assert.IsFalse(record.Valid);
            Assert.AreEqual(reason, record.InvalidReason);
            Assert.AreEqual(0, _autoStops, "no auto stop for an invalid session");
            StringAssert.StartsWith("Measurement invalid: " + reason, Session.StatusText);
            Assert.IsTrue(File.Exists(Session.LastFilePath), "invalid sessions write the file too");
            Dictionary<string, string> header = ReadHeader(Session.LastFilePath);
            Assert.AreEqual("false", header["valid"]);
            Assert.AreEqual(reason, header["invalidReason"]);
            Assert.IsFalse(File.Exists(Path.Combine(_tempRoot, MeasurementCsvExporter.FolderName, MeasurementCsvExporter.SummaryFileName)),
                "no summary row for an invalid session");
            Assert.AreEqual(_savedVSync, QualitySettings.vSyncCount, "environment restored");
            Assert.AreEqual(_savedTargetFrameRate, Application.targetFrameRate);
            Assert.AreEqual(long.MaxValue, Host.Rules.StopAtStep);
        }

        // Header block of a measurement file: key,value rows up to the empty row (values here hold no comma or quote).
        private static Dictionary<string, string> ReadHeader(string path, List<string> keysOut = null)
        {
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
            var header = new Dictionary<string, string>();
            foreach (string line in text.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.Length == 0)
                {
                    break;
                }

                int comma = line.IndexOf(',');
                string key = line.Substring(0, comma);
                string value = line.Substring(comma + 1);
                if (value.Length >= 2 && value[0] == '"')
                {
                    value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
                }

                header.Add(key, value);
                keysOut?.Add(key);
            }

            return header;
        }
    }
}
