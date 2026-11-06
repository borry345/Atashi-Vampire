using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R18_DespawnTests
    {
        [Test]
        public void ExactlyAtDistance_IsNotBeyond()
        {
            Assert.IsFalse(SpawnGeometry.IsBeyondDespawn(new Vector2(45f, 0f), Vector2.zero, 45f));
            Assert.IsFalse(SpawnGeometry.IsBeyondDespawn(new Vector2(-27f, -36f), Vector2.zero, 45f));
        }

        [Test]
        public void JustBeyondDistance_IsBeyond()
        {
            Assert.IsTrue(SpawnGeometry.IsBeyondDespawn(new Vector2(45.01f, 0f), Vector2.zero, 45f));
            Assert.IsTrue(SpawnGeometry.IsBeyondDespawn(new Vector2(100f, 100f), new Vector2(54.99f, 100f), 45f));
        }

        [Test]
        public void Inside_IsNotBeyond()
        {
            Assert.IsFalse(SpawnGeometry.IsBeyondDespawn(new Vector2(-1000f, 5f), new Vector2(-1010f, 5f), 45f));
        }
    }
}
