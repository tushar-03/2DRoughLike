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
        bool _attack1Pressed, _attack2Pressed;
        public bool Attack1Held, Attack2Held;

        public void PressJump() { _jumpPressed = true; JumpHeld = true; }
        public void ReleaseJump() { JumpHeld = false; }
        public void PressRoll() { _rollPressed = true; }
        public void PressAttack(int slot) { if (slot == 0) { _attack1Pressed = true; Attack1Held = true; } else { _attack2Pressed = true; Attack2Held = true; } }
        public void ReleaseAttack(int slot) { if (slot == 0) Attack1Held = false; else Attack2Held = false; }

        public PlayerIntent Read()
        {
            var intent = new PlayerIntent
            {
                Move = Move,
                JumpHeld = JumpHeld,
                JumpPressed = _jumpPressed,
                RollPressed = _rollPressed,
                Attack1Pressed = _attack1Pressed,
                Attack1Held = Attack1Held,
                Attack2Pressed = _attack2Pressed,
                Attack2Held = Attack2Held
            };
            _jumpPressed = false;
            _rollPressed = false;
            _attack1Pressed = false;
            _attack2Pressed = false;
            return intent;
        }
    }
}

