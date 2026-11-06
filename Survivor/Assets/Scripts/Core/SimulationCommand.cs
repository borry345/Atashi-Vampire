using UnityEngine;

namespace Survivor.Core
{
    /// One deferred spawn/despawn request; applied in Lifecycle order 0 sorted by (Source, Sequence) (rule R10).
    public readonly struct SimulationCommand
    {
        /// What the command does; selects the handler.
        public readonly SimulationCommandKind Kind;

        /// Issuing entity (sort key 1); EntityId.None for dev/weapon-less sources, the player id for weapons.
        public readonly EntityId Source;

        /// Per-source order within the step (sort key 2), assigned by the buffer.
        public readonly int Sequence;

        /// Despawn target.
        public readonly EntityId Target;

        /// Despawn reason.
        public readonly DespawnReason Reason;

        /// Spawn point or summon center.
        public readonly Vector2 Position;

        /// Initial direction of the spawned entity.
        public readonly Vector2 Direction;

        /// Summon / dev spawn ring radius.
        public readonly float Radius;

        /// Monster id or weapon id.
        public readonly string DefinitionId;

        /// Number of entities to spawn.
        public readonly int Count;

        /// Weapon level (0-based) for player spawns.
        public readonly int LevelIndex;

        /// Weapon slot for player spawns (orb/zone ownership).
        public readonly int SlotIndex;

        /// Orb index for SpawnPlayerOrb.
        public readonly int OrbIndex;

        /// Boss / dev "spawn N now" with the cap lifted.
        public readonly bool IgnoreCap;

        /// Creates a command; Sequence is normally left at 0 and assigned by the buffer through WithSequence.
        public SimulationCommand(
            SimulationCommandKind kind,
            EntityId source,
            EntityId target = default,
            DespawnReason reason = DespawnReason.Killed,
            Vector2 position = default,
            Vector2 direction = default,
            float radius = 0f,
            string definitionId = null,
            int count = 0,
            int levelIndex = 0,
            int slotIndex = 0,
            int orbIndex = 0,
            bool ignoreCap = false,
            int sequence = 0)
        {
            Kind = kind;
            Source = source;
            Sequence = sequence;
            Target = target;
            Reason = reason;
            Position = position;
            Direction = direction;
            Radius = radius;
            DefinitionId = definitionId;
            Count = count;
            LevelIndex = levelIndex;
            SlotIndex = slotIndex;
            OrbIndex = orbIndex;
            IgnoreCap = ignoreCap;
        }

        /// Returns a copy with Sequence replaced (used by the command buffer on Enqueue).
        public SimulationCommand WithSequence(int sequence)
        {
            return new SimulationCommand(Kind, Source, Target, Reason, Position, Direction, Radius, DefinitionId,
                Count, LevelIndex, SlotIndex, OrbIndex, IgnoreCap, sequence);
        }

        /// Orders by Source (EntityId order), then Sequence (rule R10).
        public static int Compare(in SimulationCommand a, in SimulationCommand b)
        {
            int bySource = a.Source.CompareTo(b.Source);
            return bySource != 0 ? bySource : a.Sequence.CompareTo(b.Sequence);
        }
    }
}
