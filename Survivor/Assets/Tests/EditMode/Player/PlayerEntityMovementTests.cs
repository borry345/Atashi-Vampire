using System;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Player
{
    // F1 (TAC-F1-1..4, F1-7) and the player registration path (5.12 rev 16, 5c.3 #1 exception).
    public sealed class PlayerEntityMovementTests
    {
        private PlrRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Destroy();
            _rig = null;
        }

        [Test]
        public void RegisterForRun_AllocatesBodyThenMagnetAndRegistersBoth()
        {
            _rig = new PlrRig(start: new Vector2(3f, -4f));
            PlayerEntity player = _rig.Player;
            Assert.AreEqual("1:1", player.Body.Id.ToString());
            Assert.AreEqual("2:1", player.MagnetBody.Id.ToString());
            Assert.AreEqual(CollisionLayer.PlayerBody, player.Body.Layer);
            Assert.AreEqual(CollisionLayer.PlayerMagnet, player.MagnetBody.Layer);
            Assert.AreEqual(0.5f, player.Body.Radius);
            Assert.AreEqual(2.0f, player.MagnetBody.Radius);
            Assert.AreSame(_rig.Host, player.Body.Host);
            Assert.AreSame(_rig.Host, player.MagnetBody.Host);
            Assert.AreEqual(new Vector2(3f, -4f), player.Position);
            Assert.AreEqual(player.Position, player.MagnetBody.Position);
            CollectionAssert.AreEquivalent(new[] { player.Body, player.MagnetBody }, _rig.Collisions.Bodies);
            CollectionAssert.AreEquivalent(new[] { player.Body, player.MagnetBody }, _rig.Physics.Bodies);
            Assert.IsNull(_rig.Host.GetComponent<Rigidbody2D>(), "PLAYER never adds physics components");
            Assert.IsNull(_rig.Host.GetComponent<Collider2D>());
        }

        [Test]
        public void RegisterForRun_TwiceThrowsAndNullThrows()
        {
            _rig = new PlrRig();
            Assert.Throws<InvalidOperationException>(() => _rig.Player.RegisterForRun(_rig.Collisions, Vector2.zero));
            _rig.Player.UnregisterFromRun();
            Assert.Throws<ArgumentNullException>(() => _rig.Player.RegisterForRun(null, Vector2.zero));
        }

        [Test]
        public void UnregisterFromRun_RemovesBothBodiesAndIsANoOpWhenNotRegistered()
        {
            _rig = new PlrRig();
            _rig.Player.UnregisterFromRun();
            Assert.AreEqual(0, _rig.Collisions.Bodies.Count);
            Assert.AreEqual(0, _rig.Physics.Bodies.Count);
            Assert.IsFalse(_rig.Player.IsRegistered);
            Assert.DoesNotThrow(() => _rig.Player.UnregisterFromRun());
            _rig.Ids.Reset();
            _rig.Player.RegisterForRun(_rig.Collisions, Vector2.one);
            Assert.AreEqual("1:1", _rig.Player.Body.Id.ToString(), "the next run starts again at 1:1 after the allocator reset");
            Assert.AreEqual("2:1", _rig.Player.MagnetBody.Id.ToString());
        }

        [Test]
        public void P3_MagnetRadiusFollowsPickupBonusAndNotifiesTheStrategy()
        {
            _rig = new PlrRig();
            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            Assert.AreEqual(StatMath.ApplyMultiplier(2.0f, 0.10f), _rig.Player.MagnetBody.Radius);
            Assert.AreEqual(1, _rig.Collisions.RadiusNotifications.Count);
            Assert.AreSame(_rig.Player.MagnetBody, _rig.Collisions.RadiusNotifications[0]);
            Assert.AreEqual(0.5f, _rig.Player.Body.Radius, "the body radius does not change");
        }

        [Test]
        public void TACF1_1_InputIsReadOnceInBeginWithStepAndPreviousPosition()
        {
            _rig = new PlrRig();
            _rig.Input.Next = new MoveCommand(MoveMath.ToUnitDirection(1, 0));
            _rig.Steps(3);
            Assert.AreEqual(3, _rig.Input.Calls.Count, "exactly one ReadMove per step");
            float speed = _rig.Data.PlayerStats.MoveSpeedPerStep;
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, _rig.Input.Calls[i].step);
                Assert.AreEqual(i * speed, _rig.Input.Calls[i].position.x, 1e-6f);
            }
        }

        [Test]
        public void TACF1_2_DiagonalStepLengthEqualsSingleAxis()
        {
            _rig = new PlrRig();
            _rig.Input.Next = new MoveCommand(MoveMath.ToUnitDirection(1, 0));
            _rig.Step();
            float axis = _rig.Player.Position.magnitude;
            Vector2 before = _rig.Player.Position;
            _rig.Input.Next = new MoveCommand(MoveMath.ToUnitDirection(1, 1));
            _rig.Step();
            float diagonal = (_rig.Player.Position - before).magnitude;
            Assert.AreEqual(axis, diagonal, 1e-6f);
        }

        [Test]
        public void TACF1_3_SpeedIsMoveSpeedPerStepTimesP2AndOverrideIsConvertedPerStep()
        {
            _rig = new PlrRig();
            _rig.StepAndCloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P2"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P2"));
            _rig.Clock.Open();
            Vector2 start = _rig.Player.Position;
            _rig.Input.Next = new MoveCommand(Vector2.up);
            _rig.Step();
            float expected = StatMath.ApplyMultiplier(_rig.Data.PlayerStats.MoveSpeedPerStep, 0.20f);
            Assert.AreEqual(expected, _rig.Player.Position.y - start.y, 1e-7f);

            start = _rig.Player.Position;
            _rig.Input.Next = new MoveCommand(Vector2.up, 6f);
            _rig.Step();
            Assert.AreEqual(StepMath.PerSecondToPerStep(6f), _rig.Player.Position.y - start.y, 1e-7f, "SpeedOverride is units per second");
        }

        [Test]
        public void TACF1_4_PositionIsNeverClampedAndBodiesFollow()
        {
            _rig = new PlrRig(start: new Vector2(1e6f, -1e6f));
            _rig.Input.Next = new MoveCommand(Vector2.right);
            _rig.Steps(10);
            Assert.Greater(_rig.Player.Position.x, 1e6f);
            Assert.AreEqual(-1e6f, _rig.Player.Position.y);
            Assert.AreEqual(_rig.Player.Position, _rig.Player.Body.Position);
            Assert.AreEqual(_rig.Player.Position, _rig.Player.MagnetBody.Position);
        }

        [Test]
        public void TACF1_7_PlayerMoveWithoutThisStepsBeginDoesNotMove()
        {
            _rig = new PlrRig();
            _rig.Input.Next = new MoveCommand(Vector2.right);
            var context = new StepContext(5, Vector2.zero, _rig.Rules, _rig.Targets, _rig.Commands);
            _rig.Movement.OnPhase(SimulationPhase.PlayerMove, context);
            Assert.AreEqual(Vector2.zero, _rig.Player.Position);
            Assert.AreEqual(0, _rig.Input.Calls.Count);
        }

        [Test]
        public void SetInputSource_ReplacesTheSourceAndRejectsNull()
        {
            _rig = new PlrRig();
            var other = new PlrInput { Next = new MoveCommand(Vector2.left) };
            _rig.Movement.SetInputSource(other);
            _rig.Step();
            Assert.AreEqual(1, other.Calls.Count);
            Assert.AreEqual(0, _rig.Input.Calls.Count);
            Assert.Less(_rig.Player.Position.x, 0f);
            Assert.Throws<ArgumentNullException>(() => _rig.Movement.SetInputSource(null));
        }

        [Test]
        public void PlayerHealth_DamageClampsHealFollowsR23AndSetHpClamps()
        {
            var health = new PlayerHealth();
            health.ResetForRun(100f);
            health.TakeDamage(50f);
            health.Heal();
            Assert.AreEqual(80f, health.Hp, 1e-5f);
            health.Heal();
            Assert.AreEqual(100f, health.Hp, "heal stops at MaxHp");
            health.TakeDamage(150f);
            Assert.AreEqual(0f, health.Hp);
            Assert.IsTrue(health.IsDead);
            health.SetHp(500f);
            Assert.AreEqual(100f, health.Hp);
            health.SetHp(-3f);
            Assert.AreEqual(0f, health.Hp);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(-1f));
        }
    }
}
