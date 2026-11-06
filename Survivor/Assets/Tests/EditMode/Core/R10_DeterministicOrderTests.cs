using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R10_DeterministicOrderTests
    {
        [Test]
        public void EntityHandle_OrdersByIndexThenGeneration()
        {
            var ids = new List<EntityHandle>
            {
                new EntityHandle(3, 2), new EntityHandle(1, 5), new EntityHandle(3, 1), new EntityHandle(2, 9), EntityHandle.None,
            };
            ids.Sort((a, b) => a.CompareTo(b));
            CollectionAssert.AreEqual(
                new[] { EntityHandle.None, new EntityHandle(1, 5), new EntityHandle(2, 9), new EntityHandle(3, 1), new EntityHandle(3, 2) },
                ids);
            Assert.IsTrue(new EntityHandle(1, 9) < new EntityHandle(2, 1));
            Assert.IsTrue(new EntityHandle(2, 2) > new EntityHandle(2, 1));
            Assert.IsFalse(new EntityHandle(2, 1) < new EntityHandle(2, 1));
        }

        [Test]
        public void CollisionEvent_OrdersByStepChannelABKind()
        {
            var a1 = new EntityHandle(1, 1);
            var a2 = new EntityHandle(2, 1);
            var b1 = new EntityHandle(5, 1);
            var b2 = new EntityHandle(5, 2);
            var expected = new List<CollisionEvent>
            {
                new CollisionEvent(10, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, a2, b1),
                new CollisionEvent(11, CollisionEventKind.Exit, CollisionChannel.PlayerProjectileVsMonster, a2, b2),
                new CollisionEvent(11, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, a1, b1),
                new CollisionEvent(11, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, a1, b2),
                new CollisionEvent(11, CollisionEventKind.Exit, CollisionChannel.PlayerOrbVsMonster, a1, b2),
                new CollisionEvent(11, CollisionEventKind.Enter, CollisionChannel.PlayerOrbVsMonster, a2, b1),
                new CollisionEvent(11, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsChest, a1, b1),
            };

            var shuffled = new List<CollisionEvent>(expected);
            shuffled.Reverse();
            (shuffled[1], shuffled[4]) = (shuffled[4], shuffled[1]);
            shuffled.Sort();
            CollectionAssert.AreEqual(expected, shuffled);
            Assert.AreEqual(0, expected[0].CompareTo(expected[0]));
        }

        [Test]
        public void CollisionEvent_ToStringIsCsvRowInKeyOrder()
        {
            var e = new CollisionEvent(120, CollisionEventKind.Enter, CollisionChannel.PlayerBodyVsMonster,
                new EntityHandle(1, 1), new EntityHandle(17, 3));
            Assert.AreEqual("120,PlayerBodyVsMonster,1:1,17:3,Enter", e.ToString());
        }

        [Test]
        public void SimulationCommand_OrdersBySourceThenSequence()
        {
            var player = new EntityHandle(1, 1);
            var monster = new EntityHandle(7, 2);
            var commands = new List<SimulationCommand>
            {
                new SimulationCommand(SimulationCommandKind.SpawnGem, monster).WithSequence(0),
                new SimulationCommand(SimulationCommandKind.SpawnPlayerProjectile, player).WithSequence(1),
                new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None).WithSequence(0),
                new SimulationCommand(SimulationCommandKind.SpawnPlayerProjectile, player).WithSequence(0),
            };
            commands.Sort((a, b) => SimulationCommand.Compare(a, b));

            Assert.AreEqual(EntityHandle.None, commands[0].Source);
            Assert.AreEqual(player, commands[1].Source);
            Assert.AreEqual(0, commands[1].Sequence);
            Assert.AreEqual(player, commands[2].Source);
            Assert.AreEqual(1, commands[2].Sequence);
            Assert.AreEqual(monster, commands[3].Source);
        }

        [Test]
        public void SimulationCommand_WithSequenceKeepsEveryOtherField()
        {
            var original = new SimulationCommand(SimulationCommandKind.SpawnPlayerOrb, new EntityHandle(1, 1),
                target: new EntityHandle(4, 2), reason: DespawnReason.Consumed, position: new UnityEngine.Vector2(1f, 2f),
                direction: new UnityEngine.Vector2(0f, 1f), radius: 3f, definitionId: "W2", count: 5,
                levelIndex: 2, slotIndex: 1, orbIndex: 3, ignoreCap: true);
            SimulationCommand copy = original.WithSequence(7);

            Assert.AreEqual(7, copy.Sequence);
            Assert.AreEqual(original.Kind, copy.Kind);
            Assert.AreEqual(original.Source, copy.Source);
            Assert.AreEqual(original.Target, copy.Target);
            Assert.AreEqual(original.Reason, copy.Reason);
            Assert.AreEqual(original.Position, copy.Position);
            Assert.AreEqual(original.Direction, copy.Direction);
            Assert.AreEqual(original.Radius, copy.Radius);
            Assert.AreEqual(original.DefinitionId, copy.DefinitionId);
            Assert.AreEqual(original.Count, copy.Count);
            Assert.AreEqual(original.LevelIndex, copy.LevelIndex);
            Assert.AreEqual(original.SlotIndex, copy.SlotIndex);
            Assert.AreEqual(original.OrbIndex, copy.OrbIndex);
            Assert.AreEqual(original.IgnoreCap, copy.IgnoreCap);
        }
    }
}
