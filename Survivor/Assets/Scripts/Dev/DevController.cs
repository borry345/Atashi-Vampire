using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.App;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Dev
{
    /// Turns overlay actions into IDevModeHost calls (5.15, TAC-F19-2): SimulationRules fields, next-run overrides (selection,
    /// input, spawn policy, seed), pending commands, Time.timeScale and the camera zoom only. Every Try* method validates first,
    /// so no command that would throw at Begin is ever queued; every user action leaves its result in LastMessage and one line
    /// in CommandLog, which also collects the chest results (5.15.5).
    public sealed class DevController : IDisposable
    {
        /// Offset from the player of SpawnBossNow / SpawnChestNow (PM default).
        public static readonly Vector2 SpawnOffset = new Vector2(3f, 0f);

        private readonly IDevModeHost _host;
        private readonly GameFlowController _flow;
        private StrategySelection _runSelection;
        private TargetProviderKind _currentTarget;
        private bool _runSeedFixed;
        private int _seenCloseSequence;
        private long _seenStep;
        private float? _pendingDespawnDistance;
        private readonly DevCommandLog _commandLog = new DevCommandLog();
        private int _runNumber;
        private long _lastChestSequence;

        /// Binds to host (null throws ArgumentNullException); flow (null allowed only for hosts without a screen flow) gates
        /// the run-state checks. Subscribes to RunEnded to reset the time scale.
        public DevController(IDevModeHost host, GameFlowController flow)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _flow = flow;
            _runSelection = host.NextRunSelection;
            _currentTarget = _runSelection.Target;
            _runSeedFixed = host.FixedSeed.HasValue;
            _seenCloseSequence = host.Clock.CloseSequence;
            _seenStep = host.Clock.Step;
            _host.RunEnded += OnRunEnded;
        }

        /// Host this controller drives.
        public IDevModeHost Host => _host;

        /// Smallest / largest camera orthographic size dev mode allows (display only; 9 stays GameRules.CameraOrthographicSize).
        public const float MinZoom = 4.5f, MaxZoom = 27f;

        /// Result of the latest action ("" before the first one); the command log keeps the latest lines.
        public string LastMessage { get; private set; } = string.Empty;

        /// Latest command-log lines (user actions and chest results) for the session; a run reset never clears it.
        public DevCommandLog CommandLog => _commandLog;

        /// 1-based run number within the session (0 before the first run); +1 at every run reset dev mode notices.
        public int RunNumber => _runNumber;

        /// Table spawns state dev last queued in this run (true at every run reset); may lead the real switch by one step.
        public bool SpawnsEnabled { get; private set; } = true;

        /// Strategy selection the current run started with (target: see CurrentTarget).
        public StrategySelection RunSelection => _runSelection;

        /// Target provider kind dev last applied (run start or a runtime switch); may lead the real switch by one step.
        public TargetProviderKind CurrentTarget => _currentTarget;

        /// Screen flow state, or Title when there is no flow.
        public FlowState State => _flow != null ? _flow.State : FlowState.Title;

        /// True while a run exists (Playing, LevelUp or Paused).
        public bool IsRunActive => State == FlowState.Playing || State == FlowState.LevelUp || State == FlowState.Paused;

        /// True while one-step mode holds the gate (Playing, gate closed with SingleStepDone).
        public bool IsSingleStepMode => State == FlowState.Playing && !_host.Clock.IsRunning
            && _host.Clock.CloseReason == GateCloseReason.SingleStepDone;

        /// Despawn distance in use: the dev override, else the current spawn table's (0 without data).
        public float DespawnDistanceInUse
        {
            get
            {
                if (_host.Rules.DespawnDistanceOverridden)
                {
                    return _host.Rules.DespawnDistance;
                }

                GameData data = _host.Data.Current;
                return data != null && data.SpawnTable != null ? data.SpawnTable.DespawnDistance : 0f;
            }
        }

        /// R20 clamp of a requested time scale to [MinTimeScale, MaxTimeScale]; NaN (unparsable input) gives 1.
        public static float ClampTimeScale(float requested)
        {
            return float.IsNaN(requested) ? 1f : Mathf.Clamp(requested, GameRules.MinTimeScale, GameRules.MaxTimeScale);
        }

        /// Zoom clamp: NaN (unparsable input) gives GameRules.CameraOrthographicSize, else [MinZoom, MaxZoom].
        public static float ClampZoom(float requested)
        {
            return float.IsNaN(requested) ? GameRules.CameraOrthographicSize : Mathf.Clamp(requested, MinZoom, MaxZoom);
        }

        /// D9: maximumDeltaTime that keeps at most MaxCatchUpSteps steps per frame at timeScale (0.05 / timeScale).
        public static float MaxDeltaTimeFor(float timeScale)
        {
            return StepMath.MaxCatchUpSteps * StepMath.StepSeconds / timeScale;
        }

        /// Per-frame bookkeeping (DevModeRoot.Update): detects a run reset (any starter) to record the run's selection and
        /// seed mode and to queue a despawn distance chosen while no run existed.
        public void Poll()
        {
            PullChestResults();
            SimulationClock clock = _host.Clock;
            bool closed = clock.CloseSequence != _seenCloseSequence;
            bool reset = (closed && clock.CloseReason == GateCloseReason.RunReset) || clock.Step < _seenStep;
            _seenCloseSequence = clock.CloseSequence;
            _seenStep = clock.Step;
            if (reset)
            {
                OnRunReset();
            }
        }

        /// SetTimeScale (5.15.1): timeScale = clamp(x), then maximumDeltaTime = 0.05 / timeScale. Returns the applied scale.
        public float SetTimeScale(float requested)
        {
            float scale = ClampTimeScale(requested);
            Time.timeScale = scale;
            Time.maximumDeltaTime = MaxDeltaTimeFor(scale);
            Done("TimeScale", "Time scale x" + scale.ToString("F2", CultureInfo.InvariantCulture) + ".");
            return scale;
        }

        /// Back to 1x and maximumDeltaTime 0.05 (run start / run end, 5.15.1).
        public void ResetTimeScale()
        {
            Time.timeScale = 1f;
            Time.maximumDeltaTime = MaxDeltaTimeFor(1f);
        }

        /// StartNewRun from Title, Result or Paused only (App does not check, P5): time scale reset, then the host restarts
        /// with the next-run settings. False (with the reason) in any other state.
        public bool TryStartNewRun()
        {
            PullChestResults();
            FlowState state = State;
            if (_flow == null || !(state == FlowState.Title || state == FlowState.Result || state == FlowState.Paused))
            {
                return Refuse("StartNewRun", "New run only from Title, Result or Paused (now " + state + ").");
            }

            ResetTimeScale();
            _host.StartNewRun();
            Poll();
            if (_flow.State != FlowState.Playing)
            {
                return Refuse("StartNewRun", "New run refused by the game (data not playable?).");
            }

            DevLog.Info("New run started by dev mode (seed " + (_host.FixedSeed.HasValue ? "fixed" : "random") + ").");
            return Done("StartNewRun", "New run started.");
        }

        /// Forward-only time jump (TAC-F19-3): a target before Clock.Step is refused here and never reaches the clock.
        public bool TryJumpToStep(long targetStep)
        {
            if (!IsRunActive)
            {
                return Refuse("JumpToStep", "Jump needs a run in progress.");
            }

            long now = _host.Clock.Step;
            if (targetStep < now)
            {
                return Refuse("JumpToStep", "Jump refused: step " + targetStep.ToString(CultureInfo.InvariantCulture) + " is before the current step "
                    + now.ToString(CultureInfo.InvariantCulture) + " (forward only).");
            }

            _host.Clock.RequestJump(targetStep);
            return Done("JumpToStep", "Jump to step " + targetStep.ToString(CultureInfo.InvariantCulture) + " at the next step.");
        }

        /// One step (5.3): opens the gate for exactly one step, after which it closes with SingleStepDone. While running it
        /// stops after the next step. Only in Playing.
        public bool TryStepOnce()
        {
            if (State != FlowState.Playing)
            {
                return Refuse("StepOnce", "Step needs Playing (now " + State + ").");
            }

            _host.Clock.RequestSingleStep();
            return Done("StepOnce", "One step requested.");
        }

        /// Leaves one-step mode (gate closed with SingleStepDone in Playing) by reopening the gate.
        public bool TryResume()
        {
            if (!IsSingleStepMode)
            {
                return Refuse("Resume", "Resume only from one-step mode.");
            }

            _host.Clock.Open();
            return Done("Resume", "Resumed.");
        }

        /// Player invulnerability (Rules.PlayerTakesDamage = !on), immediate.
        public void SetInvulnerable(bool on)
        {
            _host.Rules.PlayerTakesDamage = !on;
            Done("Invulnerable", "Invulnerable " + OnOff(on) + ".");
        }

        /// Weapons on/off (Rules.WeaponsEnabled), immediate; existing orbs and zones stop damaging too (SPEC F19).
        public void SetWeaponsEnabled(bool on)
        {
            _host.Rules.WeaponsEnabled = on;
            Done("WeaponsEnabled", "Weapons " + OnOff(on) + ".");
        }

        /// Level-up choices on/off (Rules.LevelUpEnabled), immediate.
        public void SetLevelUpEnabled(bool on)
        {
            _host.Rules.LevelUpEnabled = on;
            Done("LevelUpEnabled", "Level-up " + OnOff(on) + ".");
        }

        /// Monster cap lifted (int.MaxValue) or back to the default 400, immediate.
        public void SetMonsterCapLifted(bool lifted)
        {
            _host.Rules.MonsterCap = lifted ? int.MaxValue : GameRules.MonsterCapDefault;
            Done("MonsterCapLifted", "Monster cap " + (lifted ? "lifted" : GameRules.MonsterCapDefault.ToString(CultureInfo.InvariantCulture)) + ".");
        }

        /// Fixed master seed for the next run (null = a fresh seed per run).
        public void SetFixedSeed(ulong? seed)
        {
            _host.FixedSeed = seed;
            Done("FixedSeed", "Seed for the next run: " + (seed.HasValue ? seed.Value.ToString(CultureInfo.InvariantCulture) : "random") + ".");
        }

        /// Auto path input for the next run: a circle of radius through the spawn point (center (-radius, 0), start angle 0) at
        /// speed units per second; off restores the keyboard. False for a non-positive or infinite radius or speed.
        public bool TrySetAutoPath(bool on, float radius, float speed)
        {
            if (!on)
            {
                _host.InputOverride = null;
                return Done("AutoPath", "Auto path off (next run).");
            }

            if (!IsPositiveFinite(radius) || !IsPositiveFinite(speed))
            {
                return Refuse("AutoPath", "Auto path needs a positive radius and speed.");
            }

            _host.InputOverride = new AutoPathInputSource(new Vector2(-radius, 0f), radius, speed, 0f);
            return Done("AutoPath", "Auto path r=" + radius.ToString("0.##", CultureInfo.InvariantCulture) + " speed="
                + speed.ToString("0.##", CultureInfo.InvariantCulture) + " (next run).");
        }

        /// Pool strategy of the next run.
        public void SetNextPool(PoolStrategyKind kind)
        {
            StrategySelection s = _host.NextRunSelection;
            _host.NextRunSelection = new StrategySelection(kind, s.Target, s.Tick, s.Collision, s.EngineDetectionMode, s.SpatialHashCellSize);
            Done("NextPool", "Pool " + kind + " (next run).");
        }

        /// Tick strategy of the next run.
        public void SetNextTick(TickStrategyKind kind)
        {
            StrategySelection s = _host.NextRunSelection;
            _host.NextRunSelection = new StrategySelection(s.Pool, s.Target, kind, s.Collision, s.EngineDetectionMode, s.SpatialHashCellSize);
            Done("NextTick", "Tick " + kind + " (next run).");
        }

        /// Collision strategy of the next run.
        public void SetNextCollision(CollisionStrategyKind kind)
        {
            StrategySelection s = _host.NextRunSelection;
            _host.NextRunSelection = new StrategySelection(s.Pool, s.Target, s.Tick, kind, s.EngineDetectionMode, s.SpatialHashCellSize);
            Done("NextCollision", "Collision " + kind + " (next run).");
        }

        /// Engine collider detection mode (CCD) of the next run.
        public void SetNextCcd(CollisionDetectionMode2D mode)
        {
            StrategySelection s = _host.NextRunSelection;
            _host.NextRunSelection = new StrategySelection(s.Pool, s.Target, s.Tick, s.Collision, mode, s.SpatialHashCellSize);
            Done("NextCcd", "CCD " + mode + " (next run).");
        }

        /// Target provider: kept for the next run and, during a run, switched now (next Begin, TAC-F16-5).
        public void SetTarget(TargetProviderKind kind)
        {
            StrategySelection s = _host.NextRunSelection;
            _host.NextRunSelection = new StrategySelection(s.Pool, kind, s.Tick, s.Collision, s.EngineDetectionMode, s.SpatialHashCellSize);
            if (IsRunActive)
            {
                _host.RequestTargetProviderSwitch(kind);
                _currentTarget = kind;
                Done("Target", "Target " + kind + " (now).");
                return;
            }

            Done("Target", "Target " + kind + " (next run).");
        }

        /// Spawn policy of the next run: stop table spawns, rename them to one species, hold N live monsters. All off restores
        /// the table policy. Changing N sets D by R26 (SetDespawnDistance now, or at the next run start when no run exists) and
        /// lifts the cap above 400. False with the reason for an unknown species or a negative N.
        public bool TryConfigureSpawns(bool stopSpawns, string onlySpeciesId, int holdCount)
        {
            GameData data = _host.Data.Current;
            if (data == null)
            {
                return Refuse("ConfigureSpawns", "No data loaded.");
            }

            if (holdCount < 0)
            {
                return Refuse("ConfigureSpawns", "Hold N must be 0 (off) or positive.");
            }

            string only = string.IsNullOrEmpty(onlySpeciesId) ? null : onlySpeciesId;
            if (only != null && !IsSpawnableMonster(data, only))
            {
                return Refuse("ConfigureSpawns", "Unknown or boss monster id '" + only + "'.");
            }

            if (!stopSpawns && only == null && holdCount == 0)
            {
                _host.SpawnPolicyOverride = null;
                return Done("ConfigureSpawns", "Spawns: table (next run).");
            }

            string holdSpecies = only ?? FirstSpawnableMonster(data);
            if (holdCount > 0 && holdSpecies == null)
            {
                return Refuse("ConfigureSpawns", "No non-boss monster in the data for hold mode.");
            }

            _host.SpawnPolicyOverride = new DevSpawnPolicy(new TableSpawnPolicy(_host.Data), stopSpawns, only, holdCount, holdSpecies);
            if (holdCount > 0)
            {
                ApplyDespawnDistance(SpawnGeometry.DespawnDistanceFor(holdCount));
                if (holdCount > GameRules.MonsterCapDefault)
                {
                    _host.Rules.MonsterCap = int.MaxValue;
                }
            }

            return Done("ConfigureSpawns", "Spawns: " + (stopSpawns ? "stopped" : "table") + (only != null ? ", only " + only : string.Empty)
                + (holdCount > 0 ? ", hold " + holdCount.ToString(CultureInfo.InvariantCulture) : string.Empty) + " (next run).");
        }

        /// Despawn distance D now (SetDespawnDistance command). False unless positive and finite or without a run.
        public bool TrySetDespawnDistance(float distance)
        {
            if (!IsPositiveFinite(distance))
            {
                return Refuse("DespawnDistance", "D must be positive and finite.");
            }

            if (!IsRunActive)
            {
                return Refuse("DespawnDistance", "D needs a run in progress.");
            }

            _host.Commands.Enqueue(PendingCommand.SetDespawnDistance(distance));
            return Done("DespawnDistance", "D = " + distance.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        /// Spawns count monsters of monsterId on the spawn circle (radius 20) around the player at the next step.
        public bool TrySpawnMonstersNow(string monsterId, int count)
        {
            GameData data = _host.Data.Current;
            if (!IsRunActive || data == null)
            {
                return Refuse("SpawnMonstersNow", "Spawn needs a run in progress.");
            }

            if (count <= 0)
            {
                return Refuse("SpawnMonstersNow", "Spawn count must be positive.");
            }

            if (string.IsNullOrEmpty(monsterId) || !IsSpawnableMonster(data, monsterId))
            {
                return Refuse("SpawnMonstersNow", "Unknown or boss monster id '" + monsterId + "' (use Spawn boss for the boss).");
            }

            _host.Commands.Enqueue(PendingCommand.SpawnMonstersNow(monsterId, count, PlayerPosition, GameRules.SpawnRadius, false));
            return Done("SpawnMonstersNow", "Spawn " + count.ToString(CultureInfo.InvariantCulture) + " x " + monsterId + " queued.");
        }

        /// Boss now at the player + (3, 0); refused with "Boss: one per run" when the boss already spawned (no command queued).
        public bool TrySpawnBossNow()
        {
            GameData data = _host.Data.Current;
            if (!IsRunActive || data == null)
            {
                return Refuse("SpawnBossNow", "Boss needs a run in progress.");
            }

            if (_host.BossSpawnedThisRun)
            {
                return Refuse("SpawnBossNow", "Boss: one per run.");
            }

            string bossId = data.SpawnTable != null ? data.SpawnTable.BossMonsterId : null;
            if (string.IsNullOrEmpty(bossId) || !data.TryGetMonster(bossId, out _))
            {
                return Refuse("SpawnBossNow", "The spawn table has no valid boss.");
            }

            _host.Commands.Enqueue(PendingCommand.SpawnBossNow(bossId, PlayerPosition + SpawnOffset));
            return Done("SpawnBossNow", "Boss " + bossId + " queued.");
        }

        /// Chest now at the player + (3, 0).
        public bool TrySpawnChestNow()
        {
            if (!IsRunActive)
            {
                return Refuse("SpawnChestNow", "Chest needs a run in progress.");
            }

            _host.Commands.Enqueue(PendingCommand.SpawnChestNow(PlayerPosition + SpawnOffset));
            return Done("SpawnChestNow", "Chest queued.");
        }

        /// Player health now (clamped to [0, max] by the player).
        public bool TrySetHealth(float health)
        {
            if (!IsRunActive)
            {
                return Refuse("Health", "Health needs a run in progress.");
            }

            if (float.IsNaN(health) || float.IsInfinity(health))
            {
                return Refuse("Health", "Health must be a finite number.");
            }

            _host.Commands.Enqueue(PendingCommand.SetHealth(health));
            return Done("Health", "Health " + health.ToString("0.##", CultureInfo.InvariantCulture) + " queued.");
        }

        /// Adds xp experience at the next step.
        public bool TryAddXp(int xp)
        {
            if (!IsRunActive)
            {
                return Refuse("AddXp", "XP needs a run in progress.");
            }

            if (xp <= 0)
            {
                return Refuse("AddXp", "XP must be positive.");
            }

            _host.Commands.Enqueue(PendingCommand.AddXp(xp));
            return Done("AddXp", "+" + xp.ToString(CultureInfo.InvariantCulture) + " XP queued.");
        }

        /// Adds the experience for the next level at the next step (the level-up round opens when level-up is enabled).
        public bool TryLevelUpNow()
        {
            if (!IsRunActive)
            {
                return Refuse("LevelUpNow", "Level up needs a run in progress.");
            }

            _host.Commands.Enqueue(PendingCommand.LevelUpNow());
            return Done("LevelUpNow", "Level up queued.");
        }

        /// Plans request with DevLoadoutPlanner (base weapons at levels 0..n, 0 or missing = removed, evolved weapons kept in their
        /// slot) and queues the planned slot list (SetLoadout) for the next step. Refused without a run or when the planner
        /// refuses (5.15.5); a refusal queues nothing.
        public bool TrySetLoadout(IReadOnlyList<WeaponLoadoutEntry> request)
        {
            GameData data = _host.Data.Current;
            if (!IsRunActive || data == null)
            {
                return Refuse("Loadout", "Loadout needs a run in progress.");
            }

            if (request == null)
            {
                return Refuse("Loadout", "Loadout needs a list of weapons.");
            }

            WeaponInventory weapons = _host.Session.Weapons;
            var planned = new List<WeaponLoadoutEntry>(GameRules.MaxWeaponSlots);
            if (!DevLoadoutPlanner.TryPlan(weapons.Slots, weapons.ConsumedByEvolution, data, request, planned, out string refusal))
            {
                return Refuse("Loadout", refusal);
            }

            int owned = 0;
            for (int i = 0; i < planned.Count; i++)
            {
                if (planned[i].WeaponId != null)
                {
                    owned++;
                }
            }

            _host.Commands.Enqueue(PendingCommand.SetLoadout(planned));
            return Done("Loadout", "Loadout of " + owned.ToString(CultureInfo.InvariantCulture) + " weapon(s) queued.");
        }

        /// Plans request with DevPassiveLoadoutPlanner (passives at levels 0..max, 0 or missing = removed, all zero allowed) and
        /// queues the planned slot list (SetPassiveLoadout) for the next step. Refused without a run or when the planner refuses
        /// (5.15.5); a refusal queues nothing.
        public bool TrySetPassiveLoadout(IReadOnlyList<PassiveLoadoutEntry> request)
        {
            GameData data = _host.Data.Current;
            if (!IsRunActive || data == null)
            {
                return Refuse("PassiveLoadout", "Passive loadout needs a run in progress.");
            }

            if (request == null)
            {
                return Refuse("PassiveLoadout", "Passive loadout needs a list of passives.");
            }

            var planned = new List<PassiveLoadoutEntry>(GameRules.MaxPassiveSlots);
            if (!DevPassiveLoadoutPlanner.TryPlan(_host.Session.Passives.Slots, data, request, planned, out string refusal))
            {
                return Refuse("PassiveLoadout", refusal);
            }

            int owned = 0;
            for (int i = 0; i < planned.Count; i++)
            {
                if (planned[i].PassiveId != null)
                {
                    owned++;
                }
            }

            _host.Commands.Enqueue(PendingCommand.SetPassiveLoadout(planned));
            return Done("PassiveLoadout", "Passive loadout of " + owned.ToString(CultureInfo.InvariantCulture) + " passive(s) queued.");
        }

        /// Table spawns of the current run on or off from the next step (SetSpawnsEnabled command, 5.12.1). Refused without a run
        /// (the flag is per run and every run starts with spawns on).
        public bool TrySetSpawnsEnabled(bool enabled)
        {
            if (!IsRunActive)
            {
                return Refuse("SpawnsEnabled", "Spawn toggle needs a run in progress.");
            }

            _host.Commands.Enqueue(PendingCommand.SetSpawnsEnabled(enabled));
            SpawnsEnabled = enabled;
            return Done("SpawnsEnabled", "Table spawns " + OnOff(enabled) + " from the next step.");
        }

        /// Camera.main.orthographicSize = ClampZoom(requested) (display only). Refused when there is no main camera.
        public bool TrySetZoom(float requested)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Refuse("Zoom", "No main camera.");
            }

            float size = ClampZoom(requested);
            camera.orthographicSize = size;
            return Done("Zoom", "Zoom " + size.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        /// Camera.main.orthographicSize back to GameRules.CameraOrthographicSize (9); no-op without a camera (logged either way).
        public void ResetZoom()
        {
            if (!ApplyDefaultZoom())
            {
                Refuse("ResetZoom", "No main camera.");
                return;
            }

            Done("ResetZoom", "Zoom " + GameRules.CameraOrthographicSize.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        /// Copies the status panel values from the host (read only); actualSpeed comes from the overlay's one-second window.
        public DevStatusInput BuildStatusInput(float actualSpeed)
        {
            RunSession session = _host.Session;
            SimulationClock clock = _host.Clock;
            return new DevStatusInput(clock.Step, session.Rng.MasterSeed, _runSeedFixed, _host.LiveMonsterCount, _host.ActiveGemCount,
                _runSelection.Pool, _currentTarget, _runSelection.Tick, _runSelection.Collision, _runSelection.EngineDetectionMode,
                _host.SpatialIndex.CellSize, Time.timeScale, actualSpeed,
                _host.Data.Current?.SpawnTable?.HpMultiplierAt(clock.Step), State, clock.IsRunning, clock.CloseReason,
                session.Weapons.Slots, session.Passives.Slots);
        }

        /// Unsubscribes from the host and puts the camera back to the default size (leaving dev mode, 5.15.5).
        public void Dispose()
        {
            _host.RunEnded -= OnRunEnded;
            ApplyDefaultZoom();
        }

        private Vector2 PlayerPosition => _host.Session.Player.Position;

        private void OnRunReset()
        {
            _runNumber++;
            SpawnsEnabled = true;
            _runSelection = _host.NextRunSelection;
            _currentTarget = _runSelection.Target;
            _runSeedFixed = _host.FixedSeed.HasValue;
            if (_pendingDespawnDistance.HasValue && IsRunActive)
            {
                _host.Commands.Enqueue(PendingCommand.SetDespawnDistance(_pendingDespawnDistance.Value));
                _pendingDespawnDistance = null;
            }
        }

        private void OnRunEnded(RunOutcome outcome)
        {
            ResetTimeScale();
        }

        private void ApplyDespawnDistance(float distance)
        {
            if (IsRunActive)
            {
                _host.Commands.Enqueue(PendingCommand.SetDespawnDistance(distance));
                _pendingDespawnDistance = null;
            }
            else
            {
                // The queue is cleared by the run reset; queue D right after the next run starts instead.
                _pendingDespawnDistance = distance;
            }
        }

        private static bool IsSpawnableMonster(GameData data, string id)
        {
            return data.TryGetMonster(id, out MonsterDefinition definition) && definition.Behavior != MonsterBehavior.Boss;
        }

        private static string FirstSpawnableMonster(GameData data)
        {
            for (int i = 0; i < data.Monsters.Count; i++)
            {
                if (data.Monsters[i].Behavior != MonsterBehavior.Boss)
                {
                    return data.Monsters[i].Id;
                }
            }

            return null;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }

        private static string OnOff(bool on)
        {
            return on ? "on" : "off";
        }

        // Records a visualization toggle made in the overlay (a user action, so it gets a log line like every other one).
        internal void NoteVisualization(string option, bool on)
        {
            Done(option, OnOff(on) + ".");
        }

        // "#<run> <step> <command>: <message>" (5.15.5 default), numbers in InvariantCulture.
        internal static string FormatLine(int run, long step, string command, string message)
        {
            return "#" + run.ToString(CultureInfo.InvariantCulture) + " " + step.ToString(CultureInfo.InvariantCulture) + " " + command
                + ": " + message;
        }

        // Chest line text: upgrade "<name> Lv a->b" (U+2192; weapon or passive), evolution "<name> (evolved)", heal "Heal +x"
        // (F1); name = the data display name (passive data for PassiveUpgraded, weapon data otherwise), or the id when missing.
        internal static string ChestText(in ChestResult result, GameData data)
        {
            if (result.Kind == ChestEffect.Healed)
            {
                return "Heal +" + result.HealAmount.ToString("F1", CultureInfo.InvariantCulture);
            }

            string name = result.TargetId;
            string displayName = null;
            if (data != null && result.TargetId != null)
            {
                if (result.Kind == ChestEffect.PassiveUpgraded)
                {
                    displayName = data.TryGetPassive(result.TargetId, out PassiveDefinition passive) ? passive.DisplayName : null;
                }
                else
                {
                    displayName = data.TryGetWeapon(result.TargetId, out WeaponDefinition weapon) ? weapon.DisplayName : null;
                }
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                name = displayName;
            }

            return result.Kind == ChestEffect.Evolved
                ? name + " (evolved)"
                : name + " Lv " + result.FromLevel.ToString(CultureInfo.InvariantCulture) + "\u2192"
                    + result.ToLevel.ToString(CultureInfo.InvariantCulture);
        }

        // Moves chest results newer than the last one seen into the log, oldest first (de-dup key: ChestResult.Sequence). Runs
        // at the top of Poll (before a run reset clears the list) and before every command line, so a command recorded at
        // step N comes before the chest results of step N.
        private void PullChestResults()
        {
            ChestResultLog results = _host.ChestResults;
            if (results == null || results.LastSequence <= _lastChestSequence)
            {
                return;
            }

            GameData data = _host.Data.Current;
            for (int i = 0; i < results.Count; i++)
            {
                ChestResult result = results[i];
                if (result.Sequence > _lastChestSequence)
                {
                    _commandLog.Add(FormatLine(_runNumber, result.Step, "Chest", ChestText(in result, data)));
                }
            }

            _lastChestSequence = results.LastSequence;
        }

        private static bool ApplyDefaultZoom()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            camera.orthographicSize = GameRules.CameraOrthographicSize;
            return true;
        }

        private bool Done(string command, string message)
        {
            PullChestResults();
            LastMessage = message;
            _commandLog.Add(FormatLine(_runNumber, _host.Clock.Step, command, message));
            return true;
        }

        private bool Refuse(string command, string message)
        {
            PullChestResults();
            LastMessage = message;
            _commandLog.Add(FormatLine(_runNumber, _host.Clock.Step, command, "refused - " + message));
            DevLog.Info("Refused: " + message);
            return false;
        }
    }
}
