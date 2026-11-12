using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Progression;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Monster-monster separation pass (5.12.1 MonsterSeparation, BuildIndex order -10; TAC-F3-22, F3-23, F3-24 EM part).
    public sealed class MonsterSeparationTests
    {
        private const float Tolerance = 1e-5f;
        private MonEmRig _rig;
        private MonEmRig _rigB;
        private FlowEmRig _flow;
        private FlowEmRig _flowB;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            _rigB?.Dispose();
            _rigB = null;
            _flow?.Dispose();
            _flow = null;
            _flowB?.Dispose();
            _flowB = null;
        }

        [Test]
        public void Constructor_NullRegistry_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MonsterSeparation(null));
        }

        [Test]
        public void OtherPhases_DoNotMoveOverlappingMonsters()
        {
            _rig = new MonEmRig();
            List<Monster> m = SpawnAll(_rig, ("M1", new Vector2(2f, 1f)), ("M1", new Vector2(2.3f, 1f)));
            var separation = new MonsterSeparation(_rig.Registry);
            for (SimulationPhase phase = SimulationPhase.Begin; phase <= SimulationPhase.End; phase++)
            {
                if (phase == SimulationPhase.BuildIndex)
                {
                    continue;
                }

                separation.OnPhase(phase, _rig.Context);
                AssertSameBits(new Vector2(2f, 1f), m[0].Position, phase.ToString());
                AssertSameBits(new Vector2(2.3f, 1f), m[1].Position, phase.ToString());
            }
        }

        // TAC-F3-22 (1): two overlapping non-bosses move apart along the center line by half the depth each.
        [Test]
        public void TwoOverlappingNonBosses_MoveApartHalfEach_ToTheRadiusSum_MidpointKept()
        {
            _rig = new MonEmRig();
            Vector2 a0 = new Vector2(2f, 1f);
            Vector2 b0 = new Vector2(2.3f, 1.4f);
            List<Monster> m = SpawnAll(_rig, ("M1", a0), ("M2", b0));
            float rSum = m[0].Body.Radius + m[1].Body.Radius;
            Assert.AreEqual(0.95f, rSum, Tolerance, "M1 0.45 + M2 0.5");
            Assert.Less((b0 - a0).magnitude, rSum, "the pair starts overlapping");

            Separate(_rig);

            Vector2 a1 = m[0].Position;
            Vector2 b1 = m[1].Position;
            Assert.AreEqual(rSum, (b1 - a1).magnitude, Tolerance, "distance becomes the radius sum");
            Vector2 mid0 = (a0 + b0) * 0.5f;
            Vector2 mid1 = (a1 + b1) * 0.5f;
            Assert.AreEqual(mid0.x, mid1.x, Tolerance, "midpoint x kept");
            Assert.AreEqual(mid0.y, mid1.y, Tolerance, "midpoint y kept");
            Vector2 dir = (b0 - a0).normalized;
            float half = (rSum - (b0 - a0).magnitude) / 2f;
            AssertNear(a0 - dir * half, a1, "the smaller Id moves away from the larger by half the depth");
            AssertNear(b0 + dir * half, b1, "the larger Id moves away from the smaller by half the depth");
        }

        // TAC-F3-22 (2): exactly touching or farther apart: no move (strictly-less test).
        [Test]
        public void TouchingOrFartherPairs_DoNotMove()
        {
            _rig = new MonEmRig();
            float rSum = 0.45f + 0.5f;
            Vector2 touchA = new Vector2(0f, -5f);
            Vector2 touchB = new Vector2(rSum, -5f);
            Vector2 farA = new Vector2(5f, 3f);
            Vector2 farB = new Vector2(5.6f, 3.8f);
            List<Monster> m = SpawnAll(_rig, ("M1", touchA), ("M2", touchB), ("M1", farA), ("M2", farB));
            Assert.AreEqual(rSum, m[0].Body.Radius + m[1].Body.Radius);
            Vector2 d = m[1].Position - m[0].Position;
            Assert.IsFalse(d.x * d.x + d.y * d.y < rSum * rSum, "the touching pair is not strictly inside the radius sum");
            Assert.IsTrue(d.x * d.x + d.y * d.y <= rSum * rSum, "the touching pair is exactly at the radius sum");

            Separate(_rig);

            AssertSameBits(touchA, m[0].Position, "touching A");
            AssertSameBits(touchB, m[1].Position, "touching B");
            AssertSameBits(farA, m[2].Position, "far A");
            AssertSameBits(farB, m[3].Position, "far B");
        }

        // TAC-F3-22 (3): coincident centers: larger Id +x, smaller Id -x, (rA + rB) / 2 each.
        [Test]
        public void CoincidentNonBosses_LargerIdPlusX_SmallerIdMinusX_HalfTheSumEach()
        {
            _rig = new MonEmRig();
            Vector2 p = new Vector2(1f, -2f);
            List<Monster> m = SpawnAll(_rig, ("M1", p), ("M2", p));
            float half = (0.45f + 0.5f) / 2f;

            Separate(_rig);

            AssertNear(new Vector2(p.x - half, p.y), m[0].Position, "smaller Id to -x");
            AssertNear(new Vector2(p.x + half, p.y), m[1].Position, "larger Id to +x");
            Assert.AreEqual(p.y, m[0].Position.y, "no y move");
            Assert.AreEqual(p.y, m[1].Position.y, "no y move");
        }

        // TAC-F3-22 (4): pairs come from the pass-start positions only; pushes accumulate in (smaller, larger) order and apply once.
        [Test]
        public void ThreeInALine_PairsFromPassStart_AccumulatedThenAppliedOnce()
        {
            _rig = new MonEmRig();
            // Row 1 (y = 0, M1 r 0.45, sum 0.9): A-B and B-C overlap by 0.3, A-C do not. Accumulated: A -0.15, B +0.15 - 0.15 = 0,
            // C +0.15. Applying pushes one pair at a time would instead move B to 0.75 and then re-test B-C from there.
            // Row 2 (y = 5): only A-B overlap (0.1); B's push (+0.05) makes a new B-C overlap that this pass leaves alone.
            List<Monster> m = SpawnAll(_rig,
                ("M1", new Vector2(0f, 0f)), ("M1", new Vector2(0.6f, 0f)), ("M1", new Vector2(1.2f, 0f)),
                ("M1", new Vector2(0f, 5f)), ("M1", new Vector2(0.8f, 5f)), ("M1", new Vector2(1.72f, 5f)));

            Separate(_rig);

            AssertNear(new Vector2(-0.15f, 0f), m[0].Position, "row 1 A");
            AssertNear(new Vector2(0.6f, 0f), m[1].Position, "row 1 B: equal and opposite pushes cancel");
            AssertNear(new Vector2(1.35f, 0f), m[2].Position, "row 1 C");
            AssertNear(new Vector2(-0.05f, 5f), m[3].Position, "row 2 A");
            AssertNear(new Vector2(0.85f, 5f), m[4].Position, "row 2 B");
            AssertSameBits(new Vector2(1.72f, 5f), m[5].Position, "row 2 C: had no overlap at the pass start");
            Assert.Less((m[5].Position - m[4].Position).magnitude, 0.9f - 0.01f, "the new B-C overlap is left for the next step");

            Separate(_rig);
            Assert.AreEqual(0.9f, (m[5].Position - m[4].Position).magnitude, Tolerance, "the next pass resolves it");
        }

        // TAC-F3-23: the boss stays bit-identical; the non-boss takes the whole depth; coincident -> non-boss +x; two bosses stay.
        [Test]
        public void BossPairs_BossNeverMoves_NonBossTakesTheWholeDepth()
        {
            _rig = new MonEmRig();
            Vector2 bossFirst = new Vector2(-20f, 0f);
            Vector2 m1AfterBoss = new Vector2(-19f, 1f);
            Vector2 m1BeforeBoss = new Vector2(-1f, -1f);
            Vector2 bossSecond = new Vector2(0f, 0f);
            Vector2 coincident = new Vector2(20f, 0f);
            Vector2 twinA = new Vector2(0f, 20f);
            Vector2 twinB = new Vector2(1f, 20f);
            // Ids ascend in this order: boss < M1 (group 1), M1 < boss (group 2), M1 < boss on the same center (group 3), boss < boss.
            List<Monster> m = SpawnAll(_rig,
                ("M4", bossFirst), ("M1", m1AfterBoss),
                ("M1", m1BeforeBoss), ("M4", bossSecond),
                ("M1", coincident), ("M4", coincident),
                ("M4", twinA), ("M4", twinB));
            Assert.IsTrue(m[0].IsBoss && m[3].IsBoss && m[5].IsBoss && m[6].IsBoss && m[7].IsBoss);
            Assert.IsFalse(m[1].IsBoss || m[2].IsBoss || m[4].IsBoss);
            float rSum = m[0].Body.Radius + m[1].Body.Radius;
            Assert.AreEqual(1.95f, rSum, Tolerance, "boss 1.5 + M1 0.45");

            Separate(_rig);

            AssertSameBits(bossFirst, m[0].Position, "boss (smaller Id) stays");
            Assert.AreEqual(rSum, (m[1].Position - bossFirst).magnitude, Tolerance, "M1 pushed to the radius sum");
            AssertNear(bossFirst + (m1AfterBoss - bossFirst).normalized * rSum, m[1].Position, "M1 moves straight away from the boss");

            AssertSameBits(bossSecond, m[3].Position, "boss (larger Id) stays");
            Assert.AreEqual(rSum, (m[2].Position - bossSecond).magnitude, Tolerance, "M1 pushed to the radius sum");
            AssertNear(bossSecond + (m1BeforeBoss - bossSecond).normalized * rSum, m[2].Position, "M1 moves straight away from the boss");

            AssertSameBits(coincident, m[5].Position, "coincident boss stays");
            AssertNear(new Vector2(coincident.x + rSum, coincident.y), m[4].Position, "coincident non-boss (smaller Id) goes +x by the whole depth");
            Assert.AreEqual(coincident.y, m[4].Position.y);

            AssertSameBits(twinA, m[6].Position, "two bosses: neither moves");
            AssertSameBits(twinB, m[7].Position, "two bosses: neither moves");
        }

        // TAC-F3-24 (EM): disjoint pairs give bit-identical results whichever spawn order assigned the Ids; other categories never move.
        [Test]
        public void DisjointPairs_ForwardAndReverseSpawnOrder_BitIdentical_OtherCategoriesUnchanged()
        {
            var layout = new (string id, Vector2 position)[]
            {
                ("M1", new Vector2(-6f, -6f)), ("M2", new Vector2(-5.71f, -5.37f)),
                ("M3", new Vector2(6.13f, -4.4f)), ("M1", new Vector2(5.5f, -4.05f)),
                ("M2", new Vector2(-3.3f, 7.7f)), ("M2", new Vector2(-3.31f, 8.43f)),
                ("M1", new Vector2(4.2f, 4.2f)), ("M3", new Vector2(4.57f, 4.09f)),
                ("M1", new Vector2(10f, 0f)), ("M1", new Vector2(11f, 0f)),
            };
            _rig = new MonEmRig();
            _rigB = new MonEmRig();
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None, position: new Vector2(1f, 2f), count: 1));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnChest, EntityHandle.None, position: new Vector2(-6f, -5.8f)));
            _rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnEnemyProjectile, EntityHandle.None, position: new Vector2(-5.8f, -5.6f),
                definitionId: "M3"));
            List<Monster> forward = SpawnAll(_rig, layout);
            var reversedLayout = new (string id, Vector2 position)[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                reversedLayout[i] = layout[layout.Length - 1 - i];
            }

            List<Monster> reverse = SpawnAll(_rigB, reversedLayout);
            Assert.AreEqual(1, _rig.Live<ExperienceGem>().Count);
            Assert.AreEqual(1, _rig.Live<Chest>().Count);
            Assert.AreEqual(1, _rig.Live<EnemyProjectile>().Count);
            var others = new List<(SimEntity entity, Vector2 position)>();
            foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
            {
                if (entity.Category != EntityCategory.Monster)
                {
                    others.Add((entity, entity.Position));
                }
            }

            Separate(_rig);
            Separate(_rigB);

            int moved = 0;
            for (int i = 0; i < layout.Length; i++)
            {
                Monster f = forward[i];
                Monster r = reverse[layout.Length - 1 - i];
                AssertSameBits(f.Position, r.Position, "start " + layout[i].position.ToString("F3"));
                if (!SameBits(f.Position, layout[i].position))
                {
                    moved++;
                }
            }

            Assert.AreEqual(8, moved, "the four overlapping pairs moved, the fifth pair (1.0 apart, sum 0.9) did not");
            Assert.AreEqual(3, others.Count);
            foreach ((SimEntity entity, Vector2 position) in others)
            {
                AssertSameBits(position, entity.Position, entity.Category.ToString());
            }
        }

        // TAC-F3-24 (EM, assembled): with the pass at BuildIndex -10 in a full run, the player and every non-monster entity keep their
        // position across the pass, the crowd is pushed apart, and the same seed gives the same state hash every step.
        [Test]
        public void InAFullRun_PassMovesOnlyMonsters_SameSeedTwice_SameHashEveryStep()
        {
            const int Steps = 300;
            var runs = new FlowEmRig[2];
            var probes = new SeparationProbe[2];
            var hashes = new List<ulong>[2];
            for (int r = 0; r < 2; r++)
            {
                // Tough M1s so the starting weapon does not clear the crowd.
                (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 100000,"));
                var rig = new FlowEmRig(data, report, start: false);
                if (r == 0)
                {
                    _flow = rig;
                }
                else
                {
                    _flowB = rig;
                }

                runs[r] = rig;
                probes[r] = new SeparationProbe(rig);
                rig.Pipeline.AddHandler(SimulationPhase.BuildIndex, -20, probes[r].Before);
                rig.Pipeline.AddHandler(SimulationPhase.BuildIndex, -10, new MonsterSeparation(rig.Registry));
                rig.Pipeline.AddHandler(SimulationPhase.BuildIndex, -5, probes[r].After);
                rig.PolicyFactory = () => new MonEmNoSpawnPolicy();
                rig.Input.Still = true;
                rig.StartRun();
                Vector2 player = rig.Player.Position;
                rig.Enqueue(PendingCommand.SpawnMonstersNow("M1", 6, player + new Vector2(4f, 0f), 0f, true));
                rig.Enqueue(PendingCommand.SpawnMonstersNow("M1", 6, player + new Vector2(-3f, 2f), 0f, true));
                rig.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None, position: player + new Vector2(0f, -6f),
                    count: 1));
                hashes[r] = new List<ulong>();
                for (int i = 0; i < Steps; i++)
                {
                    rig.Step();
                    hashes[r].Add(rig.Hash);
                }
            }

            Assert.AreEqual(hashes[0], hashes[1], "same seed and input: same hash every step");
            SeparationProbe probe = probes[0];
            Assert.AreEqual(Steps, probe.Passes, "one pass per step");
            Assert.Greater(probe.MonsterMoves, 0, "the crowd was pushed apart");
            Assert.Greater(probe.OtherChecks, 0, "non-monster entities were present during passes");
            Assert.Greater(probe.ProjectileChecks, 0, "player projectiles were present during passes");
            Assert.AreEqual(12, runs[0].Live<Monster>().Count, "nobody died or despawned");
        }

        // Spawns every (id, position) in one step through the dev command path (radius 0, cap ignored); returns them in Id order.
        private static List<Monster> SpawnAll(MonEmRig rig, params (string id, Vector2 position)[] spawns)
        {
            int before = rig.SpawnedLog.Count;
            foreach ((string id, Vector2 position) in spawns)
            {
                rig.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: position, definitionId: id,
                    count: 1, ignoreCap: true));
            }

            rig.Step();
            var monsters = new List<Monster>();
            for (int i = before; i < rig.SpawnedLog.Count; i++)
            {
                if (rig.SpawnedLog[i] is Monster monster)
                {
                    monsters.Add(monster);
                }
            }

            Assert.AreEqual(spawns.Length, monsters.Count, "every monster spawned");
            for (int i = 0; i < spawns.Length; i++)
            {
                AssertSameBits(spawns[i].position, monsters[i].Position, "spawned where asked");
                if (i > 0)
                {
                    Assert.Less(monsters[i - 1].Id.CompareTo(monsters[i].Id), 0, "Ids ascend in spawn order");
                }
            }

            return monsters;
        }

        private static void Separate(MonEmRig rig)
        {
            new MonsterSeparation(rig.Registry).OnPhase(SimulationPhase.BuildIndex, rig.Context);
        }

        private static bool SameBits(Vector2 a, Vector2 b)
        {
            return BitConverter.SingleToInt32Bits(a.x) == BitConverter.SingleToInt32Bits(b.x)
                && BitConverter.SingleToInt32Bits(a.y) == BitConverter.SingleToInt32Bits(b.y);
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, string label)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, label + " (x)");
            Assert.AreEqual(expected.y, actual.y, Tolerance, label + " (y)");
        }

        private static void AssertSameBits(Vector2 expected, Vector2 actual, string label)
        {
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.x), BitConverter.SingleToInt32Bits(actual.x), label + " (x bits)");
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.y), BitConverter.SingleToInt32Bits(actual.y), label + " (y bits)");
        }

        // Records every entity's position and the player's just before the pass (order -20) and checks them just after it (-5).
        private sealed class SeparationProbe
        {
            internal readonly Hook Before;
            internal readonly Hook After;
            internal int Passes;
            internal int MonsterMoves;
            internal int OtherChecks;
            internal int ProjectileChecks;

            private readonly FlowEmRig _rig;
            private readonly List<(SimEntity entity, Vector2 position)> _before = new List<(SimEntity, Vector2)>();
            private Vector2 _playerBefore;

            internal SeparationProbe(FlowEmRig rig)
            {
                _rig = rig;
                Before = new Hook(Record);
                After = new Hook(Check);
            }

            private void Record()
            {
                _before.Clear();
                foreach (SimEntity entity in _rig.Lifecycle.ActiveEntities)
                {
                    _before.Add((entity, entity.Position));
                }

                _playerBefore = _rig.Player.Position;
            }

            private void Check()
            {
                Passes++;
                AssertSameBits(_playerBefore, _rig.Player.Position, "player");
                foreach ((SimEntity entity, Vector2 position) in _before)
                {
                    if (entity.Category == EntityCategory.Monster)
                    {
                        if (!SameBits(position, entity.Position))
                        {
                            MonsterMoves++;
                        }

                        continue;
                    }

                    OtherChecks++;
                    if (entity.Category == EntityCategory.PlayerProjectile)
                    {
                        ProjectileChecks++;
                    }

                    AssertSameBits(position, entity.Position, entity.Category.ToString());
                }
            }
        }

        private sealed class Hook : IStepPhaseHandler
        {
            private readonly Action _action;

            internal Hook(Action action)
            {
                _action = action;
            }

            public void OnPhase(SimulationPhase phase, in StepContext context)
            {
                _action();
            }
        }
    }
}
