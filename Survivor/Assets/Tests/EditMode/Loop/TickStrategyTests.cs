using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies.Tick;
using UnityEngine;

namespace Survivor.Tests.EditMode.Loop
{
    /// CentralTickStrategy (TAC-F17-1) and SelfTickStrategy registration (TAC-F17-2) plus SelfTickTemplateDecorator (5.6).
    public sealed class TickStrategyTests
    {
        [TearDown]
        public void TearDown()
        {
            LoopEmObjects.DestroyTestObjects();
        }

        private sealed class Probe : IUpdatable
        {
            private readonly List<string> _log;
            internal Action OnTick;

            internal Probe(List<string> log, int index, TickGroup group)
            {
                _log = log;
                Id = new EntityHandle(index, 1);
                Group = group;
            }

            public EntityHandle Id { get; }

            public TickGroup Group { get; }

            public void Tick(in StepContext context)
            {
                _log.Add(Id.Index + "@" + context.Step);
                OnTick?.Invoke();
            }
        }

        private static StepContext At(long step)
        {
            return new StepContext(step, Vector2.zero, null, null, null);
        }

        [Test]
        public void Central_TicksInRegistrationOrder_PerGroup()
        {
            var log = new List<string>();
            var ticks = new CentralTickStrategy();
            ticks.Register(new Probe(log, 3, TickGroup.Monsters), null);
            ticks.Register(new Probe(log, 1, TickGroup.Monsters), null);
            ticks.Register(new Probe(log, 2, TickGroup.Projectiles), null);
            Assert.AreEqual(TickStrategyKind.CentralTick, ticks.Kind);
            Assert.AreEqual(2, ticks.RegisteredCount(TickGroup.Monsters));
            Assert.AreEqual(1, ticks.RegisteredCount(TickGroup.Projectiles));

            ticks.RunGroup(TickGroup.Monsters, At(5));
            CollectionAssert.AreEqual(new[] { "3@5", "1@5" }, log);
            log.Clear();
            ticks.RunGroup(TickGroup.Projectiles, At(5));
            CollectionAssert.AreEqual(new[] { "2@5" }, log);
        }

        [Test]
        public void Central_UnregisterDuringRun_SkipsRemovedAndKeepsOrder()
        {
            var log = new List<string>();
            var ticks = new CentralTickStrategy();
            var a = new Probe(log, 1, TickGroup.Monsters);
            var b = new Probe(log, 2, TickGroup.Monsters);
            var c = new Probe(log, 3, TickGroup.Monsters);
            var d = new Probe(log, 4, TickGroup.Monsters);
            ticks.Register(a, null);
            ticks.Register(b, null);
            ticks.Register(c, null);
            ticks.Register(d, null);
            b.OnTick = () =>
            {
                ticks.Unregister(b); // itself
                ticks.Unregister(c); // a later one
            };

            Assert.DoesNotThrow(() => ticks.RunGroup(TickGroup.Monsters, At(1)));
            CollectionAssert.AreEqual(new[] { "1@1", "2@1", "4@1" }, log);
            Assert.AreEqual(2, ticks.RegisteredCount(TickGroup.Monsters));
            log.Clear();
            ticks.RunGroup(TickGroup.Monsters, At(2));
            CollectionAssert.AreEqual(new[] { "1@2", "4@2" }, log);
        }

        [Test]
        public void Central_RegisterDuringRun_JoinsAfterTheRun()
        {
            var log = new List<string>();
            var ticks = new CentralTickStrategy();
            var a = new Probe(log, 1, TickGroup.Projectiles);
            var late = new Probe(log, 9, TickGroup.Projectiles);
            a.OnTick = () =>
            {
                if (ticks.RegisteredCount(TickGroup.Projectiles) == 1)
                {
                    ticks.Register(late, null);
                }
            };
            ticks.Register(a, null);
            ticks.RunGroup(TickGroup.Projectiles, At(1));
            CollectionAssert.AreEqual(new[] { "1@1" }, log);
            Assert.AreEqual(2, ticks.RegisteredCount(TickGroup.Projectiles));
            log.Clear();
            ticks.RunGroup(TickGroup.Projectiles, At(2));
            CollectionAssert.AreEqual(new[] { "1@2", "9@2" }, log);
        }

        [Test]
        public void Central_UnregisterOutsideRun_ThenReRegister_GoesToTheEnd()
        {
            var log = new List<string>();
            var ticks = new CentralTickStrategy();
            var a = new Probe(log, 1, TickGroup.Monsters);
            var b = new Probe(log, 2, TickGroup.Monsters);
            ticks.Register(a, null);
            ticks.Register(b, null);
            ticks.Unregister(a);
            ticks.Register(a, null);
            ticks.RunGroup(TickGroup.Monsters, At(0));
            CollectionAssert.AreEqual(new[] { "2@0", "1@0" }, log);
        }

        [Test]
        public void Central_ContractViolations_Throw_AndClearEmpties()
        {
            var ticks = new CentralTickStrategy();
            var a = new Probe(new List<string>(), 1, TickGroup.Monsters);
            ticks.Register(a, null);
            Assert.Throws<InvalidOperationException>(() => ticks.Register(a, null));
            Assert.Throws<InvalidOperationException>(() => ticks.Unregister(new Probe(new List<string>(), 2, TickGroup.Monsters)));
            Assert.Throws<ArgumentNullException>(() => ticks.Register(null, null));
            a.OnTick = () => ticks.RunGroup(TickGroup.Projectiles, At(0));
            Assert.Throws<InvalidOperationException>(() => ticks.RunGroup(TickGroup.Monsters, At(0)), "not reentrant");
            ticks.Clear();
            Assert.AreEqual(0, ticks.RegisteredCount(TickGroup.Monsters));
            Assert.DoesNotThrow(() => ticks.Register(a, null), "registrable again after Clear");
        }

        [Test]
        public void Decorator_AddsDisabledTickers_ByCategory_Idempotent()
        {
            var decorator = new SelfTickTemplateDecorator();
            var monster = new GameObject("LoopEmMonsterTemplate");
            var projectile = new GameObject("LoopEmProjectileTemplate");
            var enemyProjectile = new GameObject("LoopEmEnemyProjectileTemplate");
            var gem = new GameObject("LoopEmGemTemplate");
            decorator.Decorate(monster, EntityCategory.Monster);
            decorator.Decorate(monster, EntityCategory.Monster);
            decorator.Decorate(projectile, EntityCategory.PlayerProjectile);
            decorator.Decorate(enemyProjectile, EntityCategory.EnemyProjectile);
            decorator.Decorate(gem, EntityCategory.Gem);

            Assert.AreEqual(1, monster.GetComponents<MonsterSelfTicker>().Length);
            Assert.IsFalse(monster.GetComponent<MonsterSelfTicker>().enabled);
            Assert.IsNull(monster.GetComponent<ProjectileSelfTicker>());
            Assert.IsFalse(projectile.GetComponent<ProjectileSelfTicker>().enabled);
            Assert.IsFalse(enemyProjectile.GetComponent<ProjectileSelfTicker>().enabled);
            Assert.IsNull(gem.GetComponent<MonsterSelfTicker>());
            Assert.IsNull(gem.GetComponent<ProjectileSelfTicker>());
            Assert.Throws<ArgumentNullException>(() => decorator.Decorate(null, EntityCategory.Monster));
        }

        [Test]
        public void Self_RegisterBindsAndEnables_UnregisterDisables_RunGroupIsNoOp()
        {
            var clock = new SimulationClock();
            var ticks = new SelfTickStrategy(clock);
            var log = new List<string>();
            var host = new GameObject("LoopEmSelfHost");
            new SelfTickTemplateDecorator().Decorate(host, EntityCategory.Monster);
            var probe = new Probe(log, 1, TickGroup.Monsters);
            MonsterSelfTicker ticker = host.GetComponent<MonsterSelfTicker>();

            ticks.Register(probe, host);
            Assert.AreEqual(TickStrategyKind.PerObjectFixedUpdate, ticks.Kind);
            Assert.IsTrue(ticker.enabled);
            Assert.AreSame(probe, ticker.Owner);
            Assert.AreEqual(1, ticks.RegisteredCount(TickGroup.Monsters));
            ticks.RunGroup(TickGroup.Monsters, At(0));
            Assert.AreEqual(0, log.Count, "RunGroup does nothing in per-object mode");

            ticks.Unregister(probe);
            Assert.IsFalse(ticker.enabled);
            Assert.IsNull(ticker.Owner);
            Assert.AreEqual(0, ticks.RegisteredCount(TickGroup.Monsters));
        }

        [Test]
        public void Self_ProjectileGroupUsesProjectileTicker_ClearDisablesAll()
        {
            var ticks = new SelfTickStrategy(new SimulationClock());
            var host = new GameObject("LoopEmSelfProjectile");
            new SelfTickTemplateDecorator().Decorate(host, EntityCategory.EnemyProjectile);
            ticks.Register(new Probe(new List<string>(), 1, TickGroup.Projectiles), host);
            ProjectileSelfTicker ticker = host.GetComponent<ProjectileSelfTicker>();
            Assert.IsTrue(ticker.enabled);
            ticks.Clear();
            Assert.IsFalse(ticker.enabled);
            Assert.AreEqual(0, ticks.RegisteredCount(TickGroup.Projectiles));
        }

        [Test]
        public void Self_ContractViolations_Throw()
        {
            var ticks = new SelfTickStrategy(new SimulationClock());
            var bare = new GameObject("LoopEmBareHost");
            var probe = new Probe(new List<string>(), 1, TickGroup.Monsters);
            Assert.Throws<InvalidOperationException>(() => ticks.Register(probe, bare), "host without a ticker");
            Assert.Throws<ArgumentNullException>(() => ticks.Register(probe, null));
            Assert.Throws<InvalidOperationException>(() => ticks.Unregister(probe));
            Assert.Throws<ArgumentNullException>(() => new SelfTickStrategy(null));
            new SelfTickTemplateDecorator().Decorate(bare, EntityCategory.Monster);
            ticks.Register(probe, bare);
            Assert.Throws<InvalidOperationException>(() => ticks.Register(probe, bare));
        }

        [Test]
        public void Keyboard_DirectionIsUnitOrZero_DiagonalNotFaster()
        {
            Assert.AreEqual(Vector2.zero, KeyboardInputSource.ComputeDirection(false, false, false, false));
            Assert.AreEqual(Vector2.zero, KeyboardInputSource.ComputeDirection(true, true, false, false), "opposite keys cancel");
            Assert.AreEqual(Vector2.right, KeyboardInputSource.ComputeDirection(false, true, false, false));
            Assert.AreEqual(Vector2.down, KeyboardInputSource.ComputeDirection(false, false, true, false));
            Vector2 diagonal = KeyboardInputSource.ComputeDirection(false, true, false, true);
            Assert.AreEqual(1f, diagonal.magnitude, 1e-6f);
            Assert.AreEqual(diagonal.x, diagonal.y, 1e-6f);

            var host = new GameObject("LoopEmKeyboard");
            KeyboardInputSource source = host.AddComponent<KeyboardInputSource>();
            MoveCommand move = source.ReadMove(10, Vector2.one);
            Assert.AreEqual(Vector2.zero, move.Direction, "nothing latched before the first Update");
            Assert.Less(move.SpeedOverride, 0f, "keyboard uses the player's own speed");
        }
    }
}
