using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Keyboard + gamepad intent source. It reads the shared <see cref="GameInput"/> action map (so rebinding in the
    /// controls screen takes effect everywhere) and turns it into a device-independent <see cref="PlayerIntent"/>.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour, IIntentSource
    {
        void OnEnable() => GameInput.Map.Enable();
        void OnDisable() => GameInput.Map.Disable();

        public PlayerIntent Read()
        {
            // the press that closed a menu (A to confirm, B to cancel) must not also jump or roll
            if (Roughlike.Core.InputGate.Blocked) return default;
            return new PlayerIntent
            {
                Move = GameInput.Move.ReadValue<Vector2>(),
                JumpPressed = GameInput.Jump.WasPressedThisFrame(),
                JumpHeld = GameInput.Jump.IsPressed(),
                RollPressed = GameInput.Roll.WasPressedThisFrame(),
                Attack1Pressed = GameInput.Attack1.WasPressedThisFrame(),
                Attack1Held = GameInput.Attack1.IsPressed(),
                Attack2Pressed = GameInput.Attack2.WasPressedThisFrame(),
                Attack2Held = GameInput.Attack2.IsPressed(),
                Skill1Pressed = GameInput.Skill1.WasPressedThisFrame(),
                Skill2Pressed = GameInput.Skill2.WasPressedThisFrame(),
                InteractPressed = GameInput.Interact.WasPressedThisFrame(),
                FlaskPressed = GameInput.Flask.WasPressedThisFrame()
            };
        }
    }
}
