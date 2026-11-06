using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies.Pool;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Pool
{
    /// Strategy A (CustomPoolStrategy): contract scenarios plus LIFO reuse, prewarm and growth (TAC-F9-2).
    [TestFixture]
    public sealed class CustomPoolStrategyTests : PoolStrategyContractTests
    {
        protected override IPoolStrategy CreateStrategy() => new CustomPoolStrategy();

        protected override PoolStrategyKind ExpectedKind => PoolStrategyKind.CustomPool;

        [Test]
        public void Acquire_AfterRelease_ReturnsSameInstanceWithoutInstantiating()
        {
            GameObject first = Strategy.Acquire(Template, Vector2.zero);
            Strategy.Release(first);

            GameObject second = Strategy.Acquire(Template, new Vector2(5f, 6f));

            Assert.AreSame(first, second);
            Assert.AreEqual(1, PoolTestObjects.CountClones(Template));
            Assert.AreEqual(new Vector3(5f, 6f, 0f), second.transform.position);
        }

        [Test]
        public void ReuseSequence_IsLifoAndDeterministic()
        {
            var expected = new List<int> { 0, 1, 2, 0, 1, 3, 3, 1 };

            List<int> firstRun = RunReuseSequence(new CustomPoolStrategy(), PoolTestObjects.CreateTemplate("RunA"));
            List<int> secondRun = RunReuseSequence(new CustomPoolStrategy(), PoolTestObjects.CreateTemplate("RunB"));

            CollectionAssert.AreEqual(expected, firstRun);
            CollectionAssert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void Prewarm_CreatesInactiveInstancesThatAcquireReuses()
        {
            Strategy.Prewarm(Template, 3);
            Assert.AreEqual(3, PoolTestObjects.CountClones(Template));

            for (int i = 0; i < 3; i++)
            {
                Strategy.Acquire(Template, Vector2.zero);
            }

            Assert.AreEqual(3, PoolTestObjects.CountClones(Template));
            Assert.AreEqual(3, Strategy.ActiveCount);
        }

        [Test]
        public void Prewarm_Twice_AddsInstances()
        {
            Strategy.Prewarm(Template, 2);
            Strategy.Prewarm(Template, 3);

            Assert.AreEqual(5, PoolTestObjects.CountClones(Template));
        }

        [Test]
        public void Acquire_BeyondPrewarm_GrowsWithoutCap()
        {
            Strategy.Prewarm(Template, 2);
            var instances = new HashSet<GameObject>();

            for (int i = 0; i < 50; i++)
            {
                instances.Add(Strategy.Acquire(Template, Vector2.zero));
            }

            Assert.AreEqual(50, instances.Count);
            Assert.AreEqual(50, Strategy.ActiveCount);
            Assert.AreEqual(50, PoolTestObjects.CountClones(Template));
        }

        [Test]
        public void Acquire_ReusedInstance_KeepsComponentState()
        {
            GameObject first = Strategy.Acquire(Template, Vector2.zero);
            first.GetComponent<SpriteRenderer>().color = Color.red;
            first.transform.localScale = new Vector3(2f, 2f, 1f);
            Strategy.Release(first);

            GameObject second = Strategy.Acquire(Template, Vector2.zero);

            // TAC-F9-6: resetting state is EntityLifecycle's job, the pool must leave it alone.
            Assert.AreSame(first, second);
            Assert.AreEqual(Color.red, second.GetComponent<SpriteRenderer>().color);
            Assert.AreEqual(new Vector3(2f, 2f, 1f), second.transform.localScale);
        }

        [Test]
        public void ReleaseAll_KeepsInstancesForReuse()
        {
            Strategy.Acquire(Template, Vector2.zero);
            Strategy.Acquire(Template, Vector2.zero);
            Strategy.ReleaseAll();

            Strategy.Acquire(Template, Vector2.zero);
            Strategy.Acquire(Template, Vector2.zero);

            Assert.AreEqual(2, PoolTestObjects.CountClones(Template));
        }
    }
}
