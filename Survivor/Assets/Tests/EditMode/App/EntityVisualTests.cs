using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.App
{
    /// EntityPresenter's pure rules (wave 4d): sorting order per category, diameter from radius, and which asset key each entity
    /// kind is drawn with (weapon id lookup, pickup keys from player stats, "" when anything is missing).
    public sealed class EntityVisualTests
    {
        private static GameData LoadShippedData()
        {
            (GameData data, DataValidationReport report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.IsTrue(report.IsPlayable, "the shipped data is playable");
            return data;
        }

        [TestCase(EntityCategory.PlayerZone, -10)]
        [TestCase(EntityCategory.EnemyZone, -10)]
        [TestCase(EntityCategory.Gem, 0)]
        [TestCase(EntityCategory.Chest, 0)]
        [TestCase(EntityCategory.Monster, 5)]
        [TestCase(EntityCategory.PlayerProjectile, 8)]
        [TestCase(EntityCategory.PlayerOrb, 8)]
        [TestCase(EntityCategory.EnemyProjectile, 8)]
        public void SortingOrderOf_Table(EntityCategory category, int expected)
        {
            Assert.AreEqual(expected, EntityPresenter.SortingOrderOf(category));
        }

        [Test]
        public void SortingOrder_EveryEntityIsBelowThePlayerCircle()
        {
            foreach (EntityCategory category in System.Enum.GetValues(typeof(EntityCategory)))
            {
                Assert.Less(EntityPresenter.SortingOrderOf(category), 10, category + " is drawn under the player (order 10)");
            }
        }

        [TestCase(0.5f, 1f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(1.75f, 3.5f)]
        public void DiameterOf_IsTwiceTheRadius(float radius, float expected)
        {
            Assert.AreEqual(expected, EntityPresenter.DiameterOf(radius));
        }

        [Test]
        public void WeaponKeyOf_LooksUpTheWeaponAssetKey()
        {
            GameData data = LoadShippedData();
            foreach (WeaponDefinition weapon in data.Weapons)
            {
                Assert.AreEqual(weapon.AssetKey, EntityPresenter.WeaponKeyOf(weapon.Id, data), weapon.Id);
            }
        }

        [Test]
        public void WeaponKeyOf_MissingIdWeaponOrData_IsEmpty()
        {
            GameData data = LoadShippedData();
            Assert.AreEqual(string.Empty, EntityPresenter.WeaponKeyOf(null, data));
            Assert.AreEqual(string.Empty, EntityPresenter.WeaponKeyOf("no-such-weapon", data));
            Assert.AreEqual(string.Empty, EntityPresenter.WeaponKeyOf(data.Weapons[0].Id, null));
        }

        [Test]
        public void KeyOf_PickupsUsePlayerStatsKeys_AndUninitializedEntitiesAreEmpty()
        {
            GameData data = LoadShippedData();
            var host = new GameObject("EntityVisualTests.Host");
            try
            {
                Assert.AreEqual("shape.gem.blue", EntityPresenter.KeyOf(host.AddComponent<ExperienceGem>(), data));
                Assert.AreEqual("shape.chest.yellow", EntityPresenter.KeyOf(host.AddComponent<Chest>(), data));
                Assert.AreEqual(string.Empty, EntityPresenter.KeyOf(host.AddComponent<ExperienceGem>(), null), "no data: fallback key");
                Assert.AreEqual(string.Empty, EntityPresenter.KeyOf(host.AddComponent<PlayerProjectile>(), data), "no weapon id yet");
                Assert.AreEqual(string.Empty, EntityPresenter.KeyOf(host.AddComponent<Monster>(), data), "no definition yet");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FindVisual_FindsOnlyTheVisualChild()
        {
            var host = new GameObject("EntityVisualTests.Host");
            try
            {
                Assert.IsNull(EntityPresenter.FindVisual(host.transform));
                new GameObject("Other").transform.SetParent(host.transform, false);
                Assert.IsNull(EntityPresenter.FindVisual(host.transform));
                var visual = new GameObject(EntityPresenter.VisualName);
                visual.transform.SetParent(host.transform, false);
                Assert.AreSame(visual.transform, EntityPresenter.FindVisual(host.transform));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
