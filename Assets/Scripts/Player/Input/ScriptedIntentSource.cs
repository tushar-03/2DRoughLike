using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Intent source driven from code. Used by automated tests and debug tooling.
    /// Held values persist; pressed flags are one-shot and cleared when read.
    /// </summary>
    public class ScriptedIntentSource : MonoBehaviour, IIntentSource
    {
        public Vector2 Move;
        public bool JumpHeld;

        bool _jumpPressed;
        bool _rollPressed;

        public void PressJump() { _jumpPressed = true; JumpHeld = true; }
        public void ReleaseJump() { JumpHeld = false; }
        public void PressRoll() { _rollPressed = true; }

        public PlayerIntent Read()
        {
            var intent = new PlayerIntent
            {
                Move = Move,
                JumpHeld = JumpHeld,
                JumpPressed = _jumpPressed,
                RollPressed = _rollPressed
            };
            _jumpPressed = false;
            _rollPressed = false;
            return intent;
        }
    }
}
