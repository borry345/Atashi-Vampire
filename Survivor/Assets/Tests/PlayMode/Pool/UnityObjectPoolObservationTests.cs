using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Survivor.Tests.PlayMode.Pool
{
    /// U21: direct observations of UnityEngine.Pool.ObjectPool collectionCheck and maxSize behavior.
    public sealed class UnityObjectPoolObservationTests
    {
        private int _destroyCalls;

        [SetUp]
        public void SetUpCounters()
        {
            _destroyCalls = 0;
        }

        [TearDown]
        public void TearDownObjects()
        {
            PoolTestObjects.DestroyAll();
        }

        [Test]
        public void CollectionCheckOn_DoubleRelease_ThrowsInvalidOperation()
        {
            ObjectPool<GameObject> pool = CreatePool(collectionCheck: true, maxSize: int.MaxValue);
            GameObject item = pool.Get();
            pool.Release(item);

            var exception = Assert.Throws<InvalidOperationException>(() => pool.Release(item));

            Debug.Log($"[U21] collectionCheck double release: {exception.GetType().FullName}: {exception.Message}");
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void CollectionCheckOff_DoubleRelease_IsAcceptedAndDuplicates()
        {
            ObjectPool<GameObject> pool = CreatePool(collectionCheck: false, maxSize: int.MaxValue);
            GameObject item = pool.Get();
            pool.Release(item);

            pool.Release(item);

            Debug.Log($"[U21] collectionCheck off: CountInactive={pool.CountInactive} CountAll={pool.CountAll} CountActive={pool.CountActive}");
            Assert.AreEqual(2, pool.CountInactive);
            Assert.AreSame(pool.Get(), pool.Get());
        }

        [Test]
        public void CollectionCheckOn_ReleaseOfObjectNotFromPool_IsAccepted()
        {
            ObjectPool<GameObject> pool = CreatePool(collectionCheck: true, maxSize: int.MaxValue);
            var outsider = new GameObject(PoolTestObjects.NamePrefix + "Outsider");

            pool.Release(outsider);

            Debug.Log($"[U21] foreign release: CountInactive={pool.CountInactive} CountAll={pool.CountAll} CountActive={pool.CountActive}");
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void MaxSizeIntMax_KeepsEveryReleasedItem()
        {
            ObjectPool<GameObject> pool = CreatePool(collectionCheck: true, maxSize: int.MaxValue);
            var items = new List<GameObject>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add(pool.Get());
            }

            foreach (GameObject item in items)
            {
                pool.Release(item);
            }

            Assert.AreEqual(1000, pool.CountAll);
            Assert.AreEqual(1000, pool.CountInactive);
            Assert.AreEqual(0, _destroyCalls);
        }

        [Test]
        public void SmallMaxSize_DestroysOverflowOnRelease()
        {
            ObjectPool<GameObject> pool = CreatePool(collectionCheck: true, maxSize: 2);
            var items = new List<GameObject> { pool.Get(), pool.Get(), pool.Get() };

            foreach (GameObject item in items)
            {
                pool.Release(item);
            }

            Debug.Log($"[U21] maxSize 2, 3 released: CountInactive={pool.CountInactive} destroyCalls={_destroyCalls}");
            Assert.AreEqual(2, pool.CountInactive);
            Assert.AreEqual(1, _destroyCalls);
        }

        private ObjectPool<GameObject> CreatePool(bool collectionCheck, int maxSize)
        {
            return new ObjectPool<GameObject>(
                createFunc: () => new GameObject(PoolTestObjects.NamePrefix + "Item"),
                actionOnGet: null,
                actionOnRelease: null,
                actionOnDestroy: item =>
                {
                    _destroyCalls++;
                    UnityEngine.Object.Destroy(item);
                },
                collectionCheck: collectionCheck,
                defaultCapacity: 10,
                maxSize: maxSize);
        }
    }
}
