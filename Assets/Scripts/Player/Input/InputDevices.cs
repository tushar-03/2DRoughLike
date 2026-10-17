using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Player
{
    public enum InputScheme { KeyboardMouse, Gamepad, Touch }

    /// <summary>
    /// Which kind of input the player is using right now (it follows whatever they touched last), and the text to show for an
    /// action on it ("E", "LB", "the JUMP button"). Prompts, the tutorial and haptics all read from here.
    /// </summary>
    public static class InputDevices
    {
        static IDisposable _subscription;

        public static InputScheme Scheme { get; private set; } = InputScheme.KeyboardMouse;

        /// <summary>Set by the on-screen controls while they are showing.</summary>
        public static bool TouchUiActive { get; set; }

        public static event Action SchemeChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _subscription?.Dispose();
            _subscription = null;
            Scheme = InputScheme.KeyboardMouse;
            TouchUiActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (_subscription == null) _subscription = InputSystem.onAnyButtonPress.Call(OnAnyButton);
            Scheme = Application.isMobilePlatform || WebBridge.IsTouchDevice ? InputScheme.Touch : InputScheme.KeyboardMouse;
        }

        static void OnAnyButton(InputControl control)
        {
            InputDevice d = control.device;
            InputScheme next;
            if (d is Touchscreen) next = InputScheme.Touch;
            else if (d is Gamepad) next = d.description.empty && TouchUiActive ? InputScheme.Touch : InputScheme.Gamepad;
            else if (d is Keyboard || d is Mouse) next = InputScheme.KeyboardMouse;
            else return;
            Set(next);
        }

        /// <summary>Forces a scheme (tests, and the touch overlay when it is shown).</summary>
        public static void Set(InputScheme scheme)
        {
            if (Scheme == scheme) return;
            Scheme = scheme;
            SchemeChanged?.Invoke();
        }

        /// <summary>True when a real (not on-screen) gamepad is plugged in.</summary>
        public static bool HasRealGamepad
        {
            get
            {
                foreach (var g in Gamepad.all) if (!g.description.empty) return true;
                return false;
            }
        }

        /// <summary>How to say "press this" for a gameplay action on the current input scheme.</summary>
        public static string Prompt(string action)
        {
            switch (Scheme)
            {
                case InputScheme.Gamepad: return GamepadName(action);
                case InputScheme.Touch: return TouchName(action);
                default: return GameInput.DisplayFor(GameInput.Map[action], GameInput.KeyboardGroup);
            }
        }

        static string GamepadName(string action)
        {
            string text = GameInput.DisplayFor(GameInput.Map[action], GameInput.GamepadGroup);
            // short, familiar names for the common controls
            switch (text)
            {
                case "Left Shoulder": return "LB";
                case "Right Shoulder": return "RB";
                case "Left Trigger": return "LT";
                case "Right Trigger": return "RT";
                case "Button South": return "A";
                case "Button East": return "B";
                case "Button West": return "X";
                case "Button North": return "Y";
                case "Right Stick Press": return "R3";
                default: return text;
            }
        }

        static string TouchName(string action)
        {
            switch (action)
            {
                case "Jump": return "JUMP";
                case "Roll": return "ROLL";
                case "Attack1": return "ATTACK";
                case "Attack2": return "SHIELD";
                case "Skill1": return "SKILL 1";
                case "Skill2": return "SKILL 2";
                case "Interact": return "USE";
                case "Flask": return "FLASK";
                default: return action.ToUpperInvariant();
            }
        }

        /// <summary>"Press E", "Press LB" or "Tap USE", depending on the device.</summary>
        public static string PressVerb => Scheme == InputScheme.Touch ? "Tap" : "Press";
        public static string BackName => Scheme == InputScheme.Gamepad ? "B" : (Scheme == InputScheme.Touch ? "Back" : "Esc");
        public static string MoveName => Scheme == InputScheme.Gamepad ? "the left stick" : (Scheme == InputScheme.Touch ? "the stick" : "A / D or the arrow keys");
    }
}
