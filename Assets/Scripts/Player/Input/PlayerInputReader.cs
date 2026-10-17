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
        InputAction _attack1;
        InputAction _attack2;
        InputAction _skill1;
        InputAction _skill2;
        InputAction _interact;
        InputAction _flask;

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

            // Weapon slots: each slot has its own button (tap to attack, hold for a shield).
            _attack1 = _map.AddAction("Attack1", InputActionType.Button);
            _attack1.AddBinding("<Keyboard>/j");
            _attack1.AddBinding("<Mouse>/leftButton");
            _attack1.AddBinding("<Gamepad>/buttonWest");

            _attack2 = _map.AddAction("Attack2", InputActionType.Button);
            _attack2.AddBinding("<Keyboard>/k");
            _attack2.AddBinding("<Mouse>/rightButton");
            _attack2.AddBinding("<Gamepad>/buttonNorth");

            // Skills (cooldown abilities): grenades, turrets, traps.
            _skill1 = _map.AddAction("Skill1", InputActionType.Button);
            _skill1.AddBinding("<Keyboard>/u");
            _skill1.AddBinding("<Gamepad>/leftTrigger");

            _skill2 = _map.AddAction("Skill2", InputActionType.Button);
            _skill2.AddBinding("<Keyboard>/i");
            _skill2.AddBinding("<Gamepad>/rightTrigger");

            _interact = _map.AddAction("Interact", InputActionType.Button);
            _interact.AddBinding("<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/leftShoulder");

            _flask = _map.AddAction("Flask", InputActionType.Button);
            _flask.AddBinding("<Keyboard>/q");
            _flask.AddBinding("<Gamepad>/rightStickPress");
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
                RollPressed = _roll.WasPressedThisFrame(),
                Attack1Pressed = _attack1.WasPressedThisFrame(),
                Attack1Held = _attack1.IsPressed(),
                Attack2Pressed = _attack2.WasPressedThisFrame(),
                Attack2Held = _attack2.IsPressed(),
                Skill1Pressed = _skill1.WasPressedThisFrame(),
                Skill2Pressed = _skill2.WasPressedThisFrame(),
                InteractPressed = _interact.WasPressedThisFrame(),
                FlaskPressed = _flask.WasPressedThisFrame()
            };
        }
    }
}
