using Survivor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor.Simulation
{
    /// WASD / arrow keys through the Input System, latched once per Update (TAC-F1-7). ESC, digits and F1 are handled elsewhere.
    public sealed class KeyboardInputSource : MonoBehaviour, IInputSource
    {
        private Vector2 _latchedDirection;

        /// Direction latched by the latest Update (unit length or zero).
        public Vector2 LatchedDirection => _latchedDirection;

        /// Returns the latched direction with the player's own speed; reads no Time.* and does not clear the latch,
        /// so every step of a frame sees the same input.
        public MoveCommand ReadMove(long step, Vector2 currentPosition)
        {
            return new MoveCommand(_latchedDirection);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _latchedDirection = Vector2.zero;
                return;
            }

            bool left = IsDown(keyboard.aKey) || IsDown(keyboard.leftArrowKey);
            bool right = IsDown(keyboard.dKey) || IsDown(keyboard.rightArrowKey);
            bool down = IsDown(keyboard.sKey) || IsDown(keyboard.downArrowKey);
            bool up = IsDown(keyboard.wKey) || IsDown(keyboard.upArrowKey);
            _latchedDirection = ComputeDirection(left, right, down, up);
        }

        // A key tapped and released within one frame still counts for that frame's latch (U17).
        private static bool IsDown(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            return key.isPressed || key.wasPressedThisFrame;
        }

        // Opposite keys cancel; diagonals are normalized so they are not faster than a single axis (SPEC F1).
        internal static Vector2 ComputeDirection(bool left, bool right, bool down, bool up)
        {
            float x = (right ? 1f : 0f) - (left ? 1f : 0f);
            float y = (up ? 1f : 0f) - (down ? 1f : 0f);
            var direction = new Vector2(x, y);
            return direction == Vector2.zero ? Vector2.zero : direction.normalized;
        }
    }
}
