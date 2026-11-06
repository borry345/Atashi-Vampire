using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies.Pool;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Pool
{
    /// Strategy B (InstantiatePoolStrategy): contract scenarios plus per-request Instantiate and deferred Destroy (TAC-F9-3, U22).
    [TestFixture]
    public sealed class InstantiatePoolStrategyTests : PoolStrategyContractTests
    {
        // U22: frames to wait after Release before Destroy has taken effect.
        private const int DestroyFrames = 1;

        protected override IPoolStrategy CreateStrategy() => new InstantiatePoolStrategy();

        protected override PoolStrategyKind ExpectedKind => PoolStrategyKind.InstantiateDestroy;

        protected override bool KeepsInactiveInstances => false;

        [Test]
        public void Acquire_AfterRelease_InstantiatesNewInstance()
        {
            GameObject first = Strategy.Acquire(Template, Vector2.zero);
            Strategy.Release(first);

            GameObject second = Strategy.Acquire(Template, Vector2.zero);

            Assert.AreNotSame(first, second);
        }

        [Test]
        public void ReuseSequence_NeverReuses()
        {
            var expected = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

            List<int> run = RunReuseSequence(new InstantiatePoolStrategy(), PoolTestObjects.CreateTemplate("Run"));

            CollectionAssert.AreEqual(expected, run);
        }

        [Test]
        public void Prewarm_CreatesNothing()
        {
            Strategy.Prewarm(Template, 5);

            Assert.AreEqual(0, PoolTestObjects.CountClones(Template));
            Assert.AreEqual(0, Strategy.ActiveCount);
        }

        [Test]
        public void Acquire_NewInstanceStartsFromTemplateState()
        {
            GameObject first = Strategy.Acquire(Template, Vector2.zero);
            first.GetComponent<SpriteRenderer>().color = Color.red;
            Strategy.Release(first);

            GameObject second = Strategy.Acquire(Template, Vector2.zero);

            Assert.AreEqual(Template.GetComponent<SpriteRenderer>().color, second.GetComponent<SpriteRenderer>().color);
        }

        [UnityTest]
        public IEnumerator Release_IsInactiveAtOnceAndDestroyedAfterOneFrame()
        {
            GameObject instance = Strategy.Acquire(Template, Vector2.zero);
            int instanceId = instance.GetInstanceID();

            Strategy.Release(instance);

            // Same frame: Destroy is deferred, so the object still exists but must already be invisible to later steps.
            Assert.IsTrue(instance != null, "Destroy applied in the same frame");
            Assert.IsFalse(instance.activeSelf);
            Assert.IsFalse(instance.activeInHierarchy);
            foreach (PooledObject pooled in Object.FindObjectsByType<PooledObject>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Assert.AreNotEqual(instanceId, pooled.gameObject.GetInstanceID(), "released instance still found as active");
            }

            int frames = 0;
            while (instance != null && frames < 10)
            {
                yield return null;
                frames++;
            }

            Debug.Log($"[U22] Destroy took effect after {frames} frame(s).");
            Assert.AreEqual(DestroyFrames, frames);
        }

        [UnityTest]
        public IEnumerator ReleaseAll_DestroysEveryInstanceAfterOneFrame()
        {
            var acquired = new List<GameObject>();
            for (int i = 0; i < 5; i++)
            {
                acquired.Add(Strategy.Acquire(Template, Vector2.zero));
            }

            Strategy.ReleaseAll();
            Assert.AreEqual(5, PoolTestObjects.CountClones(Template));

            for (int i = 0; i < DestroyFrames; i++)
            {
                yield return null;
            }

            foreach (GameObject instance in acquired)
            {
                Assert.IsTrue(instance == null);
            }

            Assert.AreEqual(0, PoolTestObjects.CountClones(Template));
            Assert.AreEqual(0, Strategy.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Release_AfterDestroy_LogsAndThrowsInvalidOperation()
        {
            GameObject instance = Strategy.Acquire(Template, Vector2.zero);
            Strategy.Release(instance);
            for (int i = 0; i < DestroyFrames; i++)
            {
                yield return null;
            }

            PoolTestObjects.ExpectPoolError(@"Release of a destroyed instance \(id -?\d+\): double release\.");
            Assert.Throws<System.InvalidOperationException>(() => Strategy.Release(instance));
        }
    }
}
