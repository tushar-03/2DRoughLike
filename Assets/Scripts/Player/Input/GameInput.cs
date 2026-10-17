using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Core;

namespace Roughlike.Player
{
    /// <summary>
    /// The one shared gameplay action map, built in code so default bindings are versioned with the project.
    /// Bindings are tagged with the Keyboard&amp;Mouse and Gamepad groups so the controls screen can rebind each device
    /// separately. Overrides are stored in the settings file as JSON.
    /// </summary>
    public static class GameInput
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>Actions the player can rebind (movement keeps its WASD / arrows / stick layout).</summary>
        public static readonly string[] RebindableActions =
            { "Jump", "Roll", "Attack1", "Attack2", "Skill1", "Skill2", "Interact", "Flask" };

        static InputActionMap _map;

        public static InputActionMap Map
        {
            get
            {
                if (_map == null)
                {
                    _map = Build();
                    ApplyOverrides(SettingsService.Current.bindingOverrides);
                }
                return _map;
            }
        }

        public static InputAction Move => Map["Move"];
        public static InputAction Jump => Map["Jump"];
        public static InputAction Roll => Map["Roll"];
        public static InputAction Attack1 => Map["Attack1"];
        public static InputAction Attack2 => Map["Attack2"];
        public static InputAction Skill1 => Map["Skill1"];
        public static InputAction Skill2 => Map["Skill2"];
        public static InputAction Interact => Map["Interact"];
        public static InputAction Flask => Map["Flask"];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ResetBindingsForTests();

        /// <summary>Throws the map away so it is rebuilt from the defaults and the current settings (play-mode entry, tests).</summary>
        public static void ResetBindingsForTests()
        {
            _map?.Disable();
            _map?.Dispose();
            _map = null;
        }

        static InputActionMap Build()
        {
            var map = new InputActionMap("Gameplay");

            var move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardGroup).With("Down", "<Keyboard>/s", KeyboardGroup)
                .With("Left", "<Keyboard>/a", KeyboardGroup).With("Right", "<Keyboard>/d", KeyboardGroup);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardGroup).With("Down", "<Keyboard>/downArrow", KeyboardGroup)
                .With("Left", "<Keyboard>/leftArrow", KeyboardGroup).With("Right", "<Keyboard>/rightArrow", KeyboardGroup);
            move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/dpad/up", GamepadGroup).With("Down", "<Gamepad>/dpad/down", GamepadGroup)
                .With("Left", "<Gamepad>/dpad/left", GamepadGroup).With("Right", "<Gamepad>/dpad/right", GamepadGroup);

            Button(map, "Jump", ("<Keyboard>/space", KeyboardGroup), ("<Gamepad>/buttonSouth", GamepadGroup));
            Button(map, "Roll", ("<Keyboard>/leftShift", KeyboardGroup), ("<Gamepad>/buttonEast", GamepadGroup));
            Button(map, "Attack1", ("<Keyboard>/j", KeyboardGroup), ("<Gamepad>/buttonWest", GamepadGroup), ("<Mouse>/leftButton", KeyboardGroup));
            Button(map, "Attack2", ("<Keyboard>/k", KeyboardGroup), ("<Gamepad>/buttonNorth", GamepadGroup), ("<Mouse>/rightButton", KeyboardGroup));
            Button(map, "Skill1", ("<Keyboard>/u", KeyboardGroup), ("<Gamepad>/leftTrigger", GamepadGroup));
            Button(map, "Skill2", ("<Keyboard>/i", KeyboardGroup), ("<Gamepad>/rightTrigger", GamepadGroup));
            Button(map, "Interact", ("<Keyboard>/e", KeyboardGroup), ("<Gamepad>/leftShoulder", GamepadGroup));
            Button(map, "Flask", ("<Keyboard>/q", KeyboardGroup), ("<Gamepad>/rightStickPress", GamepadGroup));

            // secondary defaults that are not shown in the rebind list
            map["Roll"].AddBinding("<Gamepad>/rightShoulder", groups: GamepadGroup);
            map["Jump"].AddBinding("<Keyboard>/c", groups: KeyboardGroup);
            map["Roll"].AddBinding("<Keyboard>/l", groups: KeyboardGroup);
            return map;
        }

        static void Button(InputActionMap map, string name, params (string path, string group)[] bindings)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var (path, group) in bindings) a.AddBinding(path, groups: group);
        }

        // =====================================================================
        // Overrides and rebinding helpers
        // =====================================================================

        [System.Serializable]
        class OverrideEntry { public string action; public string group; public string path; }

        [System.Serializable]
        class OverrideList { public System.Collections.Generic.List<OverrideEntry> entries = new System.Collections.Generic.List<OverrideEntry>(); }

        // Overrides are stored by (action, device group, path) rather than binding id: the map is rebuilt in code every
        // session, so Input System binding ids are not stable.

        public static void ApplyOverrides(string json)
        {
            Map.RemoveAllBindingOverrides();
            if (string.IsNullOrEmpty(json)) return;

            OverrideList list;
            try { list = JsonUtility.FromJson<OverrideList>(json); }
            catch (System.Exception) { return; }
            if (list == null) return;

            foreach (var e in list.entries)
            {
                var action = Map.FindAction(e.action);
                if (action == null || string.IsNullOrEmpty(e.path)) continue;
                int i = FindBinding(action, e.group);
                if (i >= 0) action.ApplyBindingOverride(i, e.path);
            }
        }

        /// <summary>Current overrides as JSON (empty when everything is default).</summary>
        public static string ExportOverrides()
        {
            var list = new OverrideList();
            foreach (string name in RebindableActions)
            {
                var action = Map[name];
                foreach (string group in new[] { KeyboardGroup, GamepadGroup })
                {
                    int i = FindBinding(action, group);
                    if (i < 0) continue;
                    string path = action.bindings[i].overridePath;
                    if (!string.IsNullOrEmpty(path)) list.entries.Add(new OverrideEntry { action = name, group = group, path = path });
                }
            }
            return list.entries.Count == 0 ? "" : JsonUtility.ToJson(list);
        }

        public static void ResetBindings()
        {
            Map.RemoveAllBindingOverrides();
            SettingsService.Current.bindingOverrides = "";
            SettingsService.NotifyChanged();
        }

        /// <summary>Index of the first simple binding of <paramref name="action"/> in the given device group, or -1.</summary>
        public static int FindBinding(InputAction action, string group)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (b.groups != null && b.groups.Contains(group)) return i;
            }
            return -1;
        }

        public static string DisplayFor(InputAction action, string group)
        {
            int i = FindBinding(action, group);
            if (i < 0) return "-";
            string text = action.GetBindingDisplayString(i);
            return string.IsNullOrEmpty(text) ? "-" : text;
        }

        public static string Label(string actionName)
        {
            switch (actionName)
            {
                case "Jump": return "Jump";
                case "Roll": return "Roll";
                case "Attack1": return "Weapon slot 1";
                case "Attack2": return "Weapon slot 2 (shield: hold to block)";
                case "Skill1": return "Skill 1";
                case "Skill2": return "Skill 2";
                case "Interact": return "Interact";
                case "Flask": return "Health flask";
                default: return actionName;
            }
        }
    }
}

