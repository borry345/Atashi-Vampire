using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Collision
{
    /// PhysicsSettingsApplier: Physics2D globals (5.9.1, U30) and the R30 layer matrix on unnamed layers 8..17 (U12, C9).
    public sealed class PhysicsSettingsApplierTests : CollisionPmFixture
    {
        private const int First = CollisionLayerMapping.FirstUnityLayer;
        private const int Last = CollisionLayerMapping.FirstUnityLayer + 9;

        [Test]
        public void Apply_SetsEveryGlobal()
        {
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            Physics2D.callbacksOnDisable = true;
            Physics2D.queriesHitTriggers = false;
            Physics2D.defaultContactOffset = 0.01f;

            PhysicsSettingsApplier.Apply();

            Assert.AreEqual(SimulationMode2D.Script, Physics2D.simulationMode);
            Assert.IsFalse(Physics2D.callbacksOnDisable);
            Assert.IsTrue(Physics2D.queriesHitTriggers);
#pragma warning disable CS0618
            Assert.IsFalse(Physics2D.autoSyncTransforms);
#pragma warning restore CS0618
            Assert.AreEqual(0.0001f, Physics2D.defaultContactOffset, "U30: set value reads back");
        }

        [Test]
        public void CollisionLayers_AreUnnamedInThisProject()
        {
            for (int layer = First; layer <= Last; layer++)
            {
                Assert.AreEqual(string.Empty, LayerMask.LayerToName(layer), "layer " + layer);
            }
        }

        [Test]
        public void Apply_EnablesExactlyTheNineChannelPairs_OnUnnamedLayers()
        {
            ScrambleMatrix();

            PhysicsSettingsApplier.Apply();

            AssertMatrix();
        }

        [Test]
        public void Apply_IsIdempotent()
        {
            PhysicsSettingsApplier.Apply();
            PhysicsSettingsApplier.Apply();

            AssertMatrix();
            Assert.AreEqual(SimulationMode2D.Script, Physics2D.simulationMode);
        }

        // Every pair among 8..17 (45 distinct pairs + 10 self pairs) and every pair between 8..17 and the other 22 layers.
        private static void AssertMatrix()
        {
            int enabled = 0;
            for (int i = First; i <= Last; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    bool expectCollide = false;
                    if (j >= First && j <= Last)
                    {
                        expectCollide = CollisionChannelTable.TryGetChannel(
                            (CollisionLayer)(i - First), (CollisionLayer)(j - First), out _, out _);
                    }

                    bool collides = !Physics2D.GetIgnoreLayerCollision(i, j);
                    Assert.AreEqual(expectCollide, collides, $"layers {i} and {j}");
                    if (collides && j >= i)
                    {
                        enabled++;
                    }
                }
            }

            Assert.AreEqual(CollisionChannelTable.ChannelCount, enabled);
        }

        private static void ScrambleMatrix()
        {
            for (int i = First; i <= Last; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    Physics2D.IgnoreLayerCollision(i, j, (i + j) % 3 == 0);
                }
            }
        }
    }
}
