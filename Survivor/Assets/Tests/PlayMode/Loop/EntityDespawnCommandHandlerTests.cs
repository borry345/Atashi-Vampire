using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    /// Despawn commands reach EntityLifecycle through the buffer's registered handler (5.4).
    public sealed class EntityDespawnCommandHandlerTests
    {
        [TearDown]
        public void TearDown()
        {
            LoopPmWorld.DestroyTestObjects();
        }

        [Test]
        public void DespawnHandler_DespawnsThroughLifecycle_AndIgnoresGoneTargets()
        {
            var world = new LoopPmWorld();
            var buffer = new SimulationCommandBuffer();
            buffer.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(world.Lifecycle));
            LoopPmTickEntity entity = null;
            world.InStep(SimulationPhase.Lifecycle, () => entity = world.SpawnTick(EntityCategory.Monster, Vector2.zero));
            EntityHandle id = entity.Id;
            var reasons = new List<DespawnReason>();
            world.Lifecycle.Despawned += (e, r) => reasons.Add(r);

            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, id, target: id, reason: DespawnReason.Consumed));
            buffer.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, EntityHandle.None, target: new EntityHandle(40, 1)));
            world.InStep(SimulationPhase.Lifecycle, () => buffer.OnPhase(SimulationPhase.Lifecycle, world.Context(buffer)));

            Assert.IsFalse(world.Ids.IsLive(id));
            CollectionAssert.AreEqual(new[] { DespawnReason.Consumed }, reasons);
        }
    }
}
