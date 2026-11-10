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
    /// Dev mode on the real Main.unity (TAC-F19-1..4, 6, 13, 5.15): attach/no-attach, overlay and status panel, time controls,
    /// state and enemy controls, LastTargetId, visualizer callback, and the F19 determinism criterion.
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
            Assert.AreEqual("HP mult: n/a", lines[5]);
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
        public IEnumerator SetLoadout_SetsLevels_AndShowsEvolved()
        {
            yield return AttachAndStart();
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("W1", 99) }));
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W1", 2) }));
            Assert.IsFalse(Controller.TrySetLoadout(new[] { new WeaponLoadoutEntry("NOPE", 1) }));
            Assert.AreEqual(0, Host.Commands.PendingCount);
            Assert.IsTrue(Controller.TrySetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 3), new WeaponLoadoutEntry("W3", 5), new WeaponLoadoutEntry("E1", 1),
            }));
            long at = Host.Clock.Step;
            yield return WaitFor(() => Host.Clock.Step >= at + 2, "applied");
            IReadOnlyList<WeaponInstance> slots = Host.Session.Weapons.Slots;
            Assert.AreEqual("W1", slots[0].Id);
            Assert.AreEqual(3, slots[0].Level);
            Assert.AreEqual("W3", slots[1].Id);
            Assert.AreEqual(5, slots[1].Level);
            Assert.AreEqual("E1", slots[2].Id);
            Assert.IsNull(slots[3]);
            var lines = new List<string>();
            _root.Overlay.BuildStatusLines(lines);
            int weapons = lines.IndexOf("Weapons:");
            Assert.AreEqual("  " + slots[0].Definition.DisplayName + " Lv 3", lines[weapons + 1]);
            Assert.AreEqual("  " + slots[1].Definition.DisplayName + " Lv 5", lines[weapons + 2]);
            Assert.AreEqual("  " + slots[2].Definition.DisplayName + " (evolved)", lines[weapons + 3]);
            Assert.AreEqual("Passives:", lines[weapons + 4]);
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
                    opened = true;
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

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
