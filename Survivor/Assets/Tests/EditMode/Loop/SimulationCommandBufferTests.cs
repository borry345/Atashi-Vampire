using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Loop
{
    /// Deferred command buffer: per-source sequence, (Source, Sequence) order (R10), handler registry (5.4, L2).
    public sealed class SimulationCommandBufferTests
    {
        private static readonly StepContext Lifecycle = new StepContext(3, Vector2.zero, new SimulationRules(), null, null);

        private static SimulationCommand Spawn(EntityHandle source, int count)
        {
            return new SimulationCommand(SimulationCommandKind.SpawnMonsters, source, count: count);
        }

        [Test]
        public void Lifecycle_SortsBySourceThenSequence()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            buffer.RegisterHandler(SimulationCommandKind.SpawnMonsters, handler);
            var player = new EntityHandle(1, 1);
            var monster = new EntityHandle(3, 2);

            buffer.Enqueue(Spawn(monster, 1));
            buffer.Enqueue(Spawn(player, 2));
            buffer.Enqueue(Spawn(EntityHandle.None, 3));
            buffer.Enqueue(Spawn(monster, 4));
            buffer.Enqueue(Spawn(player, 5));
            Assert.AreEqual(5, buffer.PendingCount);

            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);

            var order = new List<string>();
            foreach (SimulationCommand c in handler.Handled)
            {
                order.Add(c.Source + "/" + c.Sequence + "/" + c.Count);
            }

            CollectionAssert.AreEqual(new[] { "0:0/0/3", "1:1/0/2", "1:1/1/5", "3:2/0/1", "3:2/1/4" }, order);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void OtherPhases_DoNothing()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            buffer.RegisterHandler(SimulationCommandKind.SpawnMonsters, handler);
            buffer.Enqueue(Spawn(EntityHandle.None, 1));
            foreach (SimulationPhase phase in Enum.GetValues(typeof(SimulationPhase)))
            {
                if (phase != SimulationPhase.Lifecycle)
                {
                    buffer.OnPhase(phase, Lifecycle);
                }
            }

            Assert.AreEqual(0, handler.Handled.Count);
            Assert.AreEqual(1, buffer.PendingCount);
        }

        [Test]
        public void DuplicateDespawnOfSameTarget_SecondIgnored()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            buffer.RegisterHandler(SimulationCommandKind.Despawn, handler);
            var target = new EntityHandle(5, 1);
            var other = new EntityHandle(6, 1);
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, new EntityHandle(2, 1), target: target, reason: DespawnReason.Consumed));
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, new EntityHandle(1, 1), target: target, reason: DespawnReason.Killed));
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, new EntityHandle(1, 1), target: other));

            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);

            Assert.AreEqual(2, handler.Handled.Count);
            Assert.AreEqual(target, handler.Handled[0].Target);
            Assert.AreEqual(DespawnReason.Killed, handler.Handled[0].Reason, "the first in sorted order wins");
            Assert.AreEqual(other, handler.Handled[1].Target);
        }

        [Test]
        public void UnregisteredKind_ThrowsBeforeApplyingAnything()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            buffer.RegisterHandler(SimulationCommandKind.Despawn, handler);
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, EntityHandle.None, target: new EntityHandle(1, 1)));
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnGem, EntityHandle.None));

            Assert.Throws<InvalidOperationException>(() => buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle));
            Assert.AreEqual(0, handler.Handled.Count);
        }

        [Test]
        public void RegisterHandler_TwiceForSameKind_Throws()
        {
            var buffer = new SimulationCommandBuffer();
            buffer.RegisterHandler(SimulationCommandKind.SpawnGem, new LoopEmCommandHandler());
            Assert.IsTrue(buffer.HasHandler(SimulationCommandKind.SpawnGem));
            Assert.Throws<InvalidOperationException>(() => buffer.RegisterHandler(SimulationCommandKind.SpawnGem, new LoopEmCommandHandler()));
            Assert.Throws<ArgumentNullException>(() => buffer.RegisterHandler(SimulationCommandKind.SpawnChest, null));
        }

        [Test]
        public void CommandsQueuedWhileApplying_WaitForNextLifecycle_WithFreshSequences()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            handler.OnHandle = c =>
            {
                if (c.Count == 1)
                {
                    buffer.Enqueue(Spawn(EntityHandle.None, 2));
                }
            };
            buffer.RegisterHandler(SimulationCommandKind.SpawnMonsters, handler);
            buffer.Enqueue(Spawn(EntityHandle.None, 1));

            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);
            Assert.AreEqual(1, handler.Handled.Count);
            Assert.AreEqual(1, buffer.PendingCount);

            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);
            Assert.AreEqual(2, handler.Handled.Count);
            Assert.AreEqual(0, handler.Handled[1].Sequence);
        }

        [Test]
        public void Clear_DropsQueuedCommands_KeepsHandlers()
        {
            var buffer = new SimulationCommandBuffer();
            var handler = new LoopEmCommandHandler();
            buffer.RegisterHandler(SimulationCommandKind.SpawnMonsters, handler);
            buffer.Enqueue(Spawn(EntityHandle.None, 1));
            buffer.Clear();
            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);
            Assert.AreEqual(0, handler.Handled.Count);
            buffer.Enqueue(Spawn(EntityHandle.None, 1));
            buffer.OnPhase(SimulationPhase.Lifecycle, Lifecycle);
            Assert.AreEqual(0, handler.Handled[0].Sequence, "sequence counters restart after Clear");
        }
    }
}
