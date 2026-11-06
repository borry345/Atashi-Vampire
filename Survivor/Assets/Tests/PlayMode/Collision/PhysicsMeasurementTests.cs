using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Collision
{
    /// Engine facts the design depends on, measured on 6000.3.28f1 (U4, U6, U10). Results are also logged once per test.
    public sealed class PhysicsMeasurementTests : CollisionPmFixture
    {
        [Test]
        public void NewRigidbody2D_DefaultsToDiscrete_U4()
        {
            var host = new GameObject(CollisionPmUtil.NamePrefix + "_U4");

            Rigidbody2D rb = host.AddComponent<Rigidbody2D>();

            Debug.Log("[U4] new Rigidbody2D.collisionDetectionMode = " + rb.collisionDetectionMode);
            Assert.AreEqual(CollisionDetectionMode2D.Discrete, rb.collisionDetectionMode);
        }

        // Fast body (r 0.1) crosses a thin stationary target (r 0.1) in one step: 4 units per step vs a radius sum of 0.2.
        [TestCase(CollisionDetectionMode2D.Discrete, false)]
        [TestCase(CollisionDetectionMode2D.Continuous, false)]
        [TestCase(CollisionDetectionMode2D.Discrete, true)]
        [TestCase(CollisionDetectionMode2D.Continuous, true)]
        public void KinematicTrigger_TunnelingThroughThinTarget_U6_U10(CollisionDetectionMode2D mode, bool teleport)
        {
            TriggerRecorder target = CreateTrigger("Target", CollisionLayer.Monster, Vector2.zero, mode);
            TriggerRecorder mover = CreateTrigger("Mover", CollisionLayer.PlayerProjectile, new Vector2(-2f, 0f), mode);
            Rigidbody2D rb = mover.GetComponent<Rigidbody2D>();
            Physics2D.Simulate(StepMath.StepSeconds);
            Assert.AreEqual(0, target.Enters, "no overlap before the move");

            if (teleport)
            {
                rb.position = new Vector2(2f, 0f);
            }
            else
            {
                rb.MovePosition(new Vector2(2f, 0f));
            }

            Physics2D.Simulate(StepMath.StepSeconds);
            int entersInCrossingStep = target.Enters;
            int exitsInCrossingStep = target.Exits;
            Physics2D.Simulate(StepMath.StepSeconds);

            Debug.Log($"[U6/U10] mode={mode} move={(teleport ? "rb.position" : "MovePosition")} "
                + $"crossingStep enter={entersInCrossingStep} exit={exitsInCrossingStep}; "
                + $"nextStep enter={target.Enters} exit={target.Exits}; final x={rb.position.x:R}");
            Assert.AreEqual(2f, rb.position.x, 1e-4f, "the mover reached its target");
            Assert.AreEqual(0, target.Enters, "a kinematic trigger is not swept in either mode (measured)");
            Assert.AreEqual(0, target.Exits);
        }

        private static TriggerRecorder CreateTrigger(string name, CollisionLayer layer, Vector2 position, CollisionDetectionMode2D mode)
        {
            var host = new GameObject(CollisionPmUtil.NamePrefix + "_" + name);
            host.layer = CollisionLayerMapping.UnityLayerOf(layer);
            host.transform.position = position;
            Rigidbody2D rb = host.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            rb.collisionDetectionMode = mode;
            rb.position = position;
            CircleCollider2D circle = host.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.1f;
            return host.AddComponent<TriggerRecorder>();
        }
    }
}
