using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Collision
{
    /// CollisionTemplateDecorator: disabled trigger collider + non-simulated Kinematic body + link, R30 layer (5.6).
    public sealed class CollisionTemplateDecoratorTests : CollisionPmFixture
    {
        [Test]
        public void Decorate_EveryCategory_AddsInactiveEngineComponentsAndLayer()
        {
            var decorator = new CollisionTemplateDecorator();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                GameObject template = Template(category.ToString());

                decorator.Decorate(template, category);

                var layer = (CollisionLayer)Enum.Parse(typeof(CollisionLayer), category.ToString());
                Rigidbody2D rb = template.GetComponent<Rigidbody2D>();
                CircleCollider2D circle = template.GetComponent<CircleCollider2D>();
                Assert.IsNotNull(rb, category.ToString());
                Assert.IsNotNull(circle, category.ToString());
                Assert.IsNotNull(template.GetComponent<CollisionBodyLink>(), category.ToString());
                Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType, category.ToString());
                Assert.IsFalse(rb.simulated, category.ToString());
                Assert.IsTrue(rb.useFullKinematicContacts, category.ToString());
                Assert.IsTrue(circle.isTrigger, category.ToString());
                Assert.IsFalse(circle.enabled, category.ToString());
                Assert.AreEqual(CollisionLayerMapping.UnityLayerOf(layer), template.layer, category.ToString());
                Assert.IsFalse(template.activeSelf, category.ToString());
            }
        }

        [Test]
        public void Decorate_Twice_KeepsOneOfEach()
        {
            var decorator = new CollisionTemplateDecorator();
            GameObject template = Template("Twice");

            decorator.Decorate(template, EntityCategory.Gem);
            decorator.Decorate(template, EntityCategory.Gem);

            Assert.AreEqual(1, template.GetComponents<Rigidbody2D>().Length);
            Assert.AreEqual(1, template.GetComponents<CircleCollider2D>().Length);
            Assert.AreEqual(1, template.GetComponents<CollisionBodyLink>().Length);
        }

        [Test]
        public void Decorate_Null_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CollisionTemplateDecorator().Decorate(null, EntityCategory.Monster));
        }

        [Test]
        public void DecoratedClone_IsUsedAsIsByTheSynchronizer()
        {
            GameObject template = Template("Clone");
            new CollisionTemplateDecorator().Decorate(template, EntityCategory.Monster);
            GameObject clone = UnityEngine.Object.Instantiate(template);
            clone.name = CollisionPmUtil.NamePrefix + "_Clone";
            clone.SetActive(true);
            CollisionBody body = CollisionPmUtil.Body(1, CollisionLayer.Monster, 1f, 2f, 0.4f, withHost: false);
            body.Host = clone;
            var bodies = new PhysicsBodySynchronizer(CollisionPmUtil.EngineRequirements());

            bodies.Register(body);

            Assert.AreEqual(1, clone.GetComponents<Rigidbody2D>().Length);
            Assert.AreEqual(1, clone.GetComponents<CircleCollider2D>().Length);
            Assert.IsTrue(clone.GetComponent<CircleCollider2D>().enabled);
            Assert.IsTrue(clone.GetComponent<Rigidbody2D>().simulated);
        }

        private static GameObject Template(string suffix)
        {
            var template = new GameObject(CollisionPmUtil.NamePrefix + "_Template_" + suffix);
            template.SetActive(false);
            return template;
        }
    }
}
