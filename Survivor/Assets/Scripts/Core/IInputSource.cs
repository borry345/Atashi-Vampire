using UnityEngine;

namespace Survivor.Core
{
    /// Supplies movement intent for one step; implementations must not read Time.*.
    public interface IInputSource
    {
        /// Returns the movement for the given step from the player's current position.
        MoveCommand ReadMove(long step, Vector2 currentPosition);
    }
}
