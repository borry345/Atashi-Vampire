using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Tests.EditMode.Data
{
    /// Asset key resolution with fallback to the runtime circle (F11 "에셋 키 없음", U20 first measurement in EditMode).
    public sealed class AssetKeyResolverTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        private AssetKeyRegistry Registry(params string[] keys)
        {
            var registry = ScriptableObject.CreateInstance<AssetKeyRegistry>();
            _created.Add(registry);
            registry.Entries = new List<AssetKeyRegistry.Entry>();
            foreach (string key in keys)
            {
                registry.Entries.Add(new AssetKeyRegistry.Entry { Key = key, Color = Color.green });
            }

            return registry;
        }

        [Test]
        public void DefaultCircle_IsCreatedInEditMode()
        {
            Sprite circle = DefaultShapeFactory.Circle;
            Assert.IsNotNull(circle);
            Assert.AreSame(circle, DefaultShapeFactory.Circle, "cached");
            Assert.AreEqual(64, circle.texture.width);
            Assert.AreEqual(64, circle.texture.height);
            Assert.AreEqual(1f, circle.bounds.size.x, 1e-5f, "64 px at 64 ppu = 1 world unit");
            Assert.AreEqual(1f, circle.bounds.size.y, 1e-5f);
            Assert.AreEqual(255, ((Color32)circle.texture.GetPixel(32, 32)).a, "center is opaque");
            Assert.AreEqual(0, ((Color32)circle.texture.GetPixel(0, 0)).a, "corner is transparent");
            Assert.AreEqual(Color.magenta, DefaultShapeFactory.FallbackColor);
        }

        [Test]
        public void NullRegistry_FallsBackToMagentaCircle_AndWarns()
        {
            var report = new DataValidationReport();
            var resolver = new AssetKeyResolver(null, report);
            Assert.AreEqual(1, report.Issues.Count, "one warning about the missing registry");

            VisualAsset visual = resolver.Resolve("shape.monster.green");
            Assert.AreSame(DefaultShapeFactory.Circle, visual.Sprite);
            Assert.AreEqual(Color.magenta, visual.Color);
            Assert.IsNull(visual.PrefabOverride);
            Assert.IsTrue(report.IsPlayable);
            Assert.IsTrue(report.Issues.All(i => i.Severity == IssueSeverity.Warning));
        }

        [Test]
        public void UnknownKey_WarnsOncePerKey_WithKeyName()
        {
            var report = new DataValidationReport();
            var resolver = new AssetKeyResolver(Registry("shape.a"), report);
            resolver.Resolve("shape.missing");
            resolver.Resolve("shape.missing");
            resolver.Resolve("shape.other");
            Assert.AreEqual(2, report.Issues.Count);
            StringAssert.Contains("shape.missing", report.Issues[0].Message);
            StringAssert.Contains("shape.other", report.Issues[1].Message);
            Assert.IsNotNull(resolver.Resolve(null).Sprite, "null key still resolves");
        }

        [Test]
        public void RegisteredKey_ResolvesEntry_SpriteFallsBackWhenEmpty()
        {
            var report = new DataValidationReport();
            var resolver = new AssetKeyResolver(Registry("shape.a"), report);
            VisualAsset visual = resolver.Resolve("shape.a");
            Assert.AreEqual(Color.green, visual.Color);
            Assert.AreSame(DefaultShapeFactory.Circle, visual.Sprite, "entry without sprite uses the circle");
            Assert.IsTrue(resolver.Contains("shape.a"));
            Assert.AreEqual(0, report.Issues.Count);
        }

        [Test]
        public void ValidateKeys_WarnsPerFileWithDefinition()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var report = new DataValidationReport();
            var resolver = new AssetKeyResolver(Registry(), report);
            resolver.ValidateKeys(data);

            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Weapons, "W1", "assetKey"));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Monsters, "M3", "ranged.projectile.assetKey"));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Monsters, "M4", "bossPatterns[0].assetKey"));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Passives, "P2", "iconKey"));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.PlayerStats, null, "pickups.gemAssetKey"));
            Assert.IsTrue(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.PlayerStats, null, "pickups.chestAssetKey"));
            Assert.IsFalse(DataTestFixtures.Has(report, IssueSeverity.Warning, DataFileKind.Weapons, "W1", "iconKey"), "iconKey == assetKey is warned once");
            Assert.IsTrue(report.IsPlayable);
        }

        [Test]
        public void ValidateKeys_AllRegistered_NoWarnings()
        {
            var (data, _) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var report = new DataValidationReport();
            var keys = new[]
            {
                "shape.projectile.white", "shape.orb.cyan", "shape.zone.green", "shape.projectile.pink", "shape.projectile.gold", "shape.orb.gold",
                "shape.monster.green", "shape.monster.orange", "shape.monster.purple", "shape.enemyshot.red", "shape.boss.red", "shape.hazard.red",
                "icon.p1", "icon.p2", "icon.p3", "shape.gem.blue", "shape.chest.yellow",
            };
            var resolver = new AssetKeyResolver(Registry(keys), report);
            resolver.ValidateKeys(data);
            Assert.AreEqual(0, report.Issues.Count, DataTestFixtures.Dump(report));
        }

        [Test]
        public void DuplicateRegistryKey_FirstWins()
        {
            AssetKeyRegistry registry = Registry("shape.a");
            registry.Entries.Add(new AssetKeyRegistry.Entry { Key = "shape.a", Color = Color.red });
            var report = new DataValidationReport();
            var resolver = new AssetKeyResolver(registry, report);
            Assert.AreEqual(Color.green, resolver.Resolve("shape.a").Color);
            Assert.AreEqual(1, report.Issues.Count);
        }

        [Test]
        public void LoadDefault_ReturnsNullWithoutAsset_OrTheRegistry()
        {
            // The user creates Resources/AssetKeyRegistry.asset; tests must pass either way.
            AssetKeyRegistry registry = AssetKeyRegistry.LoadDefault();
            var resolver = new AssetKeyResolver(registry, null);
            Assert.IsNotNull(resolver.Resolve("shape.not.registered.anywhere").Sprite);
        }
    }
}
