using System.Collections;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Player transform ownership (5c.3 #6, wave 4c): with engine collision the simulation drives the player and magnet host
    /// transforms (PlayerPresenter skips its position write); with self collision it does not.
    public sealed class FlowPlayerTransformTests : FlowPmFixture
    {
        [UnityTest]
        public IEnumerator PlayerBodies_DriveTheirTransform_OnlyUnderEngineCollision()
        {
            yield return LoadMain();
            UseScript(11UL, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());

            Host.NextRunSelection = StrategySelection.ShippingDefault;
            Host.StartNewRun();
            Assert.AreEqual(CollisionStrategyKind.SelfSquaredDistance, Host.Lifecycle.Collisions.Kind);
            Assert.IsFalse(Host.Player.Body.DrivesTransform, "self collision: PlayerPresenter writes the player transform");
            Assert.IsFalse(Host.Player.MagnetBody.DrivesTransform, "self collision: no simulation drives the magnet");
            yield return Frames(3);

            Host.Flow.TogglePause();
            Host.NextRunSelection = StrategySelection.Baseline;
            Host.StartNewRun();
            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, Host.Lifecycle.Collisions.Kind);
            Assert.IsTrue(Host.Player.Body.DrivesTransform, "engine collision: the simulation drives the player transform");
            Assert.IsTrue(Host.Player.MagnetBody.DrivesTransform, "engine collision: the simulation drives the magnet transform");
            Assert.IsNull(Host.Player.MagnetBody.Host.transform.parent, "the magnet host is a root object");
            yield return WaitFor(() => Clock.Step >= 3, "the engine run steps", 600);
            Assert.AreEqual(Host.Player.Position, (Vector2)Host.Player.Host.transform.position,
                "the simulation moved the player host to the simulated position");
        }
    }
}
