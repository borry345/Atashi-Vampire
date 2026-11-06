using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies.Pool;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Pool
{
    /// IPoolStrategy contract scenarios shared by all three strategies; each derived fixture supplies one strategy.
    public abstract class PoolStrategyContractTests
    {
        protected IPoolStrategy Strategy { get; private set; }
        protected GameObject Template { get; private set; }

        protected abstract IPoolStrategy CreateStrategy();

        protected abstract PoolStrategyKind ExpectedKind { get; }

        [SetUp]
        public void SetUpStrategy()
        {
            PoolTestObjects.DestroyAll();
            Strategy = CreateStrategy();
            Template = PoolTestObjects.CreateTemplate("Template");
        }

        [TearDown]
        public void TearDownObjects()
        {
            PoolTestObjects.DestroyAll();
        }

        [Test]
        public void Kind_MatchesStrategy()
        {
            Assert.AreEqual(ExpectedKind, Strategy.Kind);
        }

        [Test]
        public void Acquire_ReturnsActiveCloneAtPosition()
        {
            GameObject instance = Strategy.Acquire(Template, new Vector2(3.5f, -2.25f));

            Assert.AreNotSame(Template, instance);
            Assert.IsTrue(instance.activeSelf);
            Assert.AreEqual(new Vector3(3.5f, -2.25f, 0f), instance.transform.position);
            Assert.AreEqual(1, Strategy.ActiveCount);
        }

        [Test]
        public void Acquire_SetsPooledObjectTemplateAndActiveFlag()
        {
            GameObject instance = Strategy.Acquire(Template, Vector2.zero);

            PooledObject pooled = instance.GetComponent<PooledObject>();
            Assert.IsNotNull(pooled);
            Assert.AreSame(Template, pooled.Template);
            Assert.IsTrue(pooled.IsActive);
        }

        [Test]
        public void Acquire_LeavesTemplateInactive()
        {
            Strategy.Acquire(Template, Vector2.one);

            Assert.IsFalse(Template.activeSelf);
            Assert.AreEqual(Vector3.zero, Template.transform.position);
        }

        [Test]
        public void ActiveCount_CountsAcquiredInstancesAcrossTemplates()
        {
            GameObject other = PoolTestObjects.CreateTemplate("Other");

            Strategy.Acquire(Template, Vector2.zero);
            Strategy.Acquire(Template, Vector2.zero);
            GameObject third = Strategy.Acquire(other, Vector2.zero);
            Assert.AreEqual(3, Strategy.ActiveCount);

            Strategy.Release(third);
            Assert.AreEqual(2, Strategy.ActiveCount);
        }

        [Test]
        public void Release_DeactivatesAndClearsActiveFlag()
        {
            GameObject instance = Strategy.Acquire(Template, Vector2.zero);

            Strategy.Release(instance);

            Assert.AreEqual(0, Strategy.ActiveCount);
            Assert.IsFalse(instance.activeSelf);
            Assert.IsFalse(instance.GetComponent<PooledObject>().IsActive);
        }

        [Test]
        public void Release_Twice_LogsAndThrowsInvalidOperation()
        {
            GameObject instance = Strategy.Acquire(Template, Vector2.zero);
            Strategy.Release(instance);

            PoolTestObjects.ExpectPoolError(@"Double release of 'PoolTestTemplate\(Clone\)' \(id -?\d+\)");
            Assert.Throws<InvalidOperationException>(() => Strategy.Release(instance));
            Assert.AreEqual(0, Strategy.ActiveCount);
        }

        [Test]
        public void Release_CloneNeverAcquired_LogsAndThrowsInvalidOperation()
        {
            GameObject foreign = UnityEngine.Object.Instantiate(Template);

            PoolTestObjects.ExpectPoolError(@"Release of foreign object 'PoolTestTemplate\(Clone\)' \(id -?\d+\)");
            Assert.Throws<InvalidOperationException>(() => Strategy.Release(foreign));
        }

        [Test]
        public void Release_InstanceOfAnotherStrategy_LogsAndThrowsInvalidOperation()
        {
            IPoolStrategy other = CreateStrategy();
            GameObject instance = other.Acquire(Template, Vector2.zero);

            PoolTestObjects.ExpectPoolError(@"Release of foreign object 'PoolTestTemplate\(Clone\)'");
            Assert.Throws<InvalidOperationException>(() => Strategy.Release(instance));
            Assert.AreEqual(1, other.ActiveCount);
            Assert.IsTrue(instance.activeSelf);
        }

        [Test]
        public void Release_ObjectWithoutPooledObject_LogsAndThrowsInvalidOperation()
        {
            var plain = new GameObject(PoolTestObjects.NamePrefix + "Plain");

            PoolTestObjects.ExpectPoolError(@"Release of foreign object 'PoolTestPlain'");
            Assert.Throws<InvalidOperationException>(() => Strategy.Release(plain));
        }

        [Test]
        public void Release_Null_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Strategy.Release(null));
        }

        [Test]
        public void Acquire_InvalidTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Strategy.Acquire(null, Vector2.zero));
            Assert.Throws<InvalidOperationException>(
                () => Strategy.Acquire(PoolTestObjects.CreateUndecoratedTemplate("Undecorated"), Vector2.zero));

            GameObject activeTemplate = PoolTestObjects.CreateTemplate("Active");
            activeTemplate.SetActive(true);
            Assert.Throws<InvalidOperationException>(() => Strategy.Acquire(activeTemplate, Vector2.zero));
            Assert.AreEqual(0, Strategy.ActiveCount);
        }

        [Test]
        public void Prewarm_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => Strategy.Prewarm(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.Prewarm(Template, -1));
            Assert.Throws<InvalidOperationException>(
                () => Strategy.Prewarm(PoolTestObjects.CreateUndecoratedTemplate("Undecorated"), 1));
        }

        [Test]
        public void Prewarm_DoesNotAcquire()
        {
            Strategy.Prewarm(Template, 4);

            Assert.AreEqual(0, Strategy.ActiveCount);
            foreach (PooledObject pooled in UnityEngine.Object.FindObjectsByType<PooledObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.IsFalse(pooled.gameObject.activeSelf, pooled.name);
                Assert.IsFalse(pooled.IsActive, pooled.name);
            }
        }

        [Test]
        public void ReleaseAll_ReleasesEveryAcquiredInstance()
        {
            GameObject other = PoolTestObjects.CreateTemplate("Other");
            var acquired = new List<GameObject>
            {
                Strategy.Acquire(Template, Vector2.zero),
                Strategy.Acquire(other, Vector2.zero),
                Strategy.Acquire(Template, Vector2.zero),
            };
            Strategy.Release(acquired[2]);

            Strategy.ReleaseAll();

            Assert.AreEqual(0, Strategy.ActiveCount);
            foreach (GameObject instance in acquired)
            {
                Assert.IsFalse(instance.activeSelf);
                Assert.IsFalse(instance.GetComponent<PooledObject>().IsActive);
            }

            PoolTestObjects.ExpectPoolError(@"Double release of 'PoolTestTemplate\(Clone\)'");
            Assert.Throws<InvalidOperationException>(() => Strategy.Release(acquired[0]));
        }

        [Test]
        public void ReleaseAll_WhenEmpty_DoesNothing()
        {
            Strategy.ReleaseAll();

            Assert.AreEqual(0, Strategy.ActiveCount);
        }

        [Test]
        public void Acquire_AfterReleaseAll_ServesNewRun()
        {
            Strategy.Acquire(Template, Vector2.zero);
            Strategy.ReleaseAll();

            GameObject instance = Strategy.Acquire(Template, new Vector2(1f, 1f));

            Assert.IsTrue(instance.activeSelf);
            Assert.AreEqual(1, Strategy.ActiveCount);
        }

        [Test]
        public void Acquire_OtherTemplate_DoesNotReuseReleasedInstance()
        {
            GameObject other = PoolTestObjects.CreateTemplate("Other");
            GameObject released = Strategy.Acquire(Template, Vector2.zero);
            Strategy.Release(released);

            GameObject instance = Strategy.Acquire(other, Vector2.zero);

            Assert.AreNotSame(released, instance);
            Assert.AreSame(other, instance.GetComponent<PooledObject>().Template);
            Assert.AreEqual("PoolTestOther(Clone)", instance.name);
        }

        // Maps each acquired instance to the order in which it was first seen, so runs on different templates compare.
        protected static List<int> RunReuseSequence(IPoolStrategy strategy, GameObject template)
        {
            var firstSeen = new List<GameObject>();
            var pattern = new List<int>();

            void Record(GameObject instance)
            {
                int index = firstSeen.IndexOf(instance);
                if (index < 0)
                {
                    firstSeen.Add(instance);
                    index = firstSeen.Count - 1;
                }

                pattern.Add(index);
            }

            GameObject a = strategy.Acquire(template, Vector2.zero);
            GameObject b = strategy.Acquire(template, Vector2.zero);
            GameObject c = strategy.Acquire(template, Vector2.zero);
            Record(a);
            Record(b);
            Record(c);
            strategy.Release(b);
            strategy.Release(a);
            Record(strategy.Acquire(template, Vector2.zero));
            Record(strategy.Acquire(template, Vector2.zero));
            Record(strategy.Acquire(template, Vector2.zero));
            strategy.ReleaseAll();
            Record(strategy.Acquire(template, Vector2.zero));
            Record(strategy.Acquire(template, Vector2.zero));
            return pattern;
        }
    }
}
