using NUnit.Framework;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class CoreValueTypeTests
    {
        [Test]
        public void StrategySelection_PresetsMatchDocument()
        {
            StrategySelection shipping = StrategySelection.ShippingDefault;
            Assert.AreEqual(PoolStrategyKind.CustomPool, shipping.Pool);
            Assert.AreEqual(TargetProviderKind.SpatialHash, shipping.Target);
            Assert.AreEqual(TickStrategyKind.CentralTick, shipping.Tick);
            Assert.AreEqual(CollisionStrategyKind.SelfSquaredDistance, shipping.Collision);
            Assert.AreEqual(CollisionDetectionMode2D.Discrete, shipping.EngineDetectionMode);
            Assert.AreEqual(2f, shipping.SpatialHashCellSize);

            StrategySelection baseline = StrategySelection.Baseline;
            Assert.AreEqual(PoolStrategyKind.InstantiateDestroy, baseline.Pool);
            Assert.AreEqual(TargetProviderKind.BruteForce, baseline.Target);
            Assert.AreEqual(TickStrategyKind.PerObjectFixedUpdate, baseline.Tick);
            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, baseline.Collision);
            Assert.AreEqual(CollisionDetectionMode2D.Discrete, baseline.EngineDetectionMode);
        }

        [Test]
        public void SimulationRules_DefaultsEqualNormalPlay()
        {
            var rules = new SimulationRules();
            Assert.AreEqual(400, rules.MonsterCap);
            Assert.IsFalse(rules.DespawnDistanceOverridden);
            Assert.IsTrue(rules.PlayerTakesDamage);
            Assert.IsTrue(rules.WeaponsEnabled);
            Assert.IsTrue(rules.LevelUpEnabled);
            Assert.AreEqual(long.MaxValue, rules.StopAtStep);
            Assert.IsTrue(rules.RecordBestTime);
            Assert.IsTrue(rules.AutoPauseOnFocusLoss);
            Assert.AreEqual(0.02f, rules.ChestDropChance);
        }

        [Test]
        public void MoveCommand_NoneHasZeroDirectionAndPlayerSpeed()
        {
            Assert.AreEqual(Vector2.zero, MoveCommand.None.Direction);
            Assert.Less(MoveCommand.None.SpeedOverride, 0f);
            var command = new MoveCommand(Vector2.up);
            Assert.AreEqual(-1f, command.SpeedOverride);
            Assert.AreEqual(4.5f, new MoveCommand(Vector2.up, 4.5f).SpeedOverride);
        }

        [Test]
        public void StepContext_StoresEveryField()
        {
            var rules = new SimulationRules();
            var context = new StepContext(42L, new Vector2(1f, -2f), rules, null, null);
            Assert.AreEqual(42L, context.Step);
            Assert.AreEqual(new Vector2(1f, -2f), context.PlayerPosition);
            Assert.AreSame(rules, context.Rules);
            Assert.IsNull(context.Targets);
            Assert.IsNull(context.Commands);
        }

        [Test]
        public void CollisionBody_SlotsStartUnassigned()
        {
            var body = new CollisionBody();
            Assert.AreEqual(-1, body.StrategySlot);
            Assert.AreEqual(-1, body.PhysicsSlot);
            Assert.IsFalse(body.DrivesTransform);
        }

        [Test]
        public void EnumValues_MatchDocument()
        {
            Assert.AreEqual(9, (int)SimulationPhase.End);
            Assert.AreEqual(3, (int)SimulationPhase.BuildIndex);
            Assert.AreEqual(8, (int)SimulationCommandKind.SpawnChest);
            Assert.AreEqual(5, (int)DespawnReason.RunReset);
            Assert.AreEqual(7, (int)EntityCategory.Chest);
            Assert.AreEqual(9, (int)CollisionLayer.Chest);
            Assert.AreEqual(8, (int)CollisionChannel.PlayerBodyVsChest);
            Assert.AreEqual(1, (int)CollisionEventKind.Exit);
            Assert.AreEqual(2, (int)PoolStrategyKind.UnityObjectPool);
            Assert.AreEqual(2, (int)TargetProviderKind.Physics2DQuery);
            Assert.AreEqual(1, (int)TickStrategyKind.CentralTick);
            Assert.AreEqual(1, (int)CollisionStrategyKind.SelfSquaredDistance);
            Assert.AreEqual(1, (int)TickGroup.Projectiles);
        }
    }
}
