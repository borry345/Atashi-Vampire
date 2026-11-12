using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // F2 (TAC-F2-1..6) and F6 (TAC-F6-1..9, 11), commands and spawn path (5.4, P7).
    public sealed class WeaponTests
    {
        private PlrRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
        }

        internal static void QueueLoadout(PlrRig rig, params (string id, int level)[] entries)
        {
            var list = new List<WeaponLoadoutEntry>();
            foreach ((string id, int level) in entries)
            {
                list.Add(new WeaponLoadoutEntry(id, level));
            }

            rig.BeginHook.Pending.Add(ctx => rig.Applier.HandleSetLoadout(PendingCommand.SetLoadout(list), ctx));
        }

        [Test]
        public void TACF2_1_StartsWithW1Level1AndFiresAtStepZero()
        {
            _rig = new PlrRig();
            Assert.IsTrue(_rig.Weapons.TryGetSlot(0, out WeaponInstance w1));
            Assert.AreEqual("W1", w1.Id);
            Assert.AreEqual(1, w1.Level);
            Assert.AreEqual(0, w1.NextFireStep);
            _rig.AddMonster(new Vector2(5f, 0f));
            _rig.Step();
            List<SimulationCommand> shots = _rig.Commands.At(0, SimulationCommandKind.SpawnPlayerProjectile);
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(_rig.PlayerId, shots[0].Source, "weapon commands use the player id as Source");
            Assert.AreEqual("W1", shots[0].DefinitionId);
            Assert.AreEqual(0, shots[0].LevelIndex);
            Assert.AreEqual(0, shots[0].SlotIndex);
            Assert.AreEqual(Vector2.right, shots[0].Direction);
            Assert.AreEqual(60, w1.NextFireStep, "cooldownSeconds 1.0 = 60 steps");
            List<PlayerProjectile> live = _rig.Live<PlayerProjectile>();
            Assert.AreEqual(1, live.Count, "spawned in the same step's Lifecycle");
            Assert.AreEqual(0 + 120, live[0].ExpireStep, "lifetime 2.0 s");
            Assert.AreEqual(10f, live[0].Damage);
        }

        [Test]
        public void TACF2_2_NoTargetMeansNoFireAndTheCooldownIsKept()
        {
            _rig = new PlrRig();
            _rig.Steps(5);
            Assert.AreEqual(0, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerProjectile));
            Assert.AreEqual(0, _rig.Weapons.Slots[0].NextFireStep);
            _rig.AddMonster(new Vector2(0f, 5f));
            _rig.Step();
            Assert.AreEqual(1, _rig.Commands.At(5, SimulationCommandKind.SpawnPlayerProjectile).Count);
            Assert.AreEqual(65, _rig.Weapons.Slots[0].NextFireStep);
            _rig.Steps(59);
            Assert.AreEqual(1, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerProjectile));
            _rig.Step();
            Assert.AreEqual(1, _rig.Commands.At(65, SimulationCommandKind.SpawnPlayerProjectile).Count);
        }

        [Test]
        public void TACF2_2_FirstOwnership_LevelUpWeaponNewFiresOnTheNextStep()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            _rig.Steps(3);
            _rig.StepAndCloseForLevelUp();
            Assert.AreEqual(4, _rig.Clock.Step);
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponNew, "W4"));
            Assert.AreEqual(4, _rig.Weapons.Slots[1].NextFireStep, "NextFireStep = clock.Step while the gate is closed");
            _rig.Clock.Open();
            _rig.Step();
            List<SimulationCommand> shots = _rig.Commands.At(4, SimulationCommandKind.SpawnPlayerProjectile);
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual("W4", shots[0].DefinitionId);
            Assert.AreEqual(1, shots[0].SlotIndex);
        }

        [Test]
        public void TACF2_2_FirstOwnership_SetLoadoutFiresInTheSameStep()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            _rig.Steps(2);
            QueueLoadout(_rig, ("W1", 1), ("W4", 2));
            _rig.Step();
            Assert.AreEqual(2, _rig.Weapons.Slots[1].NextFireStep - StatMath.ApplyAttackSpeed(_rig.Weapons.Slots[1].LevelDefinition.CooldownSteps, 0f));
            List<SimulationCommand> shots = _rig.Commands.At(2, SimulationCommandKind.SpawnPlayerProjectile);
            Assert.AreEqual(1, shots.Count, "W1 kept its cooldown, W4 fires at once");
            Assert.AreEqual("W4", shots[0].DefinitionId);
            Assert.AreEqual(1, shots[0].LevelIndex);
        }

        [Test]
        public void TACF2_3_P1ShortensTheNextCooldownOnlyAndNotTheScheduledOne()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            _rig.StepAndCloseForLevelUp();
            Assert.AreEqual(60, _rig.Weapons.Slots[0].NextFireStep);
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            Assert.AreEqual(60, _rig.Weapons.Slots[0].NextFireStep, "P1 applies from the next computation");
            _rig.Clock.Open();
            _rig.Steps(60);
            Assert.AreEqual(1, _rig.Commands.At(60, SimulationCommandKind.SpawnPlayerProjectile).Count);
            Assert.AreEqual(60 + StatMath.ApplyAttackSpeed(60, 0.10f), _rig.Weapons.Slots[0].NextFireStep);
            Assert.AreEqual(115, _rig.Weapons.Slots[0].NextFireStep);
        }

        [Test]
        public void TACF2_3_P1ScalesTheOrbTickButNeverTheZoneTick()
        {
            _rig = new PlrRig();
            QueueLoadout(_rig, ("W2", 1), ("W3", 1));
            _rig.StepAndCloseForLevelUp();
            ContactDamageTracker orbs = _rig.Weapons.Slots[0].Tracker;
            ContactDamageTracker zones = _rig.Weapons.Slots[1].Tracker;
            Assert.AreEqual(30, orbs.IntervalSteps);
            Assert.AreEqual(30, zones.IntervalSteps);
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            for (int i = 0; i < 4; i++)
            {
                _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P1"));
            }

            Assert.AreEqual(StatMath.ApplyAttackSpeed(30, 0.5f), orbs.IntervalSteps);
            Assert.AreEqual(20, orbs.IntervalSteps);
            Assert.AreEqual(30, zones.IntervalSteps, "W3 zone tick ignores P1");
        }

        [Test]
        public void TACF2_5_WeaponsDisabledStopsFireAndOrbSpawnsButOrbsKeepMoving()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            QueueLoadout(_rig, ("W1", 1), ("W2", 1));
            _rig.Step();
            Assert.AreEqual(2, _rig.Live<OrbitOrb>().Count);
            _rig.Rules.WeaponsEnabled = false;
            QueueLoadout(_rig, ("W1", 1), ("W2", 1), ("W4", 1));
            Vector2 before = _rig.Live<OrbitOrb>()[0].Position;
            _rig.Steps(100);
            Assert.AreEqual(1, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerProjectile), "only the step-0 shot");
            Assert.AreEqual(2, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerOrb));
            Assert.AreNotEqual(before, _rig.Live<OrbitOrb>()[0].Position, "orb positions are still written");
            _rig.Rules.WeaponsEnabled = true;
            _rig.Step();
            Assert.AreEqual(2, _rig.Commands.At(101, SimulationCommandKind.SpawnPlayerProjectile).Count, "W1 and W4 fire again");
        }

        [Test]
        public void TACF2_5_WeaponsDisabledStopsPersistentDamageButKeepsEnterRecords()
        {
            _rig = new PlrRig();
            PlrMonster monster = _rig.AddMonster(new Vector2(2f, 0f));
            QueueLoadout(_rig, ("W2", 1));
            _rig.Step();
            OrbitOrb orb = _rig.Live<OrbitOrb>()[0];
            _rig.Rules.WeaponsEnabled = false;
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, orb.Id, monster.Id);
            _rig.Steps(5);
            Assert.AreEqual(0, monster.Hits.Count);
            Assert.AreEqual(1, _rig.Weapons.Slots[0].Tracker.TrackedCount, "Enter was still recorded");
            _rig.Rules.WeaponsEnabled = true;
            _rig.Step();
            Assert.AreEqual(1, monster.Hits.Count);
            Assert.AreEqual((8f, 6L), monster.Hits[0]);
        }

        [Test]
        public void TACF6_1_FactoryPicksTheBehaviorFromTheDefinition()
        {
            GameData data = PlrTestData.Real();
            var expected = new Dictionary<string, System.Type>
            {
                { "W1", typeof(ProjectileWeapon) }, { "E1", typeof(ProjectileWeapon) }, { "W4", typeof(ProjectileWeapon) },
                { "W2", typeof(OrbitWeapon) }, { "E2", typeof(OrbitWeapon) }, { "W3", typeof(ZoneWeapon) },
            };
            foreach (KeyValuePair<string, System.Type> pair in expected)
            {
                Assert.IsTrue(data.TryGetWeapon(pair.Key, out WeaponDefinition definition));
                WeaponInstance instance = WeaponFactory.Create(definition, 1, 0, 0, 0f);
                Assert.AreEqual(pair.Value, instance.Action.GetType(), pair.Key);
                Assert.AreEqual(pair.Value != typeof(ProjectileWeapon), instance.Tracker != null, pair.Key);
            }
        }

        [Test]
        public void TACF6_2_W1AimsAtTheNearestMonsterAndZeroVectorAimsRight()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(3f, 4f));
            _rig.AddMonster(new Vector2(30f, 40f));
            _rig.Step();
            Vector2 direction = _rig.Commands.At(0, SimulationCommandKind.SpawnPlayerProjectile)[0].Direction;
            Assert.AreEqual(0.6f, direction.x, 1e-6f);
            Assert.AreEqual(0.8f, direction.y, 1e-6f);

            _rig.Destroy();
            _rig = new PlrRig();
            _rig.AddMonster(Vector2.zero);
            _rig.Step();
            Assert.AreEqual(Vector2.right, _rig.Commands.At(0, SimulationCommandKind.SpawnPlayerProjectile)[0].Direction);
        }

        [Test]
        public void TACF6_2_ProjectileCountSplitsTheSpreadEvenly()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            QueueLoadout(_rig, ("W1", 4));
            _rig.Step();
            List<SimulationCommand> shots = _rig.Commands.At(0, SimulationCommandKind.SpawnPlayerProjectile);
            Assert.AreEqual(2, shots.Count);
            Assert.AreEqual(-5f, Vector2.SignedAngle(Vector2.right, shots[0].Direction), 1e-3f);
            Assert.AreEqual(5f, Vector2.SignedAngle(Vector2.right, shots[1].Direction), 1e-3f);
            Assert.AreEqual(1f, shots[0].Direction.magnitude, 1e-6f);
        }

        [Test]
        public void TACF6_3_W4RetargetsEveryTickAndKeepsTheLastDirectionWithoutTargets()
        {
            _rig = new PlrRig();
            PlrMonster monster = _rig.AddMonster(new Vector2(10f, 0f));
            QueueLoadout(_rig, ("W4", 1));
            _rig.Step();
            PlayerProjectile projectile = _rig.Live<PlayerProjectile>()[0];
            Assert.IsTrue(projectile.IsHoming);
            Assert.AreEqual(Vector2.zero, projectile.Position, "no tick in the spawn step");
            float speed = projectile.SpeedPerStep;
            _rig.Step();
            Assert.AreEqual(new Vector2(speed, 0f), projectile.Position);
            monster.Position = new Vector2(speed, 10f);
            _rig.Step();
            Assert.AreEqual(Vector2.up, projectile.Direction);
            monster.Hp = 0f;
            _rig.Step();
            Assert.AreEqual(Vector2.up, projectile.Direction, "no target: keeps the last direction");
            Assert.AreEqual(speed * 2f, projectile.Position.y, 1e-5f);
        }

        [Test]
        public void TACF19_18_HomingReAimRecordsTheTargetAndAPooledProjectileStartsWithNone()
        {
            _rig = new PlrRig();
            PlrMonster far = _rig.AddMonster(new Vector2(10f, 0f));
            QueueLoadout(_rig, ("W4", 1));
            _rig.Step();
            PlayerProjectile projectile = _rig.Live<PlayerProjectile>()[0];
            Assert.AreEqual(default(EntityHandle), projectile.LastTargetId, "no tick in the spawn step");
            _rig.Step();
            Assert.AreEqual(far.Id, projectile.LastTargetId);
            PlrMonster near = _rig.AddMonster(projectile.Position + new Vector2(0f, 1f));
            _rig.Step();
            Assert.AreEqual(near.Id, projectile.LastTargetId, "re-aimed at the new nearest monster");
            near.Hp = 0f;
            far.Hp = 0f;
            _rig.Step();
            Assert.AreEqual(near.Id, projectile.LastTargetId, "a failed re-aim writes nothing (no branch is added)");

            // Consume it, then let W4 fire again: the pooled instance must not inherit the previous flight's target.
            far.Hp = 1000f;
            long hitStep = _rig.Clock.Step;
            _rig.Collisions.Add(hitStep, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, projectile.Id, far.Id);
            _rig.Step();
            Assert.AreEqual(0, _rig.Live<PlayerProjectile>().Count);
            WeaponInstance w4 = _rig.Weapons.Slots[0];
            while (_rig.Clock.Step < w4.NextFireStep)
            {
                _rig.Step();
            }

            _rig.Step();
            List<PlayerProjectile> live = _rig.Live<PlayerProjectile>();
            Assert.AreEqual(1, live.Count);
            Assert.AreSame(projectile, live[0], "the pool handed back the same instance");
            Assert.AreEqual(default(EntityHandle), live[0].LastTargetId, "cleared in Initialize before the first Tick");
            _rig.Step();
            Assert.AreEqual(far.Id, live[0].LastTargetId);
        }

        [Test]
        public void TACF19_18_StraightProjectilesNeverRecordATarget()
        {
            foreach (string weaponId in new[] { "W1", "E1" })
            {
                _rig?.Destroy();
                _rig = new PlrRig();
                _rig.AddMonster(new Vector2(6f, 0f));
                _rig.AddMonster(new Vector2(0f, 8f));
                QueueLoadout(_rig, (weaponId, 1));
                int seen = 0;
                for (int i = 0; i < 30; i++)
                {
                    _rig.Step();
                    foreach (PlayerProjectile projectile in _rig.Live<PlayerProjectile>())
                    {
                        Assert.AreEqual(weaponId, projectile.WeaponId);
                        Assert.IsFalse(projectile.IsHoming);
                        Assert.AreEqual(default(EntityHandle), projectile.LastTargetId, weaponId);
                        seen++;
                    }
                }

                Assert.GreaterOrEqual(seen, 30, weaponId + " projectiles were alive and ticking");
            }
        }

        [Test]
        public void TACF6_4_OrbsHaveNoLifetimeAndFollowTheStepFormula()
        {
            _rig = new PlrRig();
            QueueLoadout(_rig, ("W2", 1));
            _rig.Step();
            List<OrbitOrb> orbs = _rig.Live<OrbitOrb>();
            Assert.AreEqual(2, orbs.Count);
            Assert.AreEqual(long.MaxValue, orbs[0].ExpireStep);
            _rig.Input.Next = new MoveCommand(Vector2.right);
            _rig.Steps(300);
            List<OrbitOrb> later = _rig.Live<OrbitOrb>();
            CollectionAssert.AreEqual(new[] { orbs[0].Id, orbs[1].Id }, new[] { later[0].Id, later[1].Id }, "same orbs, never recycled");
            WeaponLevelDefinition level = _rig.Weapons.Slots[0].LevelDefinition;
            long step = 300;
            for (int i = 0; i < 2; i++)
            {
                double theta = level.AngularSpeedRadPerStep * (double)step + 2d * System.Math.PI * later[i].OrbIndex / 2;
                Vector2 expected = _rig.Player.Position + level.OrbitRadius * new Vector2((float)System.Math.Cos(theta), (float)System.Math.Sin(theta));
                Assert.AreEqual(expected.x, later[i].Position.x, 1e-4f);
                Assert.AreEqual(expected.y, later[i].Position.y, 1e-4f);
            }
        }

        [Test]
        public void TACF6_5_NearestEnemyZoneWaitsForATarget()
        {
            _rig = new PlrRig();
            QueueLoadout(_rig, ("W3", 1));
            _rig.Steps(10);
            Assert.AreEqual(0, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerZone));
            Assert.AreEqual(0, _rig.Weapons.Slots[0].NextFireStep, "no target: cooldown not spent");
            _rig.AddMonster(new Vector2(4f, 1f));
            _rig.Step();
            List<SimulationCommand> zones = _rig.Commands.At(10, SimulationCommandKind.SpawnPlayerZone);
            Assert.AreEqual(1, zones.Count);
            Assert.AreEqual(new Vector2(4f, 1f), zones[0].Position, "laid at the nearest monster");
            Assert.AreEqual(10 + 180, _rig.Weapons.Slots[0].NextFireStep);
        }

        [Test]
        public void TACF6_5_PlayerPlacementZoneNeedsNoTargetAndTAC0_8_JoinsCollisionNextStep()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.Weapons, PlrTestData.WeaponsWithExtras));
            QueueLoadout(_rig, ("Z1", 1));
            _rig.Steps(121);
            Assert.AreEqual(0, _rig.Targets.QueryCount, "placement player never queries targets");
            Assert.AreEqual(3, _rig.Commands.Count(SimulationCommandKind.SpawnPlayerZone), "steps 0, 60, 120");
            Assert.AreEqual(1, _rig.Commands.At(60, SimulationCommandKind.SpawnPlayerZone).Count);
            DamageZone zone = _rig.Live<DamageZone>()[0];
            Assert.AreEqual(120, zone.SpawnStep);
            Assert.AreEqual(120 + 30, zone.ExpireStep, "durationSeconds 0.5");
            CollectionAssert.DoesNotContain(_rig.Collisions.RegisteredAtCollect[120], zone.Id, "not in its spawn step's Collision");
            _rig.Step();
            CollectionAssert.Contains(_rig.Collisions.RegisteredAtCollect[121], zone.Id, "joins from the next step");
            _rig.Steps(29);
            Assert.AreEqual(0, _rig.Live<DamageZone>().Count, "LifetimeSystem removed it after durationSteps");
        }

        [Test]
        public void TACF6_6_OverlappingZonesOfOneWeaponShareOneCooldownRecord()
        {
            _rig = new PlrRig();
            PlrMonster monster = _rig.AddMonster(new Vector2(3f, 0f));
            QueueLoadout(_rig, ("W3", 4));
            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            for (int i = 0; i < 4; i++)
            {
                _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P1"));
            }

            _rig.Clock.Open();
            DamageZone first = _rig.Live<DamageZone>()[0];
            Assert.AreEqual(150, first.ExpireStep);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, first.Id, monster.Id);
            _rig.Steps(144);
            Assert.AreEqual(2, _rig.Live<DamageZone>().Count, "second zone laid at step 144 while the first lasts to 150");
            DamageZone second = _rig.Live<DamageZone>()[1];
            _rig.Collisions.Add(145, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, second.Id, monster.Id);
            _rig.Steps(8);
            var steps = new List<long>();
            foreach ((float amount, long step) in monster.Hits)
            {
                Assert.AreEqual(12f, amount);
                steps.Add(step);
            }

            // No extra hit at 145 (one record for the weapon); the first zone's despawn at 150 emits no Exit, the second still overlaps.
            CollectionAssert.AreEqual(new long[] { 1, 31, 61, 91, 121, 151 }, steps);
        }

        [Test]
        public void TACF6_6_ZoneDespawnEndsItsOverlapAlthoughNoExitIsEmitted()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.Weapons, PlrTestData.WeaponsWithExtras));
            PlrMonster monster = _rig.AddMonster(new Vector2(0.5f, 0f));
            QueueLoadout(_rig, ("Z1", 1));
            _rig.Step();
            DamageZone zone = _rig.Live<DamageZone>()[0];
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerZoneVsMonster, zone.Id, monster.Id);
            _rig.Steps(59);
            Assert.AreEqual(0, _rig.Live<DamageZone>().Count, "expired at step 30");
            var steps = new List<long>();
            foreach ((float amount, long step) in monster.Hits)
            {
                steps.Add(step);
            }

            CollectionAssert.AreEqual(new long[] { 1 }, steps, "no hit at 31: the zone is gone");
            Assert.AreEqual(0, _rig.Weapons.Slots[0].Tracker.TrackedCount);
        }

        [Test]
        public void TACF6_7_UpgradeUsesTheWholeLevelParameterSet()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(5f, 0f));
            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponUpgrade, "W1"));
            _rig.Clock.Open();
            _rig.Steps(60);
            PlayerProjectile shot = _rig.Live<PlayerProjectile>().Find(p => p.SpawnStep == 60);
            Assert.AreEqual(12f, shot.Damage);
            Assert.AreEqual(60 + 54, _rig.Weapons.Slots[0].NextFireStep, "level 2 cooldown 0.9 s");
        }

        [Test]
        public void TACF6_8_OrbCountChangesAreReconciledInWeaponTick()
        {
            _rig = new PlrRig();
            QueueLoadout(_rig, ("W2", 3));
            _rig.Step();
            Assert.AreEqual(3, _rig.Live<OrbitOrb>().Count);
            QueueLoadout(_rig, ("W2", 1));
            _rig.Step();
            List<SimulationCommand> despawns = _rig.Commands.At(1, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count);
            Assert.AreEqual(DespawnReason.Replaced, despawns[0].Reason);
            Assert.AreEqual(_rig.PlayerId, despawns[0].Source);
            List<OrbitOrb> left = _rig.Live<OrbitOrb>();
            Assert.AreEqual(2, left.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { left[0].OrbIndex, left[1].OrbIndex }, "the highest index went first");

            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponUpgrade, "W2"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.WeaponUpgrade, "W2"));
            Assert.AreEqual(2, _rig.Live<OrbitOrb>().Count, "no commands while the gate is closed");
            _rig.Clock.Open();
            _rig.Step();
            Assert.AreEqual(1, _rig.Commands.At(3, SimulationCommandKind.SpawnPlayerOrb).Count);
            Assert.AreEqual(3, _rig.Live<OrbitOrb>().Count);
            Assert.AreEqual(0.45f, _rig.Live<OrbitOrb>()[0].Body.Radius, "existing orbs grow with the level");
        }

        [Test]
        public void TACF6_9_ProjectileHitsOnceAndAnotherEnterInTheSameStepIsIgnored()
        {
            _rig = new PlrRig();
            PlrMonster near = _rig.AddMonster(new Vector2(5f, 0f));
            PlrMonster far = _rig.AddMonster(new Vector2(5f, 0.1f));
            _rig.Step();
            EntityHandle shot = _rig.Live<PlayerProjectile>()[0].Id;
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, far.Id);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, near.Id);
            _rig.Step();
            Assert.AreEqual(1, near.Hits.Count, "smaller monster id first");
            Assert.AreEqual(0, far.Hits.Count);
            List<SimulationCommand> despawns = _rig.Commands.At(1, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count);
            Assert.AreEqual(DespawnReason.Consumed, despawns[0].Reason);
            Assert.AreEqual(shot, despawns[0].Source);
            Assert.AreEqual(shot, despawns[0].Target);
            Assert.AreEqual(0, _rig.Live<PlayerProjectile>().Count);
        }

        [Test]
        public void TACF6_9_PiercingProjectileNeverHitsTheSameIdTwice()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.Weapons, PlrTestData.WeaponsWithExtras));
            PlrMonster first = _rig.AddMonster(new Vector2(5f, 0f));
            PlrMonster second = _rig.AddMonster(new Vector2(9f, 0f));
            QueueLoadout(_rig, ("PX", 1));
            _rig.Step();
            PlayerProjectile shot = _rig.Live<PlayerProjectile>()[0];
            Assert.AreEqual(1, shot.PierceCount);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot.Id, first.Id);
            _rig.Collisions.Add(2, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, shot.Id, first.Id);
            _rig.Collisions.Add(3, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot.Id, first.Id);
            _rig.Steps(3);
            Assert.AreEqual(1, first.Hits.Count);
            Assert.AreEqual(1, _rig.Live<PlayerProjectile>().Count, "pierce 1: still alive after one hit");
            _rig.Collisions.Add(4, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot.Id, second.Id);
            _rig.Step();
            Assert.AreEqual(1, second.Hits.Count);
            Assert.AreEqual(0, _rig.Live<PlayerProjectile>().Count, "consumed on hit pierceCount + 1");
        }

        private static PlayerProjectile FindProjectile(PlrRig rig, EntityHandle id)
        {
            foreach (PlayerProjectile projectile in rig.Live<PlayerProjectile>())
            {
                if (projectile.Id.Equals(id))
                {
                    return projectile;
                }
            }

            return null;
        }

        [Test]
        public void TACF6_11_SecondProjectileOnAMonsterKilledThisStepPassesThroughAndHitsTheNextLiveOne()
        {
            _rig = new PlrRig();
            PlrMonster m = _rig.AddMonster(new Vector2(5f, 0f), hp: 5f);
            PlrMonster n = _rig.AddMonster(new Vector2(5f, 0.1f));
            QueueLoadout(_rig, ("W1", 4));
            _rig.Step();
            List<PlayerProjectile> shots = _rig.Live<PlayerProjectile>();
            Assert.AreEqual(2, shots.Count, "W1 level 4 fires two projectiles");
            PlayerProjectile p1 = shots[0];
            PlayerProjectile p2 = shots[1];
            EntityHandle p1Id = p1.Id;
            EntityHandle p2Id = p2.Id;
            float damage = p2.Damage;
            Assert.Less(p1Id.CompareTo(p2Id), 0);
            Assert.Greater(p1.Damage, 5f, "one hit kills M");

            // Step 1: p1 kills M; p2 reaches the now dead M - recorded, no damage, not consumed.
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, p2Id, m.Id);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, p1Id, m.Id);
            _rig.Step();
            Assert.AreEqual(1, m.Hits.Count, "only p1 damages M");
            Assert.IsFalse(m.IsAlive);
            List<SimulationCommand> despawns = _rig.Commands.At(1, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count, "p2 queues nothing");
            Assert.AreEqual(p1Id, despawns[0].Target);
            Assert.AreEqual(DespawnReason.Consumed, despawns[0].Reason);
            Assert.AreSame(p2, FindProjectile(_rig, p2Id), "p2 is still flying");
            Assert.IsFalse(p2.IsConsumed);
            Assert.AreEqual(0, p2.HitCount);

            // Step 2: M was recorded in p2's list, so a later Enter on M is ignored even if M had health again (stand-in revived).
            m.Hp = 100f;
            _rig.Collisions.Add(2, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, p2Id, m.Id);
            _rig.Step();
            Assert.AreEqual(1, m.Hits.Count, "M is in p2's hit list");
            Assert.AreEqual(0, _rig.Commands.At(2, SimulationCommandKind.Despawn).Count);
            Assert.IsFalse(p2.IsConsumed);

            // Step 3: a live monster N takes the hit and p2 is consumed.
            _rig.Collisions.Add(3, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, p2Id, n.Id);
            _rig.Step();
            Assert.AreEqual(1, n.Hits.Count);
            Assert.AreEqual(damage, n.Hits[0].amount);
            despawns = _rig.Commands.At(3, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count);
            Assert.AreEqual(p2Id, despawns[0].Target);
            Assert.AreEqual(DespawnReason.Consumed, despawns[0].Reason);
            Assert.IsNull(FindProjectile(_rig, p2Id));
        }

        [Test]
        public void TACF6_11_DeadTargetDoesNotUseThePierceBudget()
        {
            _rig = new PlrRig(PlrTestData.With(DataFileKind.Weapons, PlrTestData.WeaponsWithExtras));
            PlrMonster dead = _rig.AddMonster(new Vector2(5f, 0f), hp: 0f);
            PlrMonster live = _rig.AddMonster(new Vector2(6f, 0f));
            PlrMonster next = _rig.AddMonster(new Vector2(9f, 0f));
            QueueLoadout(_rig, ("PX", 1));
            _rig.Step();
            PlayerProjectile shot = _rig.Live<PlayerProjectile>()[0];
            EntityHandle shotId = shot.Id;
            Assert.AreEqual(1, shot.PierceCount);

            // Same step: dead (smaller id) then live - the dead Enter is recorded only and leaves the pierce budget alone.
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shotId, live.Id);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shotId, dead.Id);
            _rig.Step();
            Assert.AreEqual(0, dead.Hits.Count);
            Assert.AreEqual(1, live.Hits.Count);
            Assert.AreEqual(1, shot.HitCount, "only the live target counts");
            Assert.IsFalse(shot.IsConsumed, "pierce 1: one live hit left");
            Assert.AreSame(shot, FindProjectile(_rig, shotId));

            _rig.Collisions.Add(2, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shotId, next.Id);
            _rig.Step();
            Assert.AreEqual(1, next.Hits.Count);
            Assert.AreEqual(1, _rig.Commands.At(2, SimulationCommandKind.Despawn).Count);
            Assert.IsNull(FindProjectile(_rig, shotId), "consumed on the second live hit");
        }

        [Test]
        public void TACF6_11_OneProjectileOnADeadSmallerIdAndALiveLargerIdHitsTheLiveOne()
        {
            _rig = new PlrRig();
            PlrMonster dead = _rig.AddMonster(new Vector2(5f, 0f), hp: 0f);
            PlrMonster live = _rig.AddMonster(new Vector2(5f, 0.1f));
            _rig.Step();
            EntityHandle shot = _rig.Live<PlayerProjectile>()[0].Id;
            Assert.Less(dead.Id.CompareTo(live.Id), 0);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, live.Id);
            _rig.Collisions.Add(1, CollisionEventKind.Enter, CollisionChannel.PlayerProjectileVsMonster, shot, dead.Id);
            _rig.Step();
            Assert.AreEqual(0, dead.Hits.Count);
            Assert.AreEqual(1, live.Hits.Count, "the dead smaller id is passed, the live one is hit");
            List<SimulationCommand> despawns = _rig.Commands.At(1, SimulationCommandKind.Despawn);
            Assert.AreEqual(1, despawns.Count);
            Assert.AreEqual(shot, despawns[0].Target);
            Assert.AreEqual(0, _rig.Live<PlayerProjectile>().Count);
        }

        [Test]
        public void P7_SpawnCommandForAReplacedWeaponIsDroppedAndCounted()
        {
            _rig = new PlrRig();
            _rig.ResolveHook.Pending.Add(ctx => ctx.Commands.Enqueue(new SimulationCommand(
                SimulationCommandKind.SpawnPlayerProjectile, _rig.PlayerId, position: Vector2.zero, direction: Vector2.right,
                definitionId: "W4", levelIndex: 0, slotIndex: 0)));
            _rig.Step();
            Assert.AreEqual(1, _rig.SpawnHandler.DroppedCommandCount);
            Assert.AreEqual(0, _rig.Live<PlayerProjectile>().Count);
        }

        [Test]
        public void TAC0_6_ProjectileTickQueuesNoCommands()
        {
            _rig = new PlrRig();
            _rig.AddMonster(new Vector2(30f, 0f));
            QueueLoadout(_rig, ("W1", 5), ("W4", 5));
            _rig.Steps(120);
            Assert.Greater(_rig.Live<PlayerProjectile>().Count, 0);
            foreach ((long step, SimulationPhase phase, SimulationCommand command) in _rig.Commands.Log)
            {
                Assert.AreNotEqual(SimulationPhase.ProjectileTick, phase, "IUpdatable.Tick writes only its own state");
            }
        }
    }
}
