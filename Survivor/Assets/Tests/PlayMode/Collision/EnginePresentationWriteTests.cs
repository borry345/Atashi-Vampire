using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using static Survivor.Tests.PlayMode.Collision.CollisionPmUtil;

namespace Survivor.Tests.PlayMode.Collision
{
    /// Regression for the wave 4 combination divergence: PresentationSync writes transform.position once per frame for every
    /// body whose Body.DrivesTransform is false (5c.3 #6). A transform write on an engine-simulated host is synced into the
    /// body at the next Physics2D.Simulate, which drops that step's MovePosition, so the engine strategy saw the body one step
    /// late. Engine-mode bodies must therefore report DrivesTransform and keep reporting the same events as the self strategy.
    public sealed class EnginePresentationWriteTests : CollisionPmFixture
    {
        private const int Steps = 120;
        private const int MonsterCount = 40;
        private const float MaxRoundingSq = 1e-10f;
        private const int StepsPerFrame = 6;

        [Test]
        public void EngineBodies_DriveTheirTransform_SelfAndPhysicsTargetBodiesDoNot()
        {
            CollisionPmWorld engine = CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete);
            CollisionBody monster = engine.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 0.5f));
            CollisionBody orb = engine.Add(Body(2, CollisionLayer.PlayerOrb, 3f, 0f, 0.5f));
            Assert.IsTrue(monster.DrivesTransform, "engine mode: the simulation owns the monster transform");
            Assert.IsTrue(orb.DrivesTransform, "engine mode: the simulation owns the orb transform");

            engine.Bodies.ApplyRequirements(PhysicsTargetRequirements());
            Assert.IsFalse(orb.DrivesTransform, "collider off: presentation writes the transform");
            engine.Bodies.ApplyRequirements(EngineRequirements());
            Assert.IsTrue(orb.DrivesTransform, "collider back on under the engine strategy");

            engine.Remove(monster);
            Assert.IsFalse(monster.DrivesTransform, "an unregistered body is never driven by the simulation");
            engine.Strategy.Clear();
            Assert.IsFalse(orb.DrivesTransform, "a body dropped by the engine strategy is no longer driven by the simulation");

            DestroyAll();
            var physicsTarget = new PhysicsBodySynchronizer(PhysicsTargetRequirements());
            CollisionBody queried = Body(1, CollisionLayer.Monster, 0f, 0f, 0.5f);
            physicsTarget.Register(queried);
            Assert.IsTrue(queried.Host.GetComponent<CircleCollider2D>().enabled, "Physics2D target: the monster collider is on");
            Assert.IsFalse(queried.DrivesTransform, "no Simulate without the engine strategy: presentation writes the transform");

            CollisionPmWorld self = CollisionPmWorld.Self();
            CollisionBody selfMonster = self.Add(Body(1, CollisionLayer.Monster, 0f, 0f, 0.5f));
            Assert.IsFalse(selfMonster.DrivesTransform, "self mode: presentation writes the transform");
        }

        [Test]
        public void PresentationWritesEveryFrame_EngineEventsStillEqualSelf()
        {
            List<string> selfLog = Run(CollisionPmWorld.Self(), out _);
            DestroyAll();
            List<string> engineLog = Run(CollisionPmWorld.Engine(CollisionDetectionMode2D.Discrete), out List<string> lagged);

            Assert.That(selfLog.Count, Is.GreaterThanOrEqualTo(4), "the scenario must produce Enter and Exit events");
            Assert.AreEqual(string.Join("\n", selfLog), string.Join("\n", engineLog), "engine events differ from self events");
            Assert.IsEmpty(lagged, "engine rb.position must equal body.Position after every Simulate (step:id)");
        }

        // MonsterCount monsters walk toward a few still orbs along non-representable per-step offsets, so Box2D's kinematic
        // integration sometimes lands a float off the MovePosition target. After every StepsPerFrame steps a presentation pass
        // writes the transform of each body that does not report DrivesTransform, as PresentationSync.LateUpdate does once per
        // frame; such a write differs from the body pose by that rounding and turns the next Simulate into a teleport.
        private static List<string> Run(CollisionPmWorld world, out List<string> lagged)
        {
            var monsters = new List<CollisionBody>();
            for (int i = 0; i < MonsterCount; i++)
            {
                monsters.Add(world.Add(Body(1 + i, CollisionLayer.Monster, 5.3f + (0.173f * i), -3.1f + (0.151f * i), 0.5f)));
            }

            world.Add(Body(MonsterCount + 1, CollisionLayer.PlayerOrb, 0.41f, 0.27f, 0.5f));
            world.Add(Body(MonsterCount + 2, CollisionLayer.PlayerOrb, 2.07f, -1.33f, 0.5f));
            world.Add(Body(MonsterCount + 3, CollisionLayer.PlayerOrb, 2.93f, 1.19f, 0.5f));
            var log = new List<string>();
            lagged = new List<string>();

            // Warm-up step without motion: hosts created by Register settle before the scripted motion starts.
            foreach (CollisionEvent e in world.Step(0))
            {
                log.Add(e.ToString());
            }

            for (int step = 1; step <= Steps; step++)
            {
                for (int i = 0; i < monsters.Count; i++)
                {
                    monsters[i].Position += new Vector2(-0.0731f - (0.0013f * i), 0.0517f - (0.0029f * i));
                }

                foreach (CollisionEvent e in world.Step(step))
                {
                    log.Add(e.ToString());
                }

                foreach (CollisionBody body in world.Registered)
                {
                    Rigidbody2D rb = body.Host.GetComponent<Rigidbody2D>();
                    if (rb.simulated && (rb.position - body.Position).sqrMagnitude > MaxRoundingSq)
                    {
                        lagged.Add(step + ":" + body.Id);
                    }
                }

                if (step % StepsPerFrame == 0)
                {
                    foreach (CollisionBody body in world.Registered)
                    {
                        if (!body.DrivesTransform)
                        {
                            Transform t = body.Host.transform;
                            t.position = new Vector3(body.Position.x, body.Position.y, t.position.z);
                        }
                    }
                }
            }

            return log;
        }
    }
}
