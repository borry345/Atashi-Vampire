using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Composition root wiring (5c.4 wave 4, 5c.3 #9, 5c.3a): boot order and presentation, all nine router channels, all nine
    /// command kinds, the four pending owner kinds, every documented phase order, and the requirements callback at run start
    /// and on a target provider switch.
    public sealed class FlowAssemblyTests : FlowPmFixture
    {
        [UnityTest]
        public IEnumerator Boot_ShowsTitle_WithTimeCameraLightUiAndDisplaySet()
        {
            yield return LoadMain();
            Assert.AreEqual(FlowState.Title, Flow.State);
            Assert.IsTrue(Flow.Title.IsVisible);
            Assert.IsTrue(Flow.Title.StartEnabled);
            Assert.IsFalse(Flow.Hud.IsVisible);
            Assert.IsFalse(Clock.IsRunning, "nothing runs on the title screen");
            Assert.AreEqual(0, Clock.Step);
            Assert.AreEqual(1f / 60f, Time.fixedDeltaTime, 1e-6f, "TAC-0-3");
            Assert.AreEqual(0.05f, Time.maximumDeltaTime, 1e-6f, "TAC-0-3");
            Assert.AreEqual(SimulationMode2D.Script, Physics2D.simulationMode, "PhysicsSettingsApplier ran");
            CollectionAssert.Contains(WindowSizeSelector.Candidates, DisplaySetup.AppliedSize, "DisplaySetup chose a candidate");

            Camera camera = Host.MainCamera;
            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.orthographic);
            Assert.AreEqual(GameRules.CameraOrthographicSize, camera.orthographicSize, "TAC-F1-6");
            Assert.IsNotNull(camera.GetComponent<CameraFollow>());
            Assert.AreEqual(1, Object.FindObjectsByType<BackgroundTiler>(FindObjectsSortMode.None).Length);
            // The test assembly does not reference URP, uGUI or the Input System (5c.1 test row), so these are found by type name.
            Assert.AreEqual(1, ComponentsNamed("Light2D").Count, "the scene's Global Light 2D is used, none is added");

            Canvas canvas = Flow.Title.GetComponentInParent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode, "TAC-F8-7");
            Component scaler = canvas.GetComponent("CanvasScaler");
            Assert.IsNotNull(scaler);
            Assert.AreEqual("ScaleWithScreenSize", Property(scaler, "uiScaleMode").ToString());
            Assert.AreEqual(new Vector2(1920f, 1080f), (Vector2)Property(scaler, "referenceResolution"));
            Assert.AreEqual(0.5f, (float)Property(scaler, "matchWidthOrHeight"));
            List<Component> eventSystems = ComponentsNamed("EventSystem");
            Assert.AreEqual(1, eventSystems.Count);
            Assert.IsNotNull(eventSystems[0].GetComponent("InputSystemUIInputModule"), "TAC-F8-12");

            Assert.AreEqual(1, Object.FindObjectsByType<SimulationDriverEarly>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SimulationDriverMid>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SimulationDriverLate>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PresentationSync>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SimulationFrameCounter>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<KeyboardInputSource>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(StrategySelection.ShippingDefault.Pool, Host.NextRunSelection.Pool);
            Assert.AreEqual(StrategySelection.ShippingDefault.Target, Host.NextRunSelection.Target);
        }

        [UnityTest]
        public IEnumerator Router_EveryChannelAlreadyHasAHandler()
        {
            yield return LoadMain();
            Array channels = Enum.GetValues(typeof(CollisionChannel));
            Assert.AreEqual(9, channels.Length);
            foreach (CollisionChannel channel in channels)
            {
                Assert.Throws<InvalidOperationException>(() => Host.Router.SetHandler(channel, new FlowPmNullHandler()),
                    "channel " + channel + " must already be wired by the composition root");
            }
        }

        [UnityTest]
        public IEnumerator CommandBuffer_HasAHandlerForEveryKind()
        {
            yield return LoadMain();
            Array kinds = Enum.GetValues(typeof(SimulationCommandKind));
            Assert.AreEqual(9, kinds.Length);
            foreach (SimulationCommandKind kind in kinds)
            {
                Assert.IsTrue(Host.CommandBuffer.HasHandler(kind), kind.ToString());
            }
        }

        [UnityTest]
        public IEnumerator PendingQueue_FourOwnerKindsAreRegistered()
        {
            yield return LoadMain();
            var kinds = new[] { PendingCommandKind.SetLoadout, PendingCommandKind.SetHealth, PendingCommandKind.AddXp, PendingCommandKind.LevelUpNow,
                PendingCommandKind.SetSpawnsEnabled };
            foreach (PendingCommandKind kind in kinds)
            {
                Assert.IsTrue(PendingCommandQueue.IsOwnerHandled(kind));
                Assert.Throws<InvalidOperationException>(() => Host.Commands.RegisterHandler(kind, (c, ctx) => { }), kind + " already registered");
            }

            Assert.DoesNotThrow(() => Host.Commands.Enqueue(PendingCommand.SetLoadout(new[] { new WeaponLoadoutEntry("W1", 1) })));
            Assert.DoesNotThrow(() => Host.Commands.Enqueue(PendingCommand.SetHealth(50f)));
            Assert.DoesNotThrow(() => Host.Commands.Enqueue(PendingCommand.AddXp(1)));
            Assert.DoesNotThrow(() => Host.Commands.Enqueue(PendingCommand.LevelUpNow()));
            Assert.AreEqual(4, Host.Commands.PendingCount);
            Assert.IsNotNull(Host.Commands.BossSpawnedThisRun, "BossSpawnedThisRun wired to the spawner");
            Assert.IsFalse(Host.BossSpawnedThisRun);
        }

        [UnityTest]
        public IEnumerator Pipeline_EveryDocumentedOrderIsTaken()
        {
            yield return LoadMain();
            var taken = new List<(SimulationPhase phase, int order)>
            {
                (SimulationPhase.Begin, 0), (SimulationPhase.Begin, 10), (SimulationPhase.PlayerMove, 0),
                (SimulationPhase.BuildIndex, 0), (SimulationPhase.BuildIndex, 5), (SimulationPhase.BuildIndex, 10),
                (SimulationPhase.WeaponTick, 0), (SimulationPhase.WeaponTick, 10), (SimulationPhase.Collision, 0),
                (SimulationPhase.Resolve, 0), (SimulationPhase.Resolve, 10), (SimulationPhase.Resolve, 20), (SimulationPhase.Resolve, 30),
                (SimulationPhase.Lifecycle, 0), (SimulationPhase.Lifecycle, 10), (SimulationPhase.Lifecycle, 20),
                (SimulationPhase.Lifecycle, 30), (SimulationPhase.Lifecycle, 90),
            };
            foreach ((SimulationPhase phase, int order) in taken)
            {
                Assert.Throws<InvalidOperationException>(() => Host.Pipeline.AddHandler(phase, order, new FlowPmNullPhase()),
                    phase + " order " + order + " must already have its handler (5c.3 #9)");
            }
        }

        [UnityTest]
        public IEnumerator StartRun_AppliesTheRequirementsOfEachSelection()
        {
            yield return LoadMain();
            UseScript(11UL, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());

            Flow.StartRun();
            Assert.AreEqual(FlowState.Playing, Flow.State);
            Assert.IsTrue(Clock.IsRunning);
            Assert.AreEqual(TargetProviderKind.SpatialHash, Host.Targets.Kind);
            Assert.AreEqual(CollisionStrategyKind.SelfSquaredDistance, Host.Lifecycle.Collisions.Kind);
            Assert.IsTrue(Host.SpatialIndex.Required, "spatial hash target / self collision need the index");
            Assert.IsFalse(Host.Bodies.Requirements.MonsterColliders, "R19: no colliders for the shipping default");
            Assert.AreSame(Host.Player.Host, Host.PlayerHost);
            Assert.AreNotSame(Host.Player.Body.Host, Host.Player.MagnetBody.Host, "the magnet has its own host (a root object PlayerEntity creates)");
            yield return Frames(3);

            Flow.TogglePause();
            Host.NextRunSelection = new StrategySelection(PoolStrategyKind.InstantiateDestroy, TargetProviderKind.BruteForce,
                TickStrategyKind.PerObjectFixedUpdate, CollisionStrategyKind.EngineCollider2D);
            Host.StartNewRun();
            Assert.AreEqual(FlowState.Playing, Flow.State);
            Assert.AreEqual(PoolStrategyKind.InstantiateDestroy, Host.Lifecycle.Pool.Kind);
            Assert.AreEqual(TickStrategyKind.PerObjectFixedUpdate, Host.Lifecycle.Ticks.Kind);
            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, Host.Lifecycle.Collisions.Kind);
            Assert.AreEqual(TargetProviderKind.BruteForce, Host.Targets.Kind);
            Assert.IsFalse(Host.SpatialIndex.Required, "brute force + engine collision do not need the index");
            Assert.IsTrue(Host.Bodies.Requirements.AllColliders, "R19: engine collision enables every collider");
            Assert.AreEqual(2, Host.Bodies.Count, "the player body and magnet are registered with the synchronizer");
            yield return WaitFor(() => Clock.Step >= 3, "the engine run steps", 600);
            Assert.AreEqual(FlowState.Playing, Flow.State, "no step aborted");

            Flow.TogglePause();
            Host.NextRunSelection = new StrategySelection(PoolStrategyKind.UnityObjectPool, TargetProviderKind.Physics2DQuery,
                TickStrategyKind.CentralTick, CollisionStrategyKind.SelfSquaredDistance);
            Host.StartNewRun();
            Assert.IsTrue(Host.SpatialIndex.Required, "self collision needs the index");
            Assert.IsTrue(Host.Bodies.Requirements.MonsterColliders, "R19: Physics2D query needs monster colliders");
            Assert.IsFalse(Host.Bodies.Requirements.AllColliders);
            Assert.IsTrue(Host.Bodies.Requirements.SyncMonstersAtBuildIndex);
        }

        [UnityTest]
        public IEnumerator RequestTargetProviderSwitch_SwapsInBegin_AndReappliesTheRequirements()
        {
            yield return LoadMain();
            UseScript(12UL, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Host.NextRunSelection = new StrategySelection(PoolStrategyKind.CustomPool, TargetProviderKind.BruteForce,
                TickStrategyKind.CentralTick, CollisionStrategyKind.EngineCollider2D);
            Flow.StartRun();
            Assert.IsFalse(Host.SpatialIndex.Required);
            Host.RequestTargetProviderSwitch(TargetProviderKind.SpatialHash);
            Assert.AreEqual(TargetProviderKind.BruteForce, Host.Targets.Kind, "applied only in the next Begin");
            yield return WaitFor(() => Clock.Step >= 2, "two steps");
            Assert.AreEqual(TargetProviderKind.SpatialHash, Host.Targets.Kind);
            Assert.IsTrue(Host.SpatialIndex.Required, "the shared callback recomputed Required");

            Host.RequestTargetProviderSwitch(TargetProviderKind.Physics2DQuery);
            long step = Clock.Step;
            yield return WaitFor(() => Clock.Step >= step + 2, "two more steps");
            Assert.AreEqual(TargetProviderKind.Physics2DQuery, Host.Targets.Kind);
            Assert.IsFalse(Host.SpatialIndex.Required);
            Assert.IsTrue(Host.Bodies.Requirements.SyncMonstersAtBuildIndex);
        }

        private static List<Component> ComponentsNamed(string typeName)
        {
            var found = new List<Component>();
            foreach (Component component in Object.FindObjectsByType<Component>(FindObjectsSortMode.None))
            {
                if (component.GetType().Name == typeName)
                {
                    found.Add(component);
                }
            }

            return found;
        }

        private static object Property(Component component, string name)
        {
            return component.GetType().GetProperty(name).GetValue(component);
        }
    }
}
