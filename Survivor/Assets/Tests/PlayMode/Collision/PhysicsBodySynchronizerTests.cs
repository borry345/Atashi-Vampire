using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;
using static Survivor.Tests.PlayMode.Collision.CollisionPmUtil;

namespace Survivor.Tests.PlayMode.Collision
{
    /// PhysicsBodySynchronizer: registration (C1), teleport, R19 collider toggling, syncs (U32, U11) and contract errors.
    public sealed class PhysicsBodySynchronizerTests : CollisionPmFixture
    {
        [Test]
        public void Register_BareHost_GetsEngineComponentsAndLayer()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody player = Body(1, CollisionLayer.PlayerBody, 0f, 0f, 0.45f);

            bodies.Register(player);

            Rigidbody2D rb = player.Host.GetComponent<Rigidbody2D>();
            CircleCollider2D circle = player.Host.GetComponent<CircleCollider2D>();
            Assert.IsNotNull(rb);
            Assert.IsNotNull(circle);
            Assert.IsNotNull(player.Host.GetComponent<CollisionBodyLink>());
            Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType);
            Assert.IsTrue(rb.useFullKinematicContacts);
            Assert.AreEqual(CollisionDetectionMode2D.Discrete, rb.collisionDetectionMode);
            Assert.IsTrue(circle.isTrigger);
            Assert.AreEqual(0.45f, circle.radius);
            Assert.AreEqual(CollisionLayerMapping.UnityLayerOf(CollisionLayer.PlayerBody), player.Host.layer);
            Assert.IsFalse(player.DrivesTransform);
            Assert.AreEqual(0, player.PhysicsSlot);
            Assert.AreEqual(1, bodies.Count);
        }

        [Test]
        public void Register_TeleportsRigidbodyAndTransform_AndIsVisibleToQueriesAtOnce()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody monster = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            monster.Host.transform.position = new Vector3(-50f, -50f, 3f);
            monster.Position = new Vector2(4f, 5f);

            bodies.Register(monster);

            Assert.AreEqual(new Vector2(4f, 5f), monster.Host.GetComponent<Rigidbody2D>().position);
            Assert.AreEqual(new Vector3(4f, 5f, 3f), monster.Host.transform.position);
            Collider2D hit = Physics2D.OverlapCircle(new Vector2(4f, 5f), 0.01f, CollisionLayerMapping.MaskOf(CollisionLayer.Monster));
            Assert.AreSame(monster.Host.GetComponent<CircleCollider2D>(), hit);
        }

        [Test]
        public void Register_EnablesCollidersPerRequirements()
        {
            AssertEnabledPerLayer(SelfRequirements(), monster: false, other: false);
            AssertEnabledPerLayer(PhysicsTargetRequirements(), monster: true, other: false);
            AssertEnabledPerLayer(EngineRequirements(), monster: true, other: true);
        }

        [Test]
        public void ApplyRequirements_TogglesRegisteredBodies()
        {
            var bodies = new PhysicsBodySynchronizer(SelfRequirements());
            CollisionBody monster = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            CollisionBody gem = Body(2, CollisionLayer.Gem, 1f, 0f, 0.2f);
            bodies.Register(monster);
            bodies.Register(gem);

            bodies.ApplyRequirements(EngineRequirements());
            Assert.IsTrue(IsOn(monster));
            Assert.IsTrue(IsOn(gem));

            bodies.ApplyRequirements(PhysicsTargetRequirements());
            Assert.IsTrue(IsOn(monster));
            Assert.IsFalse(IsOn(gem));
            Assert.AreEqual(PhysicsTargetRequirements().AllColliders, bodies.Requirements.AllColliders);

            bodies.ApplyRequirements(SelfRequirements());
            Assert.IsFalse(IsOn(monster));
        }

        [Test]
        public void ColliderSwitchedOn_IsFoundByOverlapCircleImmediately_U11()
        {
            var bodies = new PhysicsBodySynchronizer(SelfRequirements());
            CollisionBody monster = Body(1, CollisionLayer.Monster, 2f, 3f, 0.3f);
            bodies.Register(monster);
            int mask = CollisionLayerMapping.MaskOf(CollisionLayer.Monster);
            Assert.IsNull(Physics2D.OverlapCircle(new Vector2(2f, 3f), 0.05f, mask));

            monster.Position = new Vector2(-2f, 1f);
            bodies.ApplyRequirements(PhysicsTargetRequirements());

            Assert.AreSame(monster.Host.GetComponent<CircleCollider2D>(), Physics2D.OverlapCircle(new Vector2(-2f, 1f), 0.05f, mask));
        }

        [Test]
        public void Unregister_DisablesAndForgets()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody a = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            CollisionBody b = Body(2, CollisionLayer.Gem, 1f, 0f, 0.2f);
            bodies.Register(a);
            bodies.Register(b);
            CircleCollider2D circle = a.Host.GetComponent<CircleCollider2D>();

            bodies.Unregister(a);

            Assert.IsFalse(IsOn(a));
            Assert.AreEqual(-1, a.PhysicsSlot);
            Assert.AreEqual(0, b.PhysicsSlot, "swap-remove moves the last body into the freed slot");
            Assert.IsFalse(bodies.TryGetBody(circle, out _));
            Assert.AreEqual(1, bodies.Count);
            bodies.Register(a);
            Assert.IsTrue(bodies.TryGetBody(circle, out CollisionBody found));
            Assert.AreSame(a, found);
        }

        [Test]
        public void TryGetBody_NullOrForeignCollider_IsFalse()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            var foreign = new GameObject(NamePrefix + "_Foreign").AddComponent<CircleCollider2D>();

            Assert.IsFalse(bodies.TryGetBody(null, out CollisionBody none));
            Assert.IsNull(none);
            Assert.IsFalse(bodies.TryGetBody(foreign, out _));
        }

        [Test]
        public void SyncMonstersForQuery_MovesEnabledMonstersOnly_VisibleWithoutSimulate_U32()
        {
            var bodies = new PhysicsBodySynchronizer(PhysicsTargetRequirements());
            CollisionBody monster = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            CollisionBody shot = Body(2, CollisionLayer.PlayerProjectile, 0f, 0f, 0.1f);
            bodies.Register(monster);
            bodies.Register(shot);
            monster.Position = new Vector2(6f, -1f);
            shot.Position = new Vector2(6f, -1f);

            bodies.SyncMonstersForQuery();

            Assert.AreEqual(new Vector2(6f, -1f), monster.Host.GetComponent<Rigidbody2D>().position);
            Assert.AreEqual(Vector2.zero, shot.Host.GetComponent<Rigidbody2D>().position);
            Assert.AreSame(monster.Host.GetComponent<CircleCollider2D>(),
                Physics2D.OverlapCircle(new Vector2(6f, -1f), 0.05f, CollisionLayerMapping.MaskOf(CollisionLayer.Monster)));
        }

        [Test]
        public void OnPhase_SyncsOnlyAtBuildIndex_WhenRequired()
        {
            var required = new PhysicsBodySynchronizer(PhysicsTargetRequirements());
            var notRequired = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody a = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            CollisionBody b = Body(2, CollisionLayer.Monster, 0f, 0f, 0.3f);
            required.Register(a);
            notRequired.Register(b);
            a.Position = new Vector2(3f, 0f);
            b.Position = new Vector2(3f, 0f);

            foreach (SimulationPhase phase in Enum.GetValues(typeof(SimulationPhase)))
            {
                if (phase != SimulationPhase.BuildIndex)
                {
                    required.OnPhase(phase, default);
                }
            }

            Assert.AreEqual(Vector2.zero, a.Host.GetComponent<Rigidbody2D>().position);
            required.OnPhase(SimulationPhase.BuildIndex, default);
            notRequired.OnPhase(SimulationPhase.BuildIndex, default);
            Assert.AreEqual(new Vector2(3f, 0f), a.Host.GetComponent<Rigidbody2D>().position);
            Assert.AreEqual(Vector2.zero, b.Host.GetComponent<Rigidbody2D>().position);
        }

        [Test]
        public void SyncAllForSimulation_MovePositionLandsInTheNextSimulate_AndUpdatesRadius()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody zone = Body(1, CollisionLayer.PlayerZone, 0f, 0f, 1f);
            bodies.Register(zone);
            zone.Position = new Vector2(0.5f, 0.25f);
            zone.Radius = 1.5f;

            bodies.SyncAllForSimulation();
            Rigidbody2D rb = zone.Host.GetComponent<Rigidbody2D>();
            Assert.AreEqual(Vector2.zero, rb.position, "MovePosition is applied by the next Simulate");
            Assert.AreEqual(1.5f, zone.Host.GetComponent<CircleCollider2D>().radius);

            Physics2D.Simulate(StepMath.StepSeconds);

            Assert.AreEqual(0.5f, rb.position.x, 1e-5f);
            Assert.AreEqual(0.25f, rb.position.y, 1e-5f);
        }

        [Test]
        public void DetectionMode_AppliesToRegisteredAndNewBodies()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody a = Body(1, CollisionLayer.Monster, 0f, 0f, 0.3f);
            bodies.Register(a);

            bodies.DetectionMode = CollisionDetectionMode2D.Continuous;
            CollisionBody b = Body(2, CollisionLayer.Monster, 0f, 0f, 0.3f);
            bodies.Register(b);

            Assert.AreEqual(CollisionDetectionMode2D.Continuous, a.Host.GetComponent<Rigidbody2D>().collisionDetectionMode);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, b.Host.GetComponent<Rigidbody2D>().collisionDetectionMode);
        }

        [Test]
        public void ContractViolations_LogAndThrow()
        {
            var bodies = new PhysicsBodySynchronizer(EngineRequirements());
            CollisionBody body = Body(1, CollisionLayer.Gem, 0f, 0f, 0.2f);
            bodies.Register(body);
            CollisionBody stranger = Body(2, CollisionLayer.Gem, 0f, 0f, 0.2f);
            CollisionBody hostless = Body(3, CollisionLayer.Gem, 0f, 0f, 0.2f, withHost: false);
            CollisionBody sameHost = Body(4, CollisionLayer.Gem, 0f, 0f, 0.2f, withHost: false);
            sameHost.Host = body.Host;

            Assert.Throws<ArgumentNullException>(() => bodies.Register(null));
            Assert.Throws<ArgumentNullException>(() => bodies.Unregister(null));
            Assert.Throws<ArgumentNullException>(() => bodies.NotifyRadiusChanged(null));
            ExpectCollisionLog(LogType.Error, "ERROR", @"PhysicsBodySynchronizer\.Register: body 1:1 is already registered");
            Assert.Throws<InvalidOperationException>(() => bodies.Register(body));
            ExpectCollisionLog(LogType.Error, "ERROR", @"PhysicsBodySynchronizer\.Register: body 3:1 has no Host GameObject\.");
            Assert.Throws<InvalidOperationException>(() => bodies.Register(hostless));
            ExpectCollisionLog(LogType.Error, "ERROR", @"PhysicsBodySynchronizer\.Register: host '.*' already carries a registered body\.");
            Assert.Throws<InvalidOperationException>(() => bodies.Register(sameHost));
            Assert.AreEqual(-1, sameHost.PhysicsSlot);
            ExpectCollisionLog(LogType.Error, "ERROR", @"PhysicsBodySynchronizer\.Unregister: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => bodies.Unregister(stranger));
            ExpectCollisionLog(LogType.Error, "ERROR", @"PhysicsBodySynchronizer\.NotifyRadiusChanged: body 2:1 is not registered\.");
            Assert.Throws<InvalidOperationException>(() => bodies.NotifyRadiusChanged(stranger));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertEnabledPerLayer(PhysicsBodyRequirements requirements, bool monster, bool other)
        {
            var bodies = new PhysicsBodySynchronizer(requirements);
            int index = 1;
            foreach (CollisionLayer layer in Enum.GetValues(typeof(CollisionLayer)))
            {
                CollisionBody body = Body(index++, layer, index, 0f, 0.2f);
                bodies.Register(body);
                bool expected = layer == CollisionLayer.Monster ? monster : other;
                Assert.AreEqual(expected, IsOn(body), layer.ToString());
            }

            DestroyAll();
        }

        private static bool IsOn(CollisionBody body)
        {
            bool collider = body.Host.GetComponent<CircleCollider2D>().enabled;
            bool simulated = body.Host.GetComponent<Rigidbody2D>().simulated;
            Assert.AreEqual(collider, simulated, "collider.enabled and rb.simulated move together (C2)");
            return collider;
        }
    }
}
