using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    /// Code-built templates, decorator application (TAC-F9-7), prefab overrides (F11) and prewarm counts (Q13, L3, L5).
    public sealed class PrefabCatalogTests
    {
        [TearDown]
        public void TearDown()
        {
            LoopPmWorld.DestroyTestObjects();
        }

        private static Dictionary<EntityCategory, Type> AllCategories(Type type)
        {
            var types = new Dictionary<EntityCategory, Type>();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                types.Add(category, type);
            }

            return types;
        }

        [Test]
        public void Templates_AreInactive_WithSpriteRendererAndEntityType_AndEveryDecoratorInOrder()
        {
            var log = new List<string>();
            var catalog = new PrefabCatalog(AllCategories(typeof(LoopPmTickEntity)),
                new IEntityTemplateDecorator[] { new LoopPmDecorator(log, "pool"), new LoopPmDecorator(log, "tick") });

            var expected = new List<string>();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                GameObject template = catalog.GetTemplate(category);
                Assert.IsFalse(template.activeSelf, category.ToString());
                Assert.IsNotNull(template.GetComponent<SpriteRenderer>());
                LoopPmTickEntity entity = template.GetComponent<LoopPmTickEntity>();
                Assert.IsNotNull(entity);
                Assert.AreEqual(category, entity.Category);
                Assert.AreEqual(EntityHandle.None, entity.Id);
                expected.Add("pool:" + category + ":False");
                expected.Add("tick:" + category + ":False");
            }

            CollectionAssert.AreEqual(expected, log, "all decorators on every category, in list order, on inactive templates");
        }

        [Test]
        public void CategoryCopiedToPoolClones()
        {
            var catalog = new PrefabCatalog(AllCategories(typeof(LoopPmPlainEntity)), new IEntityTemplateDecorator[0]);
            GameObject clone = UnityEngine.Object.Instantiate(catalog.GetTemplate(EntityCategory.Chest));
            Assert.AreEqual(EntityCategory.Chest, clone.GetComponent<LoopPmPlainEntity>().Category);
        }

        [Test]
        public void InvalidComponentType_Throws()
        {
            var types = new Dictionary<EntityCategory, Type> { { EntityCategory.Monster, typeof(SpriteRenderer) } };
            Assert.Throws<ArgumentException>(() => new PrefabCatalog(types, new IEntityTemplateDecorator[0]));
            types[EntityCategory.Monster] = typeof(LoopPmAbstractEntity);
            Assert.Throws<ArgumentException>(() => new PrefabCatalog(types, new IEntityTemplateDecorator[0]));
            types[EntityCategory.Monster] = null;
            Assert.Throws<ArgumentException>(() => new PrefabCatalog(types, new IEntityTemplateDecorator[0]));
            Assert.Throws<ArgumentNullException>(() => new PrefabCatalog(null, new IEntityTemplateDecorator[0]));
            Assert.Throws<ArgumentNullException>(() => new PrefabCatalog(new Dictionary<EntityCategory, Type>(), null));
            Assert.Throws<ArgumentNullException>(() => new PrefabCatalog(new Dictionary<EntityCategory, Type>(), new IEntityTemplateDecorator[] { null }));
        }

        [Test]
        public void MissingCategory_GetTemplateThrows()
        {
            var catalog = new PrefabCatalog(new Dictionary<EntityCategory, Type> { { EntityCategory.Gem, typeof(LoopPmPlainEntity) } },
                new IEntityTemplateDecorator[0]);
            Assert.IsTrue(catalog.HasTemplate(EntityCategory.Gem));
            Assert.IsFalse(catalog.HasTemplate(EntityCategory.Monster));
            Assert.Throws<InvalidOperationException>(() => catalog.GetTemplate(EntityCategory.Monster));
        }

        [Test]
        public void PrefabOverride_NullUsesGeneratedTemplate_OtherwiseDecoratedInactiveCachedClone()
        {
            var log = new List<string>();
            var catalog = new PrefabCatalog(AllCategories(typeof(LoopPmTickEntity)), new IEntityTemplateDecorator[] { new LoopPmDecorator(log, "d") });
            Assert.AreSame(catalog.GetTemplate(EntityCategory.Monster), catalog.GetTemplate(EntityCategory.Monster, null));

            var prefab = new GameObject("LoopPmOverridePrefab");
            log.Clear();
            GameObject overrideTemplate = catalog.GetTemplate(EntityCategory.Monster, prefab);
            Assert.AreNotSame(prefab, overrideTemplate);
            Assert.IsFalse(overrideTemplate.activeSelf);
            Assert.IsTrue(prefab.activeSelf, "the source prefab is untouched");
            Assert.IsNull(prefab.GetComponent<LoopPmTickEntity>());
            Assert.AreEqual(EntityCategory.Monster, overrideTemplate.GetComponent<LoopPmTickEntity>().Category);
            Assert.IsNotNull(overrideTemplate.GetComponent<SpriteRenderer>());
            CollectionAssert.AreEqual(new[] { "d:Monster:False" }, log);
            Assert.AreSame(overrideTemplate, catalog.GetTemplate(EntityCategory.Monster, prefab), "cached per prefab");
            Assert.Throws<InvalidOperationException>(() => catalog.GetTemplate(EntityCategory.Gem, prefab));
        }

        [Test]
        public void PrewarmCounts_MatchQ13_AndPrewarmAllUsesThemInCategoryOrder()
        {
            Assert.AreEqual(450, PoolPrewarmCounts.CountOf(EntityCategory.Monster));
            Assert.AreEqual(300, PoolPrewarmCounts.CountOf(EntityCategory.PlayerProjectile));
            Assert.AreEqual(100, PoolPrewarmCounts.CountOf(EntityCategory.EnemyProjectile));
            Assert.AreEqual(16, PoolPrewarmCounts.CountOf(EntityCategory.PlayerOrb));
            Assert.AreEqual(16, PoolPrewarmCounts.CountOf(EntityCategory.PlayerZone));
            Assert.AreEqual(8, PoolPrewarmCounts.CountOf(EntityCategory.EnemyZone));
            Assert.AreEqual(600, PoolPrewarmCounts.CountOf(EntityCategory.Gem));
            Assert.AreEqual(16, PoolPrewarmCounts.CountOf(EntityCategory.Chest));

            var catalog = new PrefabCatalog(AllCategories(typeof(LoopPmPlainEntity)), new IEntityTemplateDecorator[0]);
            var pool = new LoopPmPool(null);
            PoolPrewarmCounts.PrewarmAll(pool, catalog);
            Assert.AreEqual(8, pool.Prewarms.Count);
            int i = 0;
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                Assert.AreSame(catalog.GetTemplate(category), pool.Prewarms[i].Key);
                Assert.AreEqual(PoolPrewarmCounts.CountOf(category), pool.Prewarms[i].Value);
                i++;
            }
        }
    }
}
