using UnityEngine;
using UnityEngine.InputSystem;

namespace Roughlike.Player
{
    /// <summary>
    /// Keyboard + gamepad intent source. The action map is built in code so default bindings are
    /// versioned with the project. Rebinding (M10) works through <see cref="Actions"/> binding overrides.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour, IIntentSource
    {
        InputActionMap _map;
        InputAction _move;
        InputAction _jump;
        InputAction _roll;

        public InputActionMap Actions => _map;

        void Awake()
        {
            _map = new InputActionMap("Gameplay");

            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/dpad/up").With("Down", "<Gamepad>/dpad/down")
                .With("Left", "<Gamepad>/dpad/left").With("Right", "<Gamepad>/dpad/right");

            _jump = _map.AddAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");
            _jump.AddBinding("<Keyboard>/c");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _roll = _map.AddAction("Roll", InputActionType.Button);
            _roll.AddBinding("<Keyboard>/leftShift");
            _roll.AddBinding("<Keyboard>/l");
            _roll.AddBinding("<Gamepad>/buttonEast");
            _roll.AddBinding("<Gamepad>/rightShoulder");
        }

        void OnEnable() => _map?.Enable();
        void OnDisable() => _map?.Disable();
        void OnDestroy() => _map?.Dispose();

        public PlayerIntent Read()
        {
            return new PlayerIntent
            {
                Move = _move.ReadValue<Vector2>(),
                JumpPressed = _jump.WasPressedThisFrame(),
                JumpHeld = _jump.IsPressed(),
                RollPressed = _roll.WasPressedThisFrame()
            };
        }
    }
}
