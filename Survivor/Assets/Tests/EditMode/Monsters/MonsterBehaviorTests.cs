using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Monster entity contract and the four behaviors (TAC-F3-1, F3-7, F3-8, F3-9, F3-21, F16-6, F17-1, 0-6, 0-8; 5.12 boundary interfaces).
    public sealed class MonsterBehaviorTests
    {
        private const float Eps = 1e-4f;

        // TAC-F3-21: the shipped M4 has no hazard zone; this fixture puts the removed pattern back in front of the summon so the
        // zone code path stays tested (pattern 0 = zone, pattern 1 = summon).
        private const string ZonePattern =
            "{ \"type\": \"hazardZone\", \"cooldownSeconds\": 6, \"zoneRadius\": 3, \"durationSeconds\": 2, \"damage\": 15, \"assetKey\": \"shape.hazard.red\" },";

        private MonEmRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
        }

        [Test]
        public void Spawn_SetsDefinitionHealthBodyAndBoundaryInterfaces()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));

            Assert.AreEqual(MonsterBehavior.Chaser, m1.Definition.Behavior, "TAC-F3-1: behavior comes from the definition");
            Assert.AreEqual(10f, m1.Hp);
            Assert.IsTrue(m1.IsAlive);
            Assert.AreEqual(-1L, m1.DiedAtStep);
            Assert.AreEqual(0.45f, m1.Body.Radius);
            Assert.AreEqual(CollisionLayer.Monster, m1.Body.Layer);
            Assert.AreEqual(TickGroup.Monsters, m1.Group);
            Assert.AreSame(m1, _rig.LookUpDamageable(m1.Id), "Monster is the IDamageable the player looks up");
            Assert.AreEqual(5f, _rig.LookUpContactSource(m1.Id).ContactDamage, "contact damage = definition ContactDamage");
        }

        [Test]
        public void ApplyDamage_RecordsDeathStep_AndIgnoresDamageWhileDead()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));

            m1.ApplyDamage(4f, 10);
            Assert.AreEqual(6f, m1.Hp);
            Assert.AreEqual(-1L, m1.DiedAtStep);
            m1.ApplyDamage(0f, 10);
            Assert.AreEqual(6f, m1.Hp, "zero damage is allowed and changes nothing");
            m1.ApplyDamage(6f, 11);
            Assert.IsFalse(m1.IsAlive);
            Assert.AreEqual(11L, m1.DiedAtStep);
            m1.ApplyDamage(5f, 12);
            Assert.AreEqual(0f, m1.Hp, "damage while dead is ignored");
            Assert.AreEqual(11L, m1.DiedAtStep, "the death step is not overwritten");
        }

        [Test]
        public void ApplyDamage_InvalidAmount_Throws()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => m1.ApplyDamage(-1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => m1.ApplyDamage(float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => m1.ApplyDamage(float.PositiveInfinity, 1));
            Assert.AreEqual(10f, m1.Hp);
        }

        [Test]
        public void FirstTick_IsTheStepAfterSpawn_AndSnapshotJoinsThen()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));

            Assert.AreEqual(new Vector2(5f, 0f), m1.Position, "TAC-0-8: no Tick in the spawn step");
            Assert.AreEqual(0, _rig.Snapshot.Count, "spawned in Lifecycle after this step's BuildIndex");
            Assert.AreEqual(1, _rig.Ticks.RegisteredCount(TickGroup.Monsters), "TAC-F17-1: registered with the tick strategy");

            _rig.Step();
            Assert.Less(m1.Position.x, 5f, "first Tick at spawn step + 1");
            Assert.AreEqual(1, _rig.Snapshot.Count);
        }

        [Test]
        public void Chaser_MovesStraightTowardCurrentPlayerPosition_AndStopsWhenOverlapping()
        {
            _rig = new MonEmRig();
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            float speed = m1.Definition.MoveSpeedPerStep;

            _rig.Step();
            Assert.AreEqual(5f - speed, m1.Position.x, Eps);
            Assert.AreEqual(0f, m1.Position.y, Eps);

            _rig.Player = m1.Position + new Vector2(0f, 3f);
            Vector2 before = m1.Position;
            _rig.Step();
            Assert.AreEqual(before.x, m1.Position.x, Eps, "direction follows the player position of this step");
            Assert.AreEqual(before.y + speed, m1.Position.y, Eps);

            _rig.Player = m1.Position;
            before = m1.Position;
            _rig.Step();
            Assert.AreEqual(before, m1.Position, "no movement when exactly on the player");
        }

        [Test]
        public void Charger_ChargesInFixedDirection_ThenCoolsDownChasing_ThenChargesAgain()
        {
            _rig = new MonEmRig();
            Monster m2 = _rig.SpawnMonster("M2", new Vector2(5f, 0f));
            ChargeDefinition charge = m2.Definition.Charge;
            Assert.AreEqual(30, charge.DurationSteps);
            Assert.AreEqual(180, charge.CooldownSteps);

            _rig.Step();
            long chargeStart = _rig.Clock.Step - 1;
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase, "within trigger distance 6: charge");
            Assert.AreEqual(5f - charge.ChargeSpeedPerStep, m2.Position.x, Eps);
            Assert.AreEqual(new Vector2(-1f, 0f), m2.State.ChargeDirection);

            _rig.Player = new Vector2(0f, 20f);
            Vector2 before = m2.Position;
            _rig.Step(charge.DurationSteps - 1);
            Assert.AreEqual(before.x - charge.ChargeSpeedPerStep * (charge.DurationSteps - 1), m2.Position.x, 1e-3f,
                "the direction stays fixed although the player moved");
            Assert.AreEqual(0f, m2.Position.y, Eps);
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase);

            _rig.Player = m2.Position + new Vector2(0f, 2f);
            _rig.Step();
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Cooldown, m2.State.Phase, "charge lasted exactly DurationSteps");
            Assert.AreEqual(chargeStart + charge.DurationSteps + charge.CooldownSteps, m2.State.PhaseEndStep);

            for (int i = 1; i < charge.CooldownSteps; i++)
            {
                _rig.Player = m2.Position + new Vector2(0f, 2f);
                before = m2.Position;
                _rig.Step();
                Assert.AreEqual(MonsterBehaviorState.ChargePhase.Cooldown, m2.State.Phase, "no re-charge during cooldown (step " + i + ")");
                Assert.AreEqual(m2.Definition.MoveSpeedPerStep, (m2.Position - before).magnitude, Eps, "cooldown chases at moveSpeed");
            }

            _rig.Player = m2.Position + new Vector2(0f, 2f);
            _rig.Step();
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Charge, m2.State.Phase, "cooldown over: Approach re-triggers at once");
            Assert.AreEqual(0f, m2.State.ChargeDirection.x, Eps);
            Assert.AreEqual(1f, m2.State.ChargeDirection.y, Eps);
        }

        [Test]
        public void Charger_OutsideTriggerDistance_Approaches()
        {
            _rig = new MonEmRig();
            Monster m2 = _rig.SpawnMonster("M2", new Vector2(6.5f, 0f));

            _rig.Step();
            Assert.AreEqual(MonsterBehaviorState.ChargePhase.Approach, m2.State.Phase);
            Assert.AreEqual(6.5f - m2.Definition.MoveSpeedPerStep, m2.Position.x, Eps);
        }

        [Test]
        public void Ranged_InRange_StopsAndFiresByCommand_EveryCooldown()
        {
            _rig = new MonEmRig();
            Monster m3 = _rig.SpawnMonster("M3", new Vector2(0f, 5f));
            RangedDefinition ranged = m3.Definition.Ranged;
            int projectilesInResolve = -1;
            _rig.Resolve10.Action = context => projectilesInResolve = _rig.Live<EnemyProjectile>().Count;

            _rig.Step();
            long fireStep = _rig.Clock.Step - 1;
            Assert.AreEqual(0, projectilesInResolve, "TAC-0-6/F3-8: Tick only queues a command; nothing is created before Lifecycle");
            Assert.AreEqual(new Vector2(0f, 5f), m3.Position, "inside attack range the monster stands still");
            EnemyProjectile shot = _rig.Live<EnemyProjectile>()[0];
            Assert.AreEqual(new Vector2(0f, -1f), shot.Direction, "aimed at the player");
            Assert.AreEqual(m3.Position, shot.Position);
            Assert.AreEqual(fireStep + ranged.Projectile.LifetimeSteps, shot.ExpireStep);
            Assert.AreEqual(ranged.Projectile.Radius, shot.Body.Radius);
            Assert.AreEqual(6f, _rig.LookUpContactSource(shot.Id).ContactDamage, "projectile contact damage = definition Damage");
            Assert.AreEqual(TickGroup.Projectiles, shot.Group);
            Assert.AreEqual(fireStep + ranged.FireCooldownSteps, m3.State.NextFireStep);

            _rig.Step();
            Assert.AreEqual(5f - ranged.Projectile.SpeedPerStep, shot.Position.y, Eps, "the projectile flies from the next step");

            _rig.Resolve10.Action = null;
            _rig.Step(ranged.FireCooldownSteps - 2);
            Assert.AreEqual(1, _rig.Live<EnemyProjectile>().Count, "no second shot before the cooldown");
            _rig.Step();
            Assert.AreEqual(2, _rig.Live<EnemyProjectile>().Count, "second shot exactly FireCooldownSteps later");
        }

        [Test]
        public void Ranged_ProjectileExpires_ByLifetimeSystem()
        {
            _rig = new MonEmRig();
            Monster m3 = _rig.SpawnMonster("M3", new Vector2(0f, 5f));
            _rig.Step();
            EnemyProjectile shot = _rig.Live<EnemyProjectile>()[0];
            EntityHandle id = shot.Id;
            long expire = shot.ExpireStep;

            _rig.Step((int)(expire - _rig.Clock.Step));
            Assert.IsFalse(_rig.DespawnedLog.Contains((id, DespawnReason.LifetimeExpired)), "alive until ExpireStep");
            Assert.IsTrue(_rig.Lifecycle.TryGet(id, out _));
            _rig.Step();
            Assert.IsTrue(_rig.DespawnedLog.Contains((id, DespawnReason.LifetimeExpired)), "LifetimeSystem removes it at ExpireStep");
            Assert.IsFalse(_rig.Lifecycle.TryGet(id, out _));
            Assert.IsTrue(m3.IsAlive, "the shooter is unaffected");
        }

        [Test]
        public void Ranged_OutOfRange_Chases()
        {
            _rig = new MonEmRig();
            Monster m3 = _rig.SpawnMonster("M3", new Vector2(0f, 9f));

            _rig.Step();
            Assert.AreEqual(9f - m3.Definition.MoveSpeedPerStep, m3.Position.y, Eps);
            Assert.AreEqual(0, _rig.Live<EnemyProjectile>().Count);
        }

        [Test]
        public void Boss_ZoneFixture_UsesBothPatternsOnIndependentCooldowns()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load((DataFileKind.Monsters, "\"bossPatterns\": [", "\"bossPatterns\": [ " + ZonePattern));
            Assert.AreEqual(0, report.WarningCount, "TAC-F3-21: a hazardZone entry still binds without warnings");
            _rig = new MonEmRig(data, report);
            Monster boss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            BossPatternDefinition zonePattern = boss.Definition.BossPatterns[0];
            BossPatternDefinition summonPattern = boss.Definition.BossPatterns[1];
            Assert.AreEqual(BossPatternKind.HazardZone, zonePattern.Kind);
            Assert.AreEqual(BossPatternKind.Summon, summonPattern.Kind);
            _rig.Player = new Vector2(0f, 1f);

            _rig.Step();
            long useStep = _rig.Clock.Step - 1;
            EnemyHazardZone zone = _rig.Live<EnemyHazardZone>()[0];
            Assert.AreEqual(_rig.Player, zone.Position, "pattern 1 at the player's position");
            Assert.AreEqual(zonePattern.ZoneRadius, zone.Body.Radius);
            Assert.AreEqual(useStep + zonePattern.DurationSteps, zone.ExpireStep);
            Assert.AreEqual(15f, _rig.LookUpContactSource(zone.Id).ContactDamage, "zone contact damage = pattern Damage");
            Assert.AreEqual(CollisionLayer.EnemyZone, zone.Body.Layer);

            int summoned = 0;
            foreach (Monster m in _rig.Live<Monster>())
            {
                if (m == boss)
                {
                    continue;
                }

                summoned++;
                Assert.AreEqual(summonPattern.MonsterId, m.Definition.Id, "summons are the defined small monster");
                Assert.IsFalse(m.IsBoss);
                Assert.AreEqual(summonPattern.SummonRadius, (m.Position - boss.Position).magnitude, 1e-3f, "on the summon circle");
            }

            Assert.AreEqual(summonPattern.Count, summoned);
            Assert.AreEqual(useStep + zonePattern.CooldownSteps, boss.State.NextPatternStep(0));
            Assert.AreEqual(useStep + summonPattern.CooldownSteps, boss.State.NextPatternStep(1));
            Assert.AreEqual(new Vector2(10f, -1f).magnitude - boss.Definition.MoveSpeedPerStep, (_rig.Player - boss.Position).magnitude, Eps,
                "the boss chases at moveSpeed");

            _rig.Step(zonePattern.CooldownSteps - 1);
            Assert.AreEqual(1, _rig.Live<EnemyHazardZone>().Count + CountExpiredZones(), "no zone before its cooldown");
            _rig.Step();
            Assert.AreEqual(useStep + 2 * zonePattern.CooldownSteps, boss.State.NextPatternStep(0), "zone used again on its own cooldown");
            Assert.AreEqual(useStep + summonPattern.CooldownSteps, boss.State.NextPatternStep(1), "summon cooldown independent");
        }

        [Test]
        public void Boss_RealData_OnlySummons_NoHazardZoneOverSeveralCooldowns()
        {
            _rig = new MonEmRig();
            Monster boss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            Assert.AreEqual(1, boss.Definition.BossPatterns.Count, "TAC-F3-21: the shipped M4 has one pattern");
            BossPatternDefinition summonPattern = boss.Definition.BossPatterns[0];
            Assert.AreEqual(BossPatternKind.Summon, summonPattern.Kind);
            _rig.Player = new Vector2(0f, 1f);

            _rig.Step();
            long useStep = _rig.Clock.Step - 1;
            Assert.AreEqual(1 + summonPattern.Count, _rig.Live<Monster>().Count, "the summon (index 0) is used on the first Tick");
            Assert.AreEqual(useStep + summonPattern.CooldownSteps, boss.State.NextPatternStep(0));

            _rig.Step(3 * summonPattern.CooldownSteps);
            Assert.AreEqual(useStep + 4 * summonPattern.CooldownSteps, boss.State.NextPatternStep(0), "three more summons on the cooldown");
            Assert.AreEqual(1 + 4 * summonPattern.Count, _rig.Live<Monster>().Count);
            int zones = 0;
            foreach (SimEntity spawned in _rig.SpawnedLog)
            {
                if (spawned.Category == EntityCategory.EnemyZone)
                {
                    zones++;
                }
            }

            Assert.AreEqual(0, zones, "TAC-F3-21: no EnemyHazardZone from the shipped boss");
            Assert.AreEqual(0, _rig.MonsterCommands.DroppedCommandCount);
        }

        [Test]
        public void Boss_SummonRefusedByCap_StillSpendsCooldown_AndDrawsNoAngle()
        {
            _rig = new MonEmRig();
            Monster boss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            _rig.Rules.MonsterCap = 0;
            var reference = new RngStreams(MonEmRig.Seed).Get(RngStream.Summon);

            _rig.Step();
            long useStep = _rig.Clock.Step - 1;
            Assert.AreEqual(1, _rig.Live<Monster>().Count, "TAC-F3-9: all summons refused at the cap");
            Assert.AreEqual(BossPatternKind.Summon, boss.Definition.BossPatterns[0].Kind);
            Assert.AreEqual(useStep + boss.Definition.BossPatterns[0].CooldownSteps, boss.State.NextPatternStep(0),
                "the summon command was issued, so the cooldown is spent");
            Assert.AreEqual(reference.NextUInt(), _rig.Rng.Get(RngStream.Summon).NextUInt(), "refused summons consume no Summon draws");
        }

        [Test]
        public void SizeClass_UsesR25Rule_BossAlwaysLarge()
        {
            (GameData data, DataValidationReport report) = MonEmData.Load(
                (DataFileKind.Monsters, "\"maxHealth\": 5000, \"moveSpeed\": 1.2, \"radius\": 1.5", "\"maxHealth\": 5000, \"moveSpeed\": 1.2, \"radius\": 0.6"),
                (DataFileKind.Monsters, "\"maxHealth\": 20, \"moveSpeed\": 1.5, \"radius\": 0.5", "\"maxHealth\": 20, \"moveSpeed\": 1.5, \"radius\": 1.2"));
            _rig = new MonEmRig(data, report);
            Monster m1 = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            Monster bigM2 = _rig.SpawnMonster("M2", new Vector2(15f, 0f));
            Monster smallBoss = _rig.SpawnMonster("M4", new Vector2(-15f, 0f));

            Assert.IsFalse(m1.Body.IsLarge);
            Assert.IsTrue(bigM2.Body.IsLarge, "TAC-F16-6: radius above the threshold");
            Assert.IsTrue(smallBoss.IsBoss);
            Assert.IsTrue(smallBoss.Body.IsLarge, "TAC-F16-6: the boss is large regardless of radius");
            Assert.AreEqual(MonsterSizeRules.IsLarge(0.6f, true), smallBoss.Body.IsLarge);
        }

        [Test]
        public void HotReload_LiveMonsterKeepsItsDefinition_NewSpawnUsesNewData()
        {
            _rig = new MonEmRig();
            Monster old = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            (GameData data, DataValidationReport report) = MonEmData.Load(
                (DataFileKind.Monsters, "\"maxHealth\": 10,", "\"maxHealth\": 99,"));
            Assert.IsTrue(_rig.Data.TrySwap(data, report));

            Monster fresh = _rig.SpawnMonster("M1", new Vector2(-5f, 0f));
            Assert.AreEqual(10f, old.Definition.MaxHealth, "5c.3 #10: live monsters keep the spawn-time definition");
            Assert.AreEqual(99f, fresh.Hp);
        }

        [Test]
        public void Release_ClearsState_ForPoolReuse()
        {
            _rig = new MonEmRig();
            Monster boss = _rig.SpawnMonster("M4", new Vector2(10f, 0f));
            _rig.DamageNextStep(1e6f, boss);
            _rig.Step();
            Assert.IsNull(boss.Definition, "OnReleased cleared the definition");
            Assert.IsFalse(boss.IsBoss);
            Assert.AreEqual(0f, boss.MaxHp, "OnReleased cleared MaxHp");

            Monster reused = _rig.SpawnMonster("M1", new Vector2(5f, 0f));
            Assert.AreSame(boss, reused, "the pool handed back the same instance");
            Assert.IsFalse(reused.IsBoss);
            Assert.AreEqual(-1L, reused.DiedAtStep);
            Assert.IsFalse(reused.KillConfirmed);
            Assert.AreEqual(0, reused.State.PatternCount);
            Assert.IsFalse(reused.Body.IsLarge);
        }

        private int CountExpiredZones()
        {
            int expired = 0;
            foreach ((EntityHandle _, DespawnReason reason) in _rig.DespawnedLog)
            {
                if (reason == DespawnReason.LifetimeExpired)
                {
                    expired++;
                }
            }

            return expired;
        }
    }
}
