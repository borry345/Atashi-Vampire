using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor.UI
{
    /// Keyboard shortcuts that are not EventSystem navigation (TAC-F8-12): ESC raises EscapePressed; 1/2/3 (and keypad) choose a
    /// level-up option through LevelUpView.Choose while that view is visible. Reads Keyboard.current once per Update; null
    /// (batch mode, no keyboard) means no input.
    public sealed class UiInputHandler : MonoBehaviour
    {
        private LevelUpView _levelUp;

        /// ESC was pressed this frame.
        public event Action EscapePressed;

        /// Binds the level-up view the digit keys choose on; null throws ArgumentNullException.
        public void Initialize(LevelUpView levelUp)
        {
            _levelUp = levelUp ? levelUp : throw new ArgumentNullException(nameof(levelUp));
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                EscapePressed?.Invoke();
                return;
            }

            if (_levelUp == null || !_levelUp.IsVisible)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                _levelUp.Choose(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                _levelUp.Choose(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                _levelUp.Choose(2);
            }
        }
    }
}
