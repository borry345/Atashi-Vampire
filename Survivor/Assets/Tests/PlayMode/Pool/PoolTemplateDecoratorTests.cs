using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies.Pool;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Pool
{
    /// PoolTemplateDecorator: attaches exactly one PooledObject to every template category (5.6, TAC-F9-7).
    public sealed class PoolTemplateDecoratorTests
    {
        [TearDown]
        public void TearDownObjects()
        {
            PoolTestObjects.DestroyAll();
        }

        [Test]
        public void Decorate_EveryCategory_AddsPooledObjectAndKeepsTemplateInactive()
        {
            var decorator = new PoolTemplateDecorator();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                GameObject template = PoolTestObjects.CreateUndecoratedTemplate(category.ToString());

                decorator.Decorate(template, category);

                PooledObject pooled = template.GetComponent<PooledObject>();
                Assert.IsNotNull(pooled, category.ToString());
                Assert.IsNull(pooled.Template, category.ToString());
                Assert.IsFalse(pooled.IsActive, category.ToString());
                Assert.IsFalse(template.activeSelf, category.ToString());
            }
        }

        [Test]
        public void Decorate_Twice_KeepsSinglePooledObject()
        {
            var decorator = new PoolTemplateDecorator();
            GameObject template = PoolTestObjects.CreateUndecoratedTemplate("Twice");

            decorator.Decorate(template, EntityCategory.Gem);
            decorator.Decorate(template, EntityCategory.Gem);

            Assert.AreEqual(1, template.GetComponents<PooledObject>().Length);
        }

        [Test]
        public void Decorate_Null_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new PoolTemplateDecorator().Decorate(null, EntityCategory.Monster));
        }

        [Test]
        public void Decorate_IsUsableByEveryStrategy()
        {
            GameObject template = PoolTestObjects.CreateUndecoratedTemplate("Shared");
            new PoolTemplateDecorator().Decorate(template, EntityCategory.PlayerProjectile);

            IPoolStrategy[] strategies = { new InstantiatePoolStrategy(), new CustomPoolStrategy(), new UnityObjectPoolStrategy() };
            foreach (IPoolStrategy strategy in strategies)
            {
                GameObject instance = strategy.Acquire(template, Vector2.zero);
                Assert.AreSame(template, instance.GetComponent<PooledObject>().Template, strategy.Kind.ToString());
                Assert.AreEqual(1, instance.GetComponents<PooledObject>().Length, strategy.Kind.ToString());
            }
        }
    }
}
