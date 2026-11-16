using System;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.Scripting;

namespace Survivor.Dev
{
    /// One measurement at a time (5.15.2, TAC-F19-3/5/7/9). TryBegin: guard -> ResetZoom -> hide the overlay -> apply and
    /// remember the environment -> next-run settings through DevController (preset selection, no collision decorator, fixed
    /// seed, level-up off, auto path, invulnerable, chest drop 0, cap, spawn stop + hold N) -> new run -> D by R26 and the
    /// preset loadout at step 0. Poll (every DevModeRoot.Update): invalidation checks, frame collection, StopAtStep =
    /// s_N + 300 + durationSteps once s_N is known, and the end. Any end writes the files (valid or not), restores the
    /// environment and shows the overlay again; only a valid end at StopAtStep calls AutoStop (quit / stop playing).
    public sealed class MeasurementSession : IDisposable
    {
        private readonly DevController _controller;
        private readonly DevOverlay _overlay;
        private readonly DevVisualizer _visualizer;
        private readonly StatsCollector _stats = new StatsCollector();

        private MeasurementPreset _preset;
        private int _n;
        private long _durationSteps;
        private string _hpMultipliers = string.Empty;
        private int _seenCloseSequence;
        private int? _loadoutBaseline;
        private bool _finishing;

        private int _savedVSync, _savedTargetFrameRate;
        private bool _savedRunInBackground, _savedAutoPause;
        private float _savedChestDropChance;
        private bool _environmentApplied;

        /// Session over the dev parts; controller, overlay and visualizer are required.
        public MeasurementSession(DevController controller, DevOverlay overlay, DevVisualizer visualizer)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _overlay = overlay ? overlay : throw new ArgumentNullException(nameof(overlay));
            _visualizer = visualizer ? visualizer : throw new ArgumentNullException(nameof(visualizer));
        }

        /// Called after a valid end at StopAtStep (batch measurement only, TAC-F19-3); DevModeRoot sets quit / stop playing,
        /// tests set a stub. Null = nothing.
        public Action AutoStop { get; set; }

        /// True from a successful TryBegin until the end.
        public bool IsRunning { get; private set; }

        /// Overlay status line: refusal, "Measurement invalid: <cause>", the running state or the written file.
        public string StatusText { get; private set; } = string.Empty;

        /// StopAtStep of the current / latest session (-1 until s_N is known).
        public long StopAtStep { get; private set; } = -1;

        /// Record of the latest finished session (null before the first).
        public MeasurementRecord LastRecord { get; private set; }

        /// Measurement file of the latest finished session ("" when none or the write failed).
        public string LastFilePath { get; private set; } = string.Empty;

        /// Frames collected so far / in the latest session.
        public StatsCollector Stats => _stats;

        /// Starts a measurement of preset with N = preset.ResolveN(requestedN) and durationSteps measured steps. Refused (false,
        /// StatusText = cause, nothing changed) while running, for a non-positive duration, outside Title / Result / Paused,
        /// without data or when MeasurementGuard refuses.
        public bool TryBegin(MeasurementPreset preset, int requestedN, long durationSteps)
        {
            IDevModeHost host = _controller.Host;
            string refusal = null;
            if (IsRunning)
            {
                refusal = "A measurement is already running.";
            }
            else if (preset == null)
            {
                refusal = "No preset selected.";
            }
            else if (durationSteps <= 0)
            {
                refusal = "durationSteps must be positive.";
            }
            else if (!(_controller.State == FlowState.Title || _controller.State == FlowState.Result || _controller.State == FlowState.Paused))
            {
                refusal = "Measurement starts from Title, Result or Paused (now " + _controller.State + ").";
            }
            else if (host.Data.Current == null)
            {
                refusal = "No data loaded.";
            }
            else
            {
                string guard = MeasurementGuard.Check(_visualizer.AnyOn, _controller.CollisionLogEnabled, Time.timeScale,
                    _controller.IsSingleStepMode, preset);
                if (guard != null)
                {
                    refusal = "Measurement refused: " + guard;
                }
            }

            if (refusal != null)
            {
                StatusText = refusal;
                return _controller.Note("Measurement", refusal, false);
            }

            _preset = preset;
            _n = preset.ResolveN(requestedN);
            _durationSteps = durationSteps;
            _controller.ResetZoom();
            _overlay.enabled = false;
            ApplyEnvironment(host.Rules);

            host.NextRunSelection = preset.Selection;
            host.NextRunCollisionDecorator = null;
            _controller.SetFixedSeed(MeasurementPresets.Seed);
            _controller.SetLevelUpEnabled(false);
            _controller.TrySetAutoPath(true, MeasurementPresets.AutoPathRadius, MeasurementPresets.AutoPathSpeed);
            _controller.SetInvulnerable(true);
            host.Rules.ChestDropChance = preset.ChestDropChance;
            _controller.SetMonsterCapLifted(_n > GameRules.MonsterCapDefault);
            host.Rules.StopAtStep = long.MaxValue;
            StopAtStep = -1;
            if (!_controller.TryConfigureSpawns(true, null, _n) || !_controller.TryStartNewRun())
            {
                return Abort("Measurement not started: " + _controller.LastMessage);
            }

            // The run reset cleared the queue, so D and the loadout are queued into the new run (both apply at step 0 Begin).
            _controller.TrySetDespawnDistance(MeasurementPresets.DespawnDistanceFor(_n));
            if (!_controller.TrySetLoadout(preset.WeaponLoadout))
            {
                return Abort("Measurement not started: " + _controller.LastMessage);
            }

            _hpMultipliers = MeasurementCsvExporter.FormatHpMultipliers(host.Data.Current.SpawnTable);
            _seenCloseSequence = host.Clock.CloseSequence;
            _loadoutBaseline = null;
            _finishing = false;
            _stats.Begin(_n);
            IsRunning = true;
            StatusText = "Measuring " + preset.Label + " (N " + _n + ", " + durationSteps + " steps after the warmup).";
            DevLog.Info(StatusText);
            return _controller.Note("Measurement", StatusText, true);
        }

        /// Per frame while running (DevModeRoot.Update, after DevController.Poll): ends at once on an invalidation, collects the
        /// frame, sets StopAtStep when s_N appears, and finishes one frame after Step reaches StopAtStep (that frame's profiler
        /// values are read then).
        public void Poll()
        {
            if (!IsRunning)
            {
                return;
            }

            IDevModeHost host = _controller.Host;
            if (_finishing)
            {
                Observe(host);
                Finish(true, string.Empty);
                return;
            }

            string invalid = InvalidCause(host);
            if (invalid != null)
            {
                Finish(false, invalid);
                return;
            }

            Observe(host);
            if (StopAtStep < 0 && _stats.NReachedStep >= 0)
            {
                StopAtStep = _stats.NReachedStep + GameRules.MeasurementWarmupSteps + _durationSteps;
                host.Rules.StopAtStep = StopAtStep;
            }

            if (StopAtStep >= 0 && host.Clock.Step >= host.Rules.StopAtStep)
            {
                _finishing = true;
            }
        }

        /// Ends a running session without files (dev mode leaving) and restores the environment.
        public void Dispose()
        {
            if (IsRunning)
            {
                IsRunning = false;
                _controller.Host.Rules.StopAtStep = long.MaxValue;
                RestoreEnvironment(_controller.Host.Rules);
            }

            _stats.Dispose();
        }

        private void Observe(IDevModeHost host)
        {
            _stats.Observe(host.Clock.Step, host.LastFrameSteps, host.LiveMonsterCount, host.ActiveGemCount, Time.unscaledDeltaTime);
        }

        // 5.15.2 step 6 in a fixed order; the first cause found wins. The loadout baseline is taken after step 0 (SetLoadout).
        private string InvalidCause(IDevModeHost host)
        {
            if (_overlay.enabled)
            {
                return "overlay opened (F1)";
            }

            if (_visualizer.AnyOn)
            {
                return "visualization turned on";
            }

            if (Time.timeScale != 1f)
            {
                return "time scale changed";
            }

            if (_controller.CollisionLogEnabled)
            {
                return "collision log turned on";
            }

            SimulationClockView clock = new SimulationClockView(host);
            if (clock.CloseSequence != _seenCloseSequence)
            {
                _seenCloseSequence = clock.CloseSequence;
                if (clock.CloseReason != GateCloseReason.StopAtStep)
                {
                    return "game gate closed (" + clock.CloseReason + ")";
                }
            }

            if (!_loadoutBaseline.HasValue)
            {
                if (clock.Step >= 1)
                {
                    _loadoutBaseline = host.LoadoutChangeCount;
                }
            }
            else if (host.LoadoutChangeCount != _loadoutBaseline.Value)
            {
                return "weapon loadout changed";
            }

            return null;
        }

        private void Finish(bool valid, string invalidReason)
        {
            IDevModeHost host = _controller.Host;
            _stats.End();
            MeasurementRecord record = BuildRecord(host, valid, invalidReason);
            LastRecord = record;
            string written;
            try
            {
                LastFilePath = new MeasurementCsvExporter(_controller.OutputFolder).Export(record, _stats.Frames);
                _controller.NoteFile(LastFilePath);
                written = LastFilePath;
            }
            catch (Exception e)
            {
                LastFilePath = string.Empty;
                written = "not written (" + e.Message + ")";
                DevLog.Warning("Measurement file not written: " + e.Message);
            }

            host.Rules.StopAtStep = long.MaxValue;
            RestoreEnvironment(host.Rules);
            _overlay.enabled = true;
            IsRunning = false;
            _finishing = false;
            StatusText = valid ? "Measurement done: " + written : "Measurement invalid: " + invalidReason + " -> " + written;
            DevLog.Info(StatusText);
            _controller.Note("Measurement", StatusText, valid);
            if (valid)
            {
                AutoStop?.Invoke();
            }
        }

        private MeasurementRecord BuildRecord(IDevModeHost host, bool valid, string invalidReason)
        {
            SimulationRules rules = host.Rules;
            var path = host.InputOverride as AutoPathInputSource;
            var spawns = host.SpawnPolicyOverride as DevSpawnPolicy;
            return new MeasurementRecord
            {
                TimestampUtc = DateTime.UtcNow,
                Label = _preset.Label,
                Group = _preset.Group,
                BuildType = Debug.isDebugBuild ? "Development" : "Release",
                UnityVersion = Application.unityVersion,
                Cpu = SystemInfo.processorType,
                Gpu = SystemInfo.graphicsDeviceName,
                RamMB = SystemInfo.systemMemorySize,
                Os = SystemInfo.operatingSystem,
                VSyncCount = QualitySettings.vSyncCount,
                TargetFrameRate = Application.targetFrameRate,
                ScriptingBackend = ScriptingBackendName(),
                IncrementalGC = GarbageCollector.isIncremental,
                Selection = _controller.RunSelection,
                MonsterCountN = _n,
                MonsterCapDisabled = rules.MonsterCap == int.MaxValue,
                DespawnDistance = MeasurementPresets.DespawnDistanceFor(_n),
                DespawnFormula = MeasurementPresets.DespawnFormulaFor(_n),
                Seed = host.Session.Rng.MasterSeed,
                HpMultipliers = _hpMultipliers,
                DurationSteps = _durationSteps,
                NReachedStep = _stats.NReachedStep,
                PlayerInvulnerable = !rules.PlayerTakesDamage,
                WeaponLoadout = _preset.WeaponLoadoutText,
                ChestDropChance = rules.ChestDropChance,
                SpawnStopHoldN = spawns != null && spawns.StopSpawns && spawns.HoldCount == _n,
                LevelUpEnabled = rules.LevelUpEnabled,
                AutoPathRadius = path != null ? path.Radius : 0f,
                AutoPathSpeed = path != null ? path.Speed : 0f,
                TimeScale = Time.timeScale,
                OverlayHidden = !_overlay.enabled,
                VisualizersOff = !_visualizer.AnyOn,
                CollisionLogOff = !_controller.CollisionLogEnabled,
                Valid = valid,
                InvalidReason = valid ? string.Empty : invalidReason,
                Stats = _stats.Snapshot(),
            };
        }

        // Start failed after the overlay was hidden: undo the environment, show the overlay, report the cause.
        private bool Abort(string cause)
        {
            _controller.Host.Rules.StopAtStep = long.MaxValue;
            RestoreEnvironment(_controller.Host.Rules);
            _overlay.enabled = true;
            StatusText = cause;
            return _controller.Note("Measurement", cause, false);
        }

        // TAC-F19-9 plus runInBackground and auto pause (SPEC F19 61); the chest chance is put back too (no overlay control).
        private void ApplyEnvironment(SimulationRules rules)
        {
            _savedVSync = QualitySettings.vSyncCount;
            _savedTargetFrameRate = Application.targetFrameRate;
            _savedRunInBackground = Application.runInBackground;
            _savedAutoPause = rules.AutoPauseOnFocusLoss;
            _savedChestDropChance = rules.ChestDropChance;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            rules.AutoPauseOnFocusLoss = false;
            _environmentApplied = true;
        }

        private void RestoreEnvironment(SimulationRules rules)
        {
            if (!_environmentApplied)
            {
                return;
            }

            QualitySettings.vSyncCount = _savedVSync;
            Application.targetFrameRate = _savedTargetFrameRate;
            Application.runInBackground = _savedRunInBackground;
            rules.AutoPauseOnFocusLoss = _savedAutoPause;
            rules.ChestDropChance = _savedChestDropChance;
            _environmentApplied = false;
        }

        private static string ScriptingBackendName()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#elif ENABLE_MONO
            return "Mono";
#else
            return "Unknown";
#endif
        }

        // Clock values read once per check.
        private readonly struct SimulationClockView
        {
            public SimulationClockView(IDevModeHost host)
            {
                Step = host.Clock.Step;
                CloseSequence = host.Clock.CloseSequence;
                CloseReason = host.Clock.CloseReason;
            }

            public readonly long Step;
            public readonly int CloseSequence;
            public readonly GateCloseReason CloseReason;
        }
    }
}
