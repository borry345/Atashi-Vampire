using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Dev;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Survivor.Tests.Dev
{
    /// Dev mode on the real Main.unity (TAC-F19-1..4, 6, 13..19, 5.15): attach/no-attach, overlay and status panel, time
    /// controls, state and enemy controls, LastTargetId, visualizer callback, the wave 6b conveniences (loadout 0..5, spawn
    /// toggle, zoom, homing target lines, command log), the wave 7 passive loadout and console window, and the F19 determinism
    /// criterion.
    public sealed class DevModePlayTests : DevPmFixture
    {
        private DevModeRoot _root;

        private DevController Controller => _root.Controller;

        [UnityTest]
        public IEnumerator WithoutDevArgument_NoDevObjectExists()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(DevModeActivation.EditorPrefsKey, false))
            {
                Assert.Ignore("The editor's Dev Mode On Play toggle is on; the no-argument path cannot be checked here.");
            }
#endif
            yield return LoadMain();
            Assert.IsNull(DevModeEntry.Run(new[] { "Survivor.exe", "-batchmode" }));
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<DevModeRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, Object.FindObjectsByType<DevOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, Object.FindObjectsByType<DevVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.IsTrue(Host.Rules.RecordBestTime, "nothing touched the rules");
        }

        [UnityTest]
        public IEnumerator WithDevArgument_AttachesOnceAfterAwake()
        {
            yield return LoadMain();
            DevModeRoot root = DevModeEntry.Run(new[] { "Survivor.exe", "--DEV" });
            Assert.IsNotNull(root, "FindAnyObjectByType found the GameBootstrap after its Awake");
            Assert.AreSame(Host, root.Controller.Host);
            Assert.IsFalse(Host.Rules.RecordBestTime, "attach turns best-time recording off");
            Assert.IsFalse(root.Overlay.enabled, "overlay starts hidden");
            Assert.IsFalse(root.Visualizer.enabled, "visualizer starts off");
            Assert.IsFalse(root.Visualizer.IsSubscribed);
            Assert.AreSame(root, DevModeEntry.Run(new[] { "--dev" }), "a second request reuses the root");
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<DevModeRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Overlay_ToggleShowsStatusPanel()
        {
            yield return AttachAndStart();
            Assert.IsFalse(_root.Overlay.enabled);
            _root.ToggleOverlay();
            Assert.IsTrue(_root.Overlay.enabled, "F1 path opens the overlay");
            yield return Frames(30);
            Assert.Greater(_root.Overlay.ActualSpeed, 0.5f, "actual speed sampled while open");
            var lines = new List<string>();
            _root.Overlay.BuildStatusLines(lines);
            Assert.AreEqual("HP mult: x" + R31Text(), lines[5], "R31 value of the current step (TAC-F19-14)");
            Assert.AreEqual("HP mult: x1.0", lines[5]);
            StringAssert.StartsWith("Time 00:", lines[0]);
            StringAssert.StartsWith("Seed 4242 (fixed)", lines[1]);
            StringAssert.StartsWith("Pool CustomPool  Target SpatialHash  Tick CentralTick  Collision SelfSquaredDistance", lines[3]);
            int weapons = lines.IndexOf("Weapons:");
            Host.Data.Current.TryGetWeapon("W1", out var w1);
            Assert.AreEqual("  " + w1.DisplayName + " Lv 1", lines[weapons + 1], "starting weapon with its data display name");
            Assert.AreEqual("Passives:", lines[weapons + 2]);
            _root.ToggleOverlay();
            Assert.IsFalse(_root.Overlay.enabled, "closed overlay = disabled component (no OnGUI, no sampling)");
        }

        [UnityTest]
        public IEnumerator TimeScale_ClampsAndKeepsThreeStepsPerFrame()
        {
            yield return AttachAndStart();
            Assert.AreEqual(3f, Controller.SetTimeScale(5f));
            Assert.AreEqual(3f, Time.timeScale);
            Assert.That(Math.Abs(Time.maximumDeltaTime - 0.05f / 3f), Is.LessThanOrEqualTo(1e-6f));
            Assert.AreEqual(0.25f, Controller.SetTimeScale(0.1f));
            Assert.That(Math.Abs(Time.maximumDeltaTime - 0.2f), Is.LessThanOrEqualTo(1e-6f));
            Controller.SetTimeScale(1f);
            Assert.That(Math.Abs(Time.maximumDeltaTime - 0.05f), Is.LessThanOrEqualTo(1e-6f));
            Controller.SetTimeScale(2f);
            Host.Flow.TogglePause();
            Assert.IsTrue(Controller.TryStartNewRun(), "from Paused");
            Assert.AreEqual(1f, Time.timeScale, "run start resets the scale");
            Assert.That(Math.Abs(Time.maximumDeltaTime - 0.05f), Is.LessThanOrEqualTo(1e-6f));
        }

        [UnityTest]
        public IEnumerator StartNewRun_RefusedWhilePlaying()
        {
            yield return AttachAndStart();
            yield return WaitFor(() => Host.Clock.Step >= 10, "step 10");
            Assert.IsFalse(Controller.TryStartNewRun());
            StringAssert.Contains("Title, Result or Paused", Controller.LastMessage);
            yield return null;
            Assert.Greater(Host.Clock.Step, 10L, "the run went on");
        }

        [UnityTest]
        public IEnumerator Jump_BackwardRefused_ForwardApplied()
        {
            yield return AttachAndStart();
            yield return WaitFor(() => Host.Clock.Step >= 30, "step 30");
            long before = Host.Clock.Step;
            Assert.IsFalse(Controller.TryJumpToStep(10));
            StringAssert.Contains("forward only", Controller.LastMessage);
            Assert.IsTrue(Controller.TryJumpToStep(before + 600));
            yield return WaitFor(() => Host.Clock.Step > before + 600, "jumped");
            Assert.AreEqual(FlowState.Playing, Host.Flow.State);
        }

        [UnityTest]
        public IEnumerator StepOnce_RunsExactlyOneStep()
        {
            yield return AttachAndStart();
            yield return WaitFor(() => Host.Clock.Step >= 5, "step 5");
            Assert.IsTrue(Controller.TryStepOnce(), "while running: stop after the next step");
            yield return WaitFor(() => Controller.IsSingleStepMode, "one-step mode");
            long held = Host.Clock.Step;
            yield return Frames(5);
            Assert.AreEqual(held, Host.Clock.Step, "gate held");
            Assert.IsTrue(Controller.TryStepOnce());
            yield return WaitFor(() => Host.Clock.Step == held + 1 && Controller.IsSingleStepMode, "one step");
            yield return Frames(5);
            Assert.AreEqual(held + 1, Host.Clock.Step, "exactly one step");
            Assert.AreEqual(FlowState.Playing, Host.Flow.State);
            Assert.IsTrue(Controller.TryResume());
            yield return WaitFor(() => Host.Clock.Step > held + 3, "running again");
        }

        [UnityTest]
        public IEnumerator SpawnMonstersNow_AddsExactlyThatMany()
        {
            yield return AttachAndStart(configure: c =>
            {
                Assert.IsTrue(c.TryConfigureSpawns(true, null, 0));
                c.SetWeaponsEnabled(false);
            });
            yield return WaitFor(() => Host.Clock.Step >= 3, "step 3");
            Assert.AreEqual(0, LiveNonBossMonsters());
            Assert.IsFalse(Controller.TrySpawnMonstersNow("M1", 0));
            Assert.IsFalse(Controller.TrySpawnMonstersNow("M4", 1), "boss id refused");
            Assert.IsFalse(Controller.TrySpawnMonstersNow("NOPE", 1));
            Assert.AreEqual(0, Host.Commands.PendingCount, "refusals queue nothing");
            Assert.IsTrue(Controller.TrySpawnMonstersNow("M1", 10));
            long at = Host.Clock.Step;
            yield return WaitFor(() => Host.Clock.Step >= at + 2, "two steps");
            Assert.AreEqual(10, LiveNonBossMonsters());
            Assert.AreEqual(10, Host.LiveMonsterCount, "host count after the next BuildIndex");
        }

        [UnityTest]
        public IEnumerator HoldN_RespawnsKills_AndSetsDespawnByR26()
        {
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 30)));
            yield return WaitFor(() => Host.Session.KillPipeline.KillCount >= 3, "some kills", 3000);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(30, LiveNonBossMonsters(), "topped up at the end of each step");
                yield return null;
            }

            Assert.IsTrue(Host.Rules.DespawnDistanceOverridden);
            Assert.AreEqual(45f, Host.Rules.DespawnDistance, 1e-4f);
            Assert.IsTrue(Controller.TryConfigureSpawns(true, null, 1600));
            Assert.AreEqual(int.MaxValue, Host.Rules.MonsterCap, "N above 400 lifts the cap");
            yield return Frames(2);
            Assert.AreEqual(90f, Host.Rules.DespawnDistance, 1e-4f, "R26: 45 * sqrt(1600 / 400)");
        }

        [UnityTest]
        public IEnumerator SetLoadout_RefusalsQueueNothing()
        {
            yield return AttachAndStart();
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("W1", 99) }));
            Assert.AreEqual("W1 has levels 0..5.", Controller.LastMessage);
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W1", 2) }));
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("NOPE", 1) }));
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("E1", 1) }));
            Assert.AreEqual("E1 is an evolved weapon; evolution is the only way to get it.", Controller.LastMessage);
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("W1", 0) }));
            Assert.AreEqual("Loadout would leave no weapon.", Controller.LastMessage);
            StringAssert.EndsWith(" Loadout: refused - Loadout would leave no weapon.", Controller.CommandLog.NewestFirst(0));
            Assert.AreEqual(0, Host.Commands.PendingCount);
            Assert.AreEqual("W1", Host.Session.Weapons.Slots[0].Id, "the loadout is unchanged");
        }

        [UnityTest]
        public IEnumerator SetLoadout_KeepsInstances_RemovesOrbs_FillsTheLowestEmptySlot()
        {
            // TAC-F19-15 (PM). No monsters, so W1 never fires and its NextFireStep only changes with a new instance.
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 0)));
            yield return WaitFor(() => Host.Clock.Step >= 3, "step 3");
            IReadOnlyList<WeaponInstance> slots = Host.Session.Weapons.Slots;
            WeaponInstance w1 = slots[0];
            long w1Next = w1.NextFireStep;

            Assert.IsTrue(Controller.TrySetLoadout(Loadout(("W1", 1), ("W2", 3))));
            yield return WaitFor(() => slots[1] != null && CountOrbs() == 3, "W2 Lv 3 with 3 orbs", 300);
            WeaponInstance w2 = slots[1];
            Assert.AreSame(w1, slots[0], "W1 kept its instance");
            Assert.AreEqual(w1Next, w1.NextFireStep);

            Assert.IsTrue(Controller.TrySetLoadout(Loadout(("W1", 1), ("W2", 1))));
            yield return WaitFor(() => w2.Level == 1 && CountOrbs() == 2, "W2 Lv 1: the surplus orb is collected", 300);
            Assert.AreSame(w2, slots[1], "lowering the level keeps the instance");

            Assert.IsTrue(Controller.TrySetLoadout(Loadout(("W1", 1), ("W2", 0), ("W3", 2))));
            yield return WaitFor(() => slots[1] != null && slots[1].Id == "W3", "W3 in slot 1", 300);
            Assert.AreSame(w1, slots[0], "W1 kept its instance");
            Assert.AreEqual(w1Next, w1.NextFireStep, "W1 cooldown untouched");
            Assert.AreEqual(2, slots[1].Level);
            Assert.IsNull(slots[2]);
            Assert.IsNull(slots[3]);
            Assert.AreEqual(0, CountOrbs(), "W2 removed with its orbs");
            var lines = new List<string>();
            _root.Overlay.BuildStatusLines(lines);
            int weapons = lines.IndexOf("Weapons:");
            Assert.AreEqual("  " + w1.Definition.DisplayName + " Lv 1", lines[weapons + 1]);
            Assert.AreEqual("  " + slots[1].Definition.DisplayName + " Lv 2", lines[weapons + 2]);
            Assert.AreEqual("Passives:", lines[weapons + 3]);
        }

        [UnityTest]
        public IEnumerator SpawnBossNow_OnePerRun()
        {
            yield return AttachAndStart(configure: c => c.SetWeaponsEnabled(false));
            Assert.IsTrue(Controller.TrySpawnBossNow());
            yield return WaitFor(() => Host.BossSpawnedThisRun, "boss spawned");
            Assert.IsFalse(Controller.TrySpawnBossNow());
            Assert.AreEqual("Boss: one per run.", Controller.LastMessage);
            Assert.AreEqual(0, Host.Commands.PendingCount, "no second command queued");
        }

        [UnityTest]
        public IEnumerator LastTargetId_SetOnceTheWeaponFires()
        {
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 0)));
            WeaponInstance w1 = Host.Session.Weapons.Slots[0];
            Assert.IsFalse(w1.LastTargetId.IsValid, "no target before any monster");
            yield return WaitFor(() => Host.Clock.Step >= 3, "step 3");
            Assert.IsFalse(w1.LastTargetId.IsValid, "no monster, no target");
            Assert.IsTrue(Controller.TrySpawnMonstersNow("M1", 5));
            yield return WaitFor(() => w1.LastTargetId.IsValid, "a target was chosen", 600);
            Assert.AreEqual(EntityCategory.Monster, MonsterCategoryOf(w1.LastTargetId), "the target is a live monster (projectiles need travel time)");
        }

        [UnityTest]
        public IEnumerator TargetSwitch_TrackedForTheStatusPanel()
        {
            yield return AttachAndStart();
            Controller.SetTarget(TargetProviderKind.BruteForce);
            Assert.AreEqual(TargetProviderKind.BruteForce, Controller.CurrentTarget);
            Assert.AreEqual(TargetProviderKind.BruteForce, Host.NextRunSelection.Target);
            var lines = new List<string>();
            _root.Overlay.BuildStatusLines(lines);
            StringAssert.Contains("Target BruteForce", lines[3]);
            yield return Frames(3);
            Assert.AreEqual(FlowState.Playing, Host.Flow.State, "the switch ran without an error close");
        }

        [UnityTest]
        public IEnumerator Visualizer_AllOffRemovesTheCallback()
        {
            yield return AttachAndStart();
            DevVisualizer visualizer = _root.Visualizer;
            visualizer.ShowGrid = true;
            visualizer.ShowTargets = true;
            Assert.IsTrue(visualizer.enabled);
            Assert.IsTrue(visualizer.IsSubscribed);
            yield return Frames(3);
            visualizer.ShowGrid = false;
            Assert.IsTrue(visualizer.IsSubscribed, "one option still on");
            visualizer.ShowTargets = false;
            Assert.IsFalse(visualizer.enabled);
            Assert.IsFalse(visualizer.IsSubscribed);
        }

        [UnityTest]
        public IEnumerator Determinism_SameSeedSameSettings_SameHash()
        {
            // F19 criterion: seed 4242, level-up off, auto path r6 speed 4, invulnerable, hold N = 100; 600 steps twice.
            // Run 1 opens and closes the overlay (TAC-F19-13: the panel must not change the state).
            ulong first = 0;
            int firstLive = -1;
            yield return DeterministicRun(true, (hash, live) => { first = hash; firstLive = live; });
            DevModeEntry.Detach();
            ulong second = 0;
            int secondLive = -2;
            yield return DeterministicRun(false, (hash, live) => { second = hash; secondLive = live; });
            Debug.Log("[DevDeterminism] hash=0x" + first.ToString("X16", CultureInfo.InvariantCulture) + " live="
                + firstLive.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual(first, second, "state hash after 600 steps");
            Assert.AreEqual(firstLive, secondLive, "live monster count after 600 steps");
            Assert.Greater(firstLive, 0);
        }

        [UnityTest]
        public IEnumerator StatusPanel_HpMultiplierIsR31()
        {
            // TAC-F19-14 (value part; the HUD comparison waits for the FLOW panel).
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 0)));
            var lines = new List<string>();
            foreach ((long target, string expected) in new[] { (7300L, "x1.5"), (19000L, "x2.2"), (40000L, "x3.0") })
            {
                Assert.IsTrue(Controller.TryJumpToStep(target));
                yield return WaitFor(() => Host.Clock.Step > target, "jump to " + target);
                _root.Overlay.BuildStatusLines(lines);
                Assert.AreEqual("HP mult: x" + R31Text(), lines[5]);
                Assert.AreEqual("HP mult: " + expected, lines[5], "step " + Host.Clock.Step);
            }
        }

        [UnityTest]
        public IEnumerator SpawnToggle_RefusedOutsideARun_StopsAndResumesTableSpawns()
        {
            // TAC-F19-16.
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            Assert.IsFalse(Controller.IsRunActive);
            Assert.IsFalse(Controller.TrySetSpawnsEnabled(false));
            Assert.AreEqual("Spawn toggle needs a run in progress.", Controller.LastMessage);
            Assert.AreEqual(0, Host.Commands.PendingCount);
            Assert.IsTrue(Controller.SpawnsEnabled);
            Controller.SetFixedSeed(4242UL);
            Controller.SetInvulnerable(true);
            Controller.SetLevelUpEnabled(false);
            Controller.SetWeaponsEnabled(false);
            ISpawnPolicy policy = Host.SpawnPolicyOverride;
            Assert.IsTrue(Controller.TryStartNewRun());
            yield return WaitFor(() => Host.Clock.Step >= 130 && LiveNonBossMonsters() > 0, "table spawns", 600);

            Assert.IsTrue(Controller.TrySetSpawnsEnabled(false));
            Assert.AreEqual(1, Host.Commands.PendingCount, "one SetSpawnsEnabled command");
            Assert.IsFalse(Controller.SpawnsEnabled);
            long at = Host.Clock.Step;
            yield return WaitFor(() => Host.Clock.Step >= at + 2, "applied");
            int held = LiveNonBossMonsters();
            yield return WaitFor(() => Host.Clock.Step >= at + 180, "three seconds");
            Assert.AreEqual(held, LiveNonBossMonsters(), "no table spawns while off");
            Assert.AreSame(policy, Host.SpawnPolicyOverride, "the next-run spawn policy is untouched");

            Assert.IsTrue(Controller.TrySetSpawnsEnabled(true));
            Assert.IsTrue(Controller.SpawnsEnabled);
            yield return WaitFor(() => LiveNonBossMonsters() > held, "table spawns again", 600);

            Assert.IsTrue(Controller.TrySetSpawnsEnabled(false));
            Host.Flow.TogglePause();
            Assert.IsTrue(Controller.TryStartNewRun());
            Assert.IsTrue(Controller.SpawnsEnabled, "every run starts with spawns on");
            Assert.AreSame(policy, Host.SpawnPolicyOverride);
        }

        [UnityTest]
        public IEnumerator Zoom_ClampsResetsAndReturnsToNineOnDetach()
        {
            // TAC-F19-17 (PM).
            yield return AttachAndStart();
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.AreEqual(9f, camera.orthographicSize);
            Assert.IsTrue(Controller.TrySetZoom(12f));
            Assert.AreEqual(12f, camera.orthographicSize);
            Assert.AreEqual("Zoom 12.", Controller.LastMessage);
            Controller.ResetZoom();
            Assert.AreEqual(9f, camera.orthographicSize);
            Assert.IsTrue(Controller.TrySetZoom(1f));
            Assert.AreEqual(4.5f, camera.orthographicSize);
            Assert.IsTrue(Controller.TrySetZoom(30f));
            Assert.AreEqual(27f, camera.orthographicSize);
            Assert.IsTrue(Controller.TrySetZoom(float.NaN));
            Assert.AreEqual(9f, camera.orthographicSize);

            Assert.IsTrue(Controller.TrySetZoom(20f));
            Host.Flow.TogglePause();
            Assert.IsTrue(Controller.TryStartNewRun());
            yield return null;
            Assert.AreEqual(20f, camera.orthographicSize, "a run reset keeps the zoom");

            DevModeEntry.Detach();
            yield return null;
            Assert.AreEqual(9f, camera.orthographicSize, "leaving dev mode restores 9");
        }

        [UnityTest]
        public IEnumerator TargetLines_HomingProjectilesOnly()
        {
            // TAC-F19-18 (drawing part): one extra line per live homing projectile with a live target; none for W1.
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 0)));
            yield return WaitFor(() => Host.Clock.Step >= 3, "step 3");
            Assert.IsTrue(Controller.TrySetLoadout(Loadout(("W1", 0), ("W4", 1))));
            yield return WaitFor(() => Host.Session.Weapons.Slots[0] != null && Host.Session.Weapons.Slots[0].Id == "W4", "W4 in slot 0", 300);
            Assert.IsTrue(Controller.TrySpawnMonstersNow("M1", 20));
            yield return WaitFor(() => HomingLines() >= 1, "a homing projectile tracking a live monster", 1200);
            var lines = new List<Vector2>();
            DevVisualizer.CollectTargetLines(Host.Session, Host.Session.Player.Position, lines);
            Assert.AreEqual(2 * (WeaponLines() + HomingLines()), lines.Count, "player->target per weapon + projectile->target per homing projectile");

            Assert.IsTrue(Controller.TrySetLoadout(Loadout(("W1", 1), ("W4", 0))));
            Assert.IsTrue(Controller.TrySpawnMonstersNow("M1", 20));
            yield return WaitFor(() => CountProjectiles(true) == 0 && CountProjectiles(false) > 0, "only W1 projectiles in flight", 1200);
            DevVisualizer.CollectTargetLines(Host.Session, Host.Session.Player.Position, lines);
            Assert.AreEqual(2 * WeaponLines(), lines.Count, "no projectile lines for W1");
            foreach (SimEntity entity in Host.Session.Lifecycle.ActiveEntities)
            {
                if (entity is PlayerProjectile projectile)
                {
                    Assert.IsFalse(projectile.LastTargetId.IsValid, "W1 projectiles never track");
                }
            }
        }

        [UnityTest]
        public IEnumerator CommandLog_OneLinePerAction_ChestLinesOnce_KeptAcrossRuns()
        {
            // TAC-F19-19 (PM).
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            DevCommandLog log = Controller.CommandLog;
            Assert.AreEqual(0, Controller.RunNumber);
            Controller.SetFixedSeed(4242UL);
            Assert.AreEqual("#0 0 FixedSeed: Seed for the next run: 4242.", log.NewestFirst(0));
            Controller.SetInvulnerable(true);
            Controller.SetLevelUpEnabled(false);
            Assert.IsTrue(Controller.TryConfigureSpawns(true, null, 0));
            Assert.AreEqual(4, log.Count);
            Host.InputOverride = new MoveRightInput();
            Assert.IsTrue(Controller.TryStartNewRun());
            Assert.AreEqual(1, Controller.RunNumber);
            Assert.AreEqual("#1 0 StartNewRun: New run started.", log.NewestFirst(0));
            yield return WaitFor(() => Host.Clock.Step >= 5, "step 5");

            long step = Host.Clock.Step;
            Assert.IsFalse(Controller.TrySetHealth(float.NaN));
            Assert.AreEqual("#1 " + step + " Health: refused - Health must be a finite number.", log.NewestFirst(0));
            Controller.SetTimeScale(1f);
            Assert.AreEqual("#1 " + step + " TimeScale: Time scale x1.00.", log.NewestFirst(0));
            Assert.AreEqual("Time scale x1.00.", Controller.LastMessage);
            Assert.IsTrue(Controller.TrySpawnChestNow());
            Assert.AreEqual("#1 " + step + " SpawnChestNow: Chest queued.", log.NewestFirst(0));
            Assert.AreEqual(8, log.Count);

            yield return WaitFor(() => Host.ChestResults.LastSequence >= 1, "the chest opened", 600);
            ChestResult result = Host.ChestResults[Host.ChestResults.Count - 1];
            Assert.IsTrue(Controller.TryLevelUpNow(), "a command right after the chest result");
            yield return null;
            string chestLine = DevController.FormatLine(1, result.Step, "Chest", DevController.ChestText(in result, Host.Data.Current));
            Assert.AreEqual(1, CountLines(log, chestLine), "chest line added once");
            Assert.AreEqual(1, CountLines(log, " Chest: "), "one chest line");
            Assert.Greater(IndexOf(log, " LevelUpNow: "), -1);
            Assert.Greater(IndexOf(log, " SpawnChestNow: "), IndexOf(log, " Chest: "), "the chest line is newer than the chest command");
            Assert.Greater(IndexOf(log, " Chest: "), IndexOf(log, " LevelUpNow: "), "the result happened before the later command");
            AssertStepsNonDecreasing(log);
            yield return WaitFor(() => Host.Clock.Step >= result.Step + 10, "ten more steps");
            Assert.AreEqual(1, CountLines(log, " Chest: "), "no duplicate after more polls");

            Host.Flow.TogglePause();
            Assert.IsTrue(Controller.TryStartNewRun());
            Assert.AreEqual(2, Controller.RunNumber);
            Assert.AreEqual("#2 0 StartNewRun: New run started.", log.NewestFirst(0));
            Assert.AreEqual(1, CountLines(log, chestLine), "the last run's lines stay");
            Assert.AreEqual(0, Host.ChestResults.Count, "the simulation list was cleared by the new run");
            yield return Frames(3);
            Assert.AreEqual(1, CountLines(log, " Chest: "), "a cleared list adds nothing");
        }

        [UnityTest]
        public IEnumerator PassiveLoadout_KeepsTheSlot_RemovesZero_FillsTheLowestEmptySlot_AllZeroAllowed()
        {
            // TAC-F19-20 1 and 2 (PM, Dev path).
            yield return AttachAndStart(configure: c => Assert.IsTrue(c.TryConfigureSpawns(true, null, 0)));
            IReadOnlyList<PassiveInstance> slots = Host.Session.Passives.Slots;
            Assert.IsTrue(Controller.TrySetPassiveLoadout(Passives(("P1", 2), ("P2", 1))), Controller.LastMessage);
            Assert.AreEqual(1, Host.Commands.PendingCount, "one command queued");
            yield return WaitFor(() => slots[0] != null && slots[1] != null, "P1 and P2 owned", 300);
            PassiveInstance p1 = slots[0];
            Assert.AreEqual("P1", p1.Id);
            Assert.AreEqual(2, p1.Level);
            Assert.AreEqual("P2", slots[1].Id);
            Assert.IsNull(slots[2]);

            Assert.IsTrue(Controller.TrySetPassiveLoadout(Passives(("P1", 3), ("P2", 0), ("P3", 1))), Controller.LastMessage);
            Assert.AreEqual(1, Host.Commands.PendingCount, "one command queued");
            StringAssert.EndsWith(" PassiveLoadout: Passive loadout of 2 passive(s) queued.", Controller.CommandLog.NewestFirst(0));
            yield return WaitFor(() => slots[1] != null && slots[1].Id == "P3", "P3 in the slot P2 left", 300);
            Assert.AreSame(p1, slots[0], "P1 kept its instance and slot");
            Assert.AreEqual(3, p1.Level);
            Assert.AreEqual(1, slots[1].Level);
            Assert.IsNull(slots[2]);
            yield return null;
            var lines = new List<string>();
            _root.Overlay.BuildStatusLines(lines);
            int passives = lines.IndexOf("Passives:");
            Assert.Greater(passives, -1);
            Assert.AreEqual(DevStatusFormatter.PassiveLine(p1.Definition.DisplayName, "P1", 3), lines[passives + 1]);
            Assert.AreEqual(DevStatusFormatter.PassiveLine(slots[1].Definition.DisplayName, "P3", 1), lines[passives + 2]);
            Assert.IsTrue(passives + 3 == lines.Count || !lines[passives + 3].StartsWith("  "), "exactly two passive lines");

            Assert.IsTrue(Controller.TrySetPassiveLoadout(Passives(("P1", 0), ("P3", 0))), "all zero is not refused");
            StringAssert.EndsWith(" PassiveLoadout: Passive loadout of 0 passive(s) queued.", Controller.CommandLog.NewestFirst(0));
            yield return WaitFor(() => slots[0] == null && slots[1] == null && slots[2] == null, "no passive left", 300);
            Assert.AreEqual(FlowState.Playing, Host.Flow.State, "the run goes on without passives");
            Assert.AreEqual("W1", Host.Session.Weapons.Slots[0].Id, "weapons untouched");
        }

        [UnityTest]
        public IEnumerator PassiveLoadout_RefusalsQueueNothing_AndNeedARun()
        {
            // TAC-F19-20 3 and 4 (PM, Dev path).
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            Assert.IsFalse(Controller.TrySetPassiveLoadout(Passives(("P1", 1))));
            Assert.AreEqual("Passive loadout needs a run in progress.", Controller.LastMessage);
            StringAssert.EndsWith(" PassiveLoadout: refused - Passive loadout needs a run in progress.", Controller.CommandLog.NewestFirst(0));
            Controller.SetInvulnerable(true);
            Controller.SetLevelUpEnabled(false);
            Assert.IsTrue(Controller.TryStartNewRun(), Controller.LastMessage);
            yield return null;

            Assert.IsFalse(Controller.TrySetPassiveLoadout(null));
            Assert.AreEqual("Passive loadout needs a list of passives.", Controller.LastMessage);
            Assert.IsFalse(Controller.TrySetPassiveLoadout(Passives(("NOPE", 1))));
            Assert.AreEqual("Unknown passive 'NOPE'.", Controller.LastMessage);
            Assert.IsFalse(Controller.TrySetPassiveLoadout(Passives(("P1", 6))));
            Assert.AreEqual("P1 has levels 0..5.", Controller.LastMessage);
            Assert.IsFalse(Controller.TrySetPassiveLoadout(Passives(("P1", 1), ("P1", 2))));
            Assert.AreEqual("Passive 'P1' appears twice.", Controller.LastMessage);
            StringAssert.EndsWith(" PassiveLoadout: refused - Passive 'P1' appears twice.", Controller.CommandLog.NewestFirst(0));
            Assert.AreEqual(0, Host.Commands.PendingCount, "no command on refusal");
            yield return Frames(2);
            foreach (PassiveInstance passive in Host.Session.Passives.Slots)
            {
                Assert.IsNull(passive, "the passives are unchanged");
            }
        }

        [UnityTest]
        public IEnumerator Console_ClosedStillRecords_OpensWithTheLines_HiddenWithTheOverlay()
        {
            // TAC-F19-21 (PM; the IMGUI window itself is checked through its line source and its toggle state).
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            DevOverlay overlay = _root.Overlay;
            Assert.IsFalse(overlay.ConsoleOpen, "the console starts closed");
            Assert.AreNotEqual(DevOverlay.MainWindowId, DevOverlay.ConsoleWindowId, "separate IMGUI window");
            Assert.AreEqual("No table spawns (next run, for Hold N)", DevOverlay.NoTableSpawnsLabel);
            _root.ToggleOverlay();
            Assert.IsTrue(overlay.enabled);

            // Commands and a chest while the console is closed.
            DevCommandLog log = Controller.CommandLog;
            Controller.SetFixedSeed(4242UL);
            Controller.SetInvulnerable(true);
            Controller.SetLevelUpEnabled(false);
            Assert.IsTrue(Controller.TryConfigureSpawns(true, null, 0));
            Host.InputOverride = new MoveRightInput();
            Assert.IsTrue(Controller.TryStartNewRun());
            yield return null;
            Assert.IsTrue(Controller.TrySpawnChestNow());
            yield return WaitFor(() => Host.ChestResults.LastSequence >= 1, "the chest opened", 600);
            yield return null;
            Assert.IsFalse(overlay.ConsoleOpen);
            Assert.AreEqual(1, CountLines(log, " Chest: "), "the chest line was recorded while closed");
            Assert.AreEqual(1, CountLines(log, " SpawnChestNow: "));
            Assert.AreEqual(1, CountLines(log, " StartNewRun: "));

            overlay.ConsoleOpen = true;
            var console = new List<string>();
            DevOverlay.BuildConsoleLines(log, console);
            Assert.AreEqual(log.Count, console.Count);
            for (int i = 0; i < log.Count; i++)
            {
                Assert.AreEqual(log.NewestFirst(i), console[i], "newest line first");
            }

            StringAssert.Contains(" Chest: ", console[0], "the latest line is on top");
            var status = new List<string>();
            overlay.BuildStatusLines(status);
            foreach (string line in console)
            {
                CollectionAssert.DoesNotContain(status, line, "no log line in the main window");
            }

            _root.ToggleOverlay();
            Assert.IsFalse(overlay.enabled, "F1 hides the overlay and with it the console (drawn only in its OnGUI)");
            Assert.IsTrue(overlay.ConsoleOpen, "the toggle state is kept while hidden");
            _root.ToggleOverlay();
            Assert.IsTrue(overlay.enabled);
            Assert.IsTrue(overlay.ConsoleOpen, "reopening shows the console again");
            Assert.Throws<ArgumentNullException>(() => DevOverlay.BuildConsoleLines(null, console));
            Assert.Throws<ArgumentNullException>(() => DevOverlay.BuildConsoleLines(log, null));
        }

        private IEnumerator DeterministicRun(bool toggleOverlay, Action<ulong, int> result)
        {
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            DevController c = _root.Controller;
            c.SetFixedSeed(4242UL);
            c.SetLevelUpEnabled(false);
            Assert.IsTrue(c.TrySetAutoPath(true, 6f, 4f));
            c.SetInvulnerable(true);
            Assert.IsTrue(c.TryConfigureSpawns(true, null, 100));
            Host.Rules.StopAtStep = 600;
            Assert.IsTrue(c.TryStartNewRun());
            bool opened = false;
            int frames = 0;
            while (!(Host.Clock.CloseReason == GateCloseReason.StopAtStep && !Host.Clock.IsRunning))
            {
                Assert.Less(++frames, MaxFrames, "run did not reach 600 steps");
                Assert.AreEqual(FlowState.Playing, Host.Flow.State, "no level-up, death or error");
                if (toggleOverlay && !opened && Host.Clock.Step >= 100)
                {
                    _root.ToggleOverlay();
                    _root.Overlay.ConsoleOpen = true;
                    opened = true;
                    Assert.IsTrue(c.TrySetZoom(20f));
                    _root.Visualizer.ShowTargets = true;
                    c.NoteVisualization("ShowTargets", true);
                }

                if (toggleOverlay && opened && _root.Overlay.enabled && Host.Clock.Step >= 300)
                {
                    var lines = new List<string>();
                    _root.Overlay.BuildStatusLines(lines);
                    _root.ToggleOverlay();
                }

                yield return null;
            }

            Assert.AreEqual(600L, Host.Clock.Step);
            result(SimulationStateHash.Compute(Host.Session), Host.LiveMonsterCount);
        }

        private IEnumerator AttachAndStart(Action<DevController> configure = null)
        {
            yield return LoadMain();
            _root = DevModeEntry.Attach(Host);
            _root.Controller.SetFixedSeed(4242UL);
            _root.Controller.SetInvulnerable(true);
            _root.Controller.SetLevelUpEnabled(false);
            configure?.Invoke(_root.Controller);
            Assert.IsTrue(_root.Controller.TryStartNewRun(), _root.Controller.LastMessage);
            yield return null;
        }

        private static IReadOnlyList<WeaponLoadoutEntry> Loadout(params (string id, int level)[] entries)
        {
            var loadout = new List<WeaponLoadoutEntry>(entries.Length);
            foreach ((string id, int level) in entries)
            {
                loadout.Add(new WeaponLoadoutEntry(id, level));
            }

            return loadout;
        }

        private static IReadOnlyList<PassiveLoadoutEntry> Passives(params (string id, int level)[] entries)
        {
            var loadout = new List<PassiveLoadoutEntry>(entries.Length);
            foreach ((string id, int level) in entries)
            {
                loadout.Add(new PassiveLoadoutEntry(id, level));
            }

            return loadout;
        }

        private string R31Text()
        {
            return Host.Data.Current.SpawnTable.HpMultiplierAt(Host.Clock.Step).ToString("F1", CultureInfo.InvariantCulture);
        }

        private int CountOrbs()
        {
            int count = 0;
            foreach (SimEntity entity in Host.Session.Lifecycle.ActiveEntities)
            {
                if (entity is OrbitOrb)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountProjectiles(bool homing)
        {
            int count = 0;
            foreach (SimEntity entity in Host.Session.Lifecycle.ActiveEntities)
            {
                if (entity is PlayerProjectile projectile && projectile.IsHoming == homing)
                {
                    count++;
                }
            }

            return count;
        }

        // Live homing projectiles whose tracked target is alive (expected projectile -> target lines).
        private int HomingLines()
        {
            int count = 0;
            foreach (SimEntity entity in Host.Session.Lifecycle.ActiveEntities)
            {
                if (entity is PlayerProjectile projectile && projectile.IsHoming && projectile.LastTargetId.IsValid
                    && Host.Session.Lifecycle.TryGet(projectile.LastTargetId, out _))
                {
                    count++;
                }
            }

            return count;
        }

        // Weapons whose last target is alive (expected player -> target lines).
        private int WeaponLines()
        {
            int count = 0;
            foreach (WeaponInstance weapon in Host.Session.Weapons.Slots)
            {
                if (weapon != null && weapon.LastTargetId.IsValid && Host.Session.Lifecycle.TryGet(weapon.LastTargetId, out _))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLines(DevCommandLog log, string part)
        {
            int count = 0;
            for (int i = 0; i < log.Count; i++)
            {
                if (log.NewestFirst(i).Contains(part))
                {
                    count++;
                }
            }

            return count;
        }

        // Newest-first index of the first line containing part, -1 when none.
        private static int IndexOf(DevCommandLog log, string part)
        {
            for (int i = 0; i < log.Count; i++)
            {
                if (log.NewestFirst(i).Contains(part))
                {
                    return i;
                }
            }

            return -1;
        }

        // Oldest to newest, the (run, step) pairs never go back (commands and chest results interleave in step order).
        private static void AssertStepsNonDecreasing(DevCommandLog log)
        {
            long previousRun = -1, previousStep = -1;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                string[] parts = log.NewestFirst(i).Split(' ');
                long run = long.Parse(parts[0].Substring(1), CultureInfo.InvariantCulture);
                long step = long.Parse(parts[1], CultureInfo.InvariantCulture);
                Assert.IsTrue(run > previousRun || (run == previousRun && step >= previousStep), "order at: " + log.NewestFirst(i));
                previousRun = run;
                previousStep = step;
            }
        }

        private int LiveNonBossMonsters()
        {
            int count = 0;
            foreach (SimEntity entity in Host.Session.Lifecycle.ActiveEntities)
            {
                if (entity is Monster monster && !monster.IsBoss)
                {
                    count++;
                }
            }

            return count;
        }

        private EntityCategory? MonsterCategoryOf(EntityHandle id)
        {
            return Host.Session.Lifecycle.TryGet(id, out SimEntity entity) ? entity.Category : (EntityCategory?)null;
        }

        // Moves right at the default speed (reaches a chest queued at the player + (3, 0)).
        private sealed class MoveRightInput : IInputSource
        {
            public MoveCommand ReadMove(long step, Vector2 currentPosition)
            {
                return new MoveCommand(Vector2.right);
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
