using System;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class BoundaryInterfaceTests
    {
        // Test double implementing the documented contract of both interfaces.
        private sealed class Double : IDamageable, IContactDamageSource
        {
            public float health;

            public Double(float health, float contactDamage)
            {
                this.health = health;
                ContactDamage = contactDamage;
            }

            public bool IsAlive { get; private set; } = true;

            public float ContactDamage { get; }

            public void ApplyDamage(float amount, long step)
            {
                if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount));
                }

                if (!IsAlive)
                {
                    return;
                }

                health -= amount;
                if (health <= 0f)
                {
                    health = 0f;
                    IsAlive = false;
                }
            }
        }

        [Test]
        public void ApplyDamage_ReducesHealth_AndDiesAtZero()
        {
            var d = new Double(10f, 0f);
            IDamageable target = d;

            target.ApplyDamage(4f, 1);
            Assert.AreEqual(6f, d.health);
            Assert.IsTrue(target.IsAlive);

            target.ApplyDamage(6f, 2);
            Assert.AreEqual(0f, d.health);
            Assert.IsFalse(target.IsAlive);
        }

        [Test]
        public void ApplyDamage_WhileNotAlive_IsIgnored()
        {
            var d = new Double(5f, 0f);
            IDamageable target = d;
            target.ApplyDamage(10f, 1);
            Assert.IsFalse(target.IsAlive);
            float after = d.health;

            target.ApplyDamage(3f, 1);

            Assert.AreEqual(after, d.health);
            Assert.IsFalse(target.IsAlive);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ApplyDamage_InvalidAmount_Throws(float amount)
        {
            IDamageable target = new Double(10f, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => target.ApplyDamage(amount, 1));
        }

        [Test]
        public void ContactDamage_ReadableThroughInterface()
        {
            IContactDamageSource source = new Double(1f, 7.5f);
            Assert.AreEqual(7.5f, source.ContactDamage);
        }
    }
}
