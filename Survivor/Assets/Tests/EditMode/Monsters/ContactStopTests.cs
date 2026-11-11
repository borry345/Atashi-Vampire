using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Progression;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Monster-player contact stop (R32 StepMove.MoveStopAtContact; TAC-F3-17, F3-18, F3-19 EM part).
    public sealed class ContactStopTests
    {
        private const float ContactTolerance = 1e-5f;
        private MonEmRig _rig;
        private FlowEmRig _flow;
        private FlowEmRig _flowB;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            _flow?.Dispose();
            _flow = null;
            _flowB?.Dispose();
            _flowB = null;
        }

        [Test]
        public void MoveStopAtContact_CrossingMove_StopsTouchingOnTheSegment()
        {
            var cases = new[]
            {
                (from: new Vector2(5f, 0f), d: new Vector2(-10f, 0f), player: Vector2.zero, rM: 0.45f, rP: 0.5f),
                (from: new Vector2(1.2f, 0f), d: new Vector2(-0.3f, 0f), player: Vector2.zero, rM: 0.5f, rP: 0.5f),
                (from: new Vector2(3f, 2f), d: new Vector2(-3.1f, -2.05f), player: new Vector2(0.1f, -0.2f), rM: 1.5f, rP: 0.5f),
                (from: new Vector2(-7.3f, 11.9f), d: new Vector2(6f, -11f), player: new Vector2(-1.25f, 1.5f), rM: 0.45f, rP: 0.5f),
                (from: new Vector2(4f, 0f), d: new Vector2(-8f, 0f), player: new Vector2(0f, 0.6f), rM: 0.5f, rP: 0.5f),
            };
            foreach ((Vector2 from, Vector2 d, Vector2 player, float rM, float rP) in cases)
            {
                Vector2 p = StepMove.MoveStopAtContact(from, d, player, rM, rP);
                string label = "from " + from + " d " + d;
                Assert.IsTrue(CircleMath.Overlaps(p, rM, player, rP), "(a) R5 holds at the stop point: " + label);
                Assert.GreaterOrEqual((p - player).magnitude, rM + rP - ContactTolerance, "(a) not inside the contact distance: " + label);
                Vector2 travelled = p - from;
                float cross = travelled.x * d.y - travelled.y * d.x;
                Assert.AreEqual(0f, cross / d.magnitude, 1e-5f, "(a) on the displacement line: " + label);
                float along = Vector2.Dot(travelled, d) / d.sqrMagnitude;
                Assert.Greater(along, 0f, label);
                Assert.Less(along, 1f, "(a) stops before the end of the move: " + label);
            }
        }

        [Test]
        public void MoveStopAtContact_MissingTouchingAndLeavingMoves()
        {
            Vector2 player = Vector2.zero;
            Assert.AreEqual(new Vector2(4f, 0f), StepMove.MoveStopAtContact(new Vector2(5f, 0f), new Vector2(-1f, 0f), player, 0.45f, 0.5f),
                "(b) short of the contact circle: from + d");
            Assert.AreEqual(new Vector2(-2f, 1f), StepMove.MoveStopAtContact(new Vector2(2f, 1f), new Vector2(-4f, 0f), player, 0.45f, 0.5f),
                "(b) passing by outside the circle: from + d");
            Assert.AreEqual(new Vector2(0.5f, 0f), StepMove.MoveStopAtContact(new Vector2(0.5f, 0f), new Vector2(-1f, 0f), player, 0.45f, 0.5f),
                "(c) already touching: no move toward");
            Assert.AreEqual(new Vector2(0.5f, 0f), StepMove.MoveStopAtContact(new Vector2(0.5f, 0f), new Vector2(1f, 0f), player, 0.45f, 0.5f),
                "(c) already touching: no retreat");
            Assert.AreEqual(new Vector2(0.95f, 0f), StepMove.MoveStopAtContact(new Vector2(0.95f, 0f), new Vector2(-0.1f, 0f), player, 0.45f, 0.5f),
                "(c) exactly at the contact distance counts as touching");
            Assert.AreEqual(new Vector2(3f, 0f), StepMove.MoveStopAtContact(new Vector2(2f, 0f), new Vector2(1f, 0f), player, 0.45f, 0.5f),
                "(d) moving away: from + d");
            Assert.AreEqual(new Vector2(2f, 0f), StepMove.MoveStopAtContact(new Vector2(2f, 0f), Vector2.zero, player, 0.45f, 0.5f),
                "zero displacement: from");
        }

        [Test]
        public void MoveStopAtContact_10000RandomCrossingMoves_AlwaysStopTouching()
        {
            var random = new System.Random(20261111);
            int checkedPairs = 0;
            int stopped = 0;
            for (int i = 0; i < 10000; i++)
            {
                var player = new Vector2(Range(random, -60f, 60f), Range(random, -60f, 60f));
                float rM = Range(random, 0.1f, 2f);
                float rP = Range(random, 0.1f, 1f);
                float r = rM + rP;
                float angle = Range(random, 0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = r + Range(random, 0.001f, 20f);
                Vector2 from = player + outward * distance;
                if (CircleMath.Overlaps(from, rM, player, rP))
                {
                    continue;
                }

                // Aim within half the contact radius of the player and overshoot it, so the segment crosses the contact circle.
                var side = new Vector2(-outward.y, outward.x);
                Vector2 aim = player + side * (Range(random, -0.5f, 0.5f) * r);
                Vector2 d = (aim - from) * Range(random, 1.01f, 3f);
                Vector2 p = StepMove.MoveStopAtContact(from, d, player, rM, rP);
                checkedPairs++;
                Assert.IsTrue(CircleMath.Overlaps(p, rM, player, rP), "(e) pair " + i + ": R5 holds at the stop point");
                if (p != from + d)
                {
                    stopped++;
                }
            }

            Assert.Greater(checkedPairs, 9900);
            Assert.AreEqual(checkedPairs, stopped, "every crossing move stops short of its end point");
        }

        [Test]
        public void MoveStopAtContact_10000SlowMovesFarFromTheOrigin_AlwaysStopTouching()
        {
            // A slow monster (|d| of a few hundredths) just outside the contact circle, far from the world origin: one ulp of the
            // ratio moves the float position by much less than one ulp, so the ratio search must still find a touching point.
            var random = new System.Random(20261112);
            for (int i = 0; i < 10000; i++)
            {
                var player = new Vector2(Range(random, -1500f, 1500f), Range(random, -1500f, 1500f));
                float rM = Range(random, 0.1f, 2f);
                float rP = Range(random, 0.1f, 1f);
                float r = rM + rP;
                float angle = Range(random, 0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float gap = Range(random, 0f, 0.05f);
                Vector2 from = player + outward * (r + gap);
                if (CircleMath.Overlaps(from, rM, player, rP))
                {
                    continue;
                }

                Vector2 d = -outward * (gap + Range(random, 0.002f, 0.04f));
                Vector2 p = StepMove.MoveStopAtContact(from, d, player, rM, rP);
                Assert.IsTrue(CircleMath.Overlaps(p, rM, player, rP), "pair " + i + ": R5 holds at the stop point");
                Assert.GreaterOrEqual((p - player).magnitude, r - 1e-3f, "pair " + i + ": stopped at the circle, not deep inside");
            }
        }

        [Test]
        public void M1_StopsAtContactDistance_StaysThere_AndDoesNotMoveWhenThePlayerWalksIntoIt()
        {
            _rig = new MonEmRig();
            Assert.AreEqual(0.5f, _rig.Spawner.PlayerRadius, "Bind read the player body radius of the data");
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(3f, 0f));
            float contact = m1.Body.Radius + _rig.Spawner.PlayerRadius;

            _rig.Step(120);
            Vector2 stop = m1.Position;
            AssertTouching(m1, _rig.Player, _rig.Spawner.PlayerRadius);
            Assert.AreEqual(0f, stop.y);
            Assert.GreaterOrEqual(stop.x, contact - ContactTolerance);

            _rig.Step(10);
            Assert.AreEqual(stop, m1.Position, "no movement while touching");

            _rig.Player = new Vector2(0.3f, 0f);
            _rig.Step();
            Assert.AreEqual(stop, m1.Position, "the player walked into it: no retreat, no push");

            _rig.Player = new Vector2(-2f, 0f);
            _rig.Step();
            Assert.Less(m1.Position.x, stop.x, "the player left: chasing resumes");
            _rig.Step(120);
            AssertTouching(m1, _rig.Player, _rig.Spawner.PlayerRadius);
        }

        [Test]
        public void M2_ChargeStopsAtFirstIntersection_PhaseTimingUnchanged()
        {
            _rig = new MonEmRig();
            Monster m2 = _rig.SpawnMonster("M2", new Vector2(5f, 0f));
            ChargeDefinition charge = m2.Definition.Charge;

            _rig.Step();
            long chargeStart = _rig.Clock.Step - 1;
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase);
            Assert.AreEqual(chargeStart + charge.DurationSteps, m2.State.PhaseEndStep);

            // Move the player off the fixed charge line: the line still crosses the contact circle (offset 0.6 < 1.0).
            _rig.Player = new Vector2(0f, 0.6f);
            float contact = m2.Body.Radius + _rig.Spawner.PlayerRadius;
            float firstX = Mathf.Sqrt(contact * contact - 0.36f);
            _rig.Step(charge.DurationSteps - 2);
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase, "still charging");
            Assert.AreEqual(0f, m2.Position.y, "the charge keeps its fixed direction");
            Assert.GreaterOrEqual(m2.Position.x, firstX - ContactTolerance, "stopped at the first intersection, not past it");
            AssertTouching(m2, _rig.Player, _rig.Spawner.PlayerRadius);
            Vector2 stop = m2.Position;

            _rig.Step();
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase, "step chargeStart + DurationSteps - 1");
            _rig.Step();
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Cooldown, m2.State.Phase, "Cooldown in the same step as without the stop");
            Assert.AreEqual(chargeStart + charge.DurationSteps + charge.CooldownSteps, m2.State.PhaseEndStep);
            Assert.AreEqual(stop, m2.Position, "cooldown chase while touching: no movement");
        }

        [Test]
        public void M3_ApproachAndM4_Chase_StopAtTheirOwnContactDistance()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"attackRange\": 8,", "\"attackRange\": 0.5,"));
            _rig = new MonEmRig(data, report);
            Monster m3 = _rig.SpawnMonster("M3", new Vector2(0f, 3f));
            Monster boss = _rig.SpawnMonster("M4", new Vector2(-2.6f, 0f));

            _rig.Step(150);
            AssertTouching(m3, _rig.Player, _rig.Spawner.PlayerRadius);
            AssertTouching(boss, _rig.Player, _rig.Spawner.PlayerRadius);
            Assert.AreEqual(0, _rig.Live<EnemyProjectile>().Count, "M3 never got inside its 0.5 attack range");
            Assert.AreEqual(2f, boss.Body.Radius + _rig.Spawner.PlayerRadius, "boss contact distance = 1.5 + 0.5");
        }

        [Test]
        public void StoppedM1_DamagesThePlayer_OncePerInvulnerabilityWindow_AtMostOncePerStep()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 100000,"));
            _flow = new FlowEmRig(data, report, start: false);
            _flow.PolicyFactory = () => new MonEmNoSpawnPolicy();
            _flow.Input.Still = true;
            _flow.StartRun();
            Vector2 player = _flow.Player.Position;
            float playerRadius = data.PlayerStats.Radius;
            int invulnerability = data.PlayerStats.InvulnerabilitySteps;
            Assert.Greater(invulnerability, 1);
            _flow.Enqueue(PendingCommand.SpawnMonstersNow("M1", 1, player + new Vector2(3f, 0f), 0f, true));
            _flow.Enqueue(PendingCommand.SpawnMonstersNow("M1", 1, player + new Vector2(-3f, 0f), 0f, true));

            long firstContact = -1;
            var hits = new List<long>();
            float hp = _flow.Health.Hp;
            for (int i = 0; i < 300; i++)
            {
                _flow.Step();
                long step = _flow.Clock.Step - 1;
                List<Monster> monsters = _flow.Live<Monster>();
                Assert.AreEqual(2, monsters.Count);
                if (firstContact < 0 && CircleMath.Overlaps(monsters[0].Position, monsters[0].Body.Radius, player, playerRadius))
                {
                    firstContact = step;
                }

                float lost = hp - _flow.Health.Hp;
                hp = _flow.Health.Hp;
                if (lost > 0f)
                {
                    Assert.AreEqual(monsters[0].ContactDamage, lost, "two touching monsters: still one hit per step");
                    hits.Add(step);
                }
            }

            Assert.AreEqual(player, _flow.Player.Position, "the player stood still");
            foreach (Monster m in _flow.Live<Monster>())
            {
                AssertTouching(m, player, playerRadius);
            }

            Assert.Greater(firstContact, 0L);
            Assert.GreaterOrEqual(hits.Count, 3);
            Assert.AreEqual(firstContact, hits[0], "the stopped monster hits in the step it reaches the contact distance");
            for (int i = 1; i < hits.Count; i++)
            {
                Assert.AreEqual(invulnerability, hits[i] - hits[i - 1], "hit again right after each invulnerability window");
            }
        }

        [Test]
        public void SameSeedTwice_MonstersReachThePlayer_SameHashEveryStep()
        {
            // Tough M1s so the starting weapon does not kill them before they reach the player.
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 100000,"));
            _flow = new FlowEmRig(data, report, start: false);
            (data, report) = MonEmData.Load((DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 100000,"));
            _flowB = new FlowEmRig(data, report, start: false);
            FlowEmRig[] runs = { _flow, _flowB };
            var hashes = new List<ulong>[2];
            int touchingSteps = 0;
            for (int r = 0; r < 2; r++)
            {
                FlowEmRig rig = runs[r];
                rig.Input.Still = true;
                rig.StartRun();
                hashes[r] = new List<ulong>();
                float playerRadius = rig.Data.Current.PlayerStats.Radius;
                for (int i = 0; i < 1000; i++)
                {
                    List<ulong> one = rig.Play(1);
                    if (one.Count == 0)
                    {
                        break;
                    }

                    hashes[r].Add(one[0]);
                    if (r == 0 && AnyTouching(rig, playerRadius))
                    {
                        touchingSteps++;
                    }
                }
            }

            Assert.Greater(touchingSteps, 0, "the scenario has monsters stopped at the player");
            Assert.Greater(hashes[0].Count, 0);
            CollectionAssert.AreEqual(hashes[0], hashes[1], "TAC-F3-19: same seed, same input, same state hash");
            Debug.Log("[MonEm] contact-stop scenario: " + hashes[0].Count + " steps, " + touchingSteps + " with a touching monster, final hash 0x"
                + hashes[0][hashes[0].Count - 1].ToString("X16"));
        }

        private static bool AnyTouching(FlowEmRig rig, float playerRadius)
        {
            foreach (Monster m in rig.Live<Monster>())
            {
                if (CircleMath.Overlaps(m.Position, m.Body.Radius, rig.Player.Position, playerRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertTouching(Monster m, Vector2 player, float playerRadius)
        {
            float contact = m.Body.Radius + playerRadius;
            Assert.IsTrue(CircleMath.Overlaps(m.Position, m.Body.Radius, player, playerRadius), m.Definition.Id + " touches the player (R5)");
            Assert.GreaterOrEqual((m.Position - player).magnitude, contact - ContactTolerance, m.Definition.Id + " not inside the contact distance");
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
