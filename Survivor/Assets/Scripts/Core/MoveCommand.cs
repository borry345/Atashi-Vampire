using UnityEngine;

namespace Survivor.Core
{
    /// Movement intent for one step.
    public readonly struct MoveCommand
    {
        /// Unit length or zero.
        public readonly Vector2 Direction;

        /// Units per second; a negative value means use player stats.
        public readonly float SpeedOverride;

        /// Creates a command; a negative speedOverride keeps the player's own speed.
        public MoveCommand(Vector2 direction, float speedOverride = -1f)
        {
            Direction = direction;
            SpeedOverride = speedOverride;
        }

        /// No movement (zero direction, player speed).
        public static readonly MoveCommand None = new MoveCommand(Vector2.zero);
    }
}
