using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.UI
{
    /// <summary>
    /// The controls screen: every action with its keyboard and gamepad binding, and buttons to rebind them. Rebinding waits
    /// for the next key or button (Esc cancels). Overrides are saved in the settings file and shared with gameplay.
    /// </summary>
    public sealed class ControlsView
    {
        /// <summary>True while waiting for a key press, so Esc cancels the rebind instead of closing the screen.</summary>
        public static bool IsRebinding { get; private set; }

        sealed class BindButton
        {
            public InputAction Action;
            public string Group;
            public Text Label;
        }

        readonly List<BindButton> _buttons = new List<BindButton>();
        readonly Text _status;
        InputActionRebindingExtensions.RebindingOperation _operation;

        public Selectable FirstSelectable { get; private set; }

        public static ControlsView Build(RectTransform window, System.Action back) => new ControlsView(window, back);

        ControlsView(RectTransform window, System.Action back)
        {
            // headings
            var heading = UiKit.Node("Headings", window);
            UiKit.Place(heading, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(0f, 36f));
            heading.offsetMin = new Vector2(40f, heading.offsetMin.y);
            heading.offsetMax = new Vector2(-40f, heading.offsetMax.y);
            Heading(heading, "Action", 0.02f, 0.45f, TextAnchor.MiddleLeft);
            Heading(heading, "Keyboard / mouse", 0.45f, 0.72f, TextAnchor.MiddleCenter);
            Heading(heading, "Gamepad", 0.72f, 0.98f, TextAnchor.MiddleCenter);

            var list = UiKit.Node("Rows", window);
            UiKit.Place(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            list.offsetMin = new Vector2(40f, 110f);
            list.offsetMax = new Vector2(-40f, -108f);

            float y = 0f;
            // movement is shown for reference (it keeps its WASD / arrows / stick layout)
            FixedRow(list, "Move", "W A S D  /  Arrow keys", "Left stick  /  D-pad", y);
            y -= 52f;
            foreach (string name in GameInput.RebindableActions)
            {
                var action = GameInput.Map[name];
                var row = UiKit.Node("Row " + name, list);
                UiKit.Place(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, 46f));
                var back2 = UiKit.Image(row, "Back", new Color(1f, 1f, 1f, 0.05f));
                UiKit.Stretch(back2.rectTransform);
                var label = UiKit.Label(row, "Label", GameInput.Label(name), 24, Color.white, TextAnchor.MiddleLeft);
                UiKit.Place(label.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

                AddBindButton(row, action, GameInput.KeyboardGroup, 0.46f, 0.71f);
                AddBindButton(row, action, GameInput.GamepadGroup, 0.73f, 0.98f);
                y -= 50f;
            }

            _status = UiKit.Label(window, "Status", "Select a binding and press confirm, then press the new key or button.", 20, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(-60f, 28f));

            var reset = MenuKit.Action(window, "Reset to defaults", new Vector2(300f, 52f), ResetAll);
            UiKit.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(300f, 52f));
            var done = MenuKit.Action(window, "Back  (Esc / B)", new Vector2(300f, 52f), () => back());
            UiKit.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(300f, 52f));

            FirstSelectable = _buttons.Count > 0 ? _buttons[0].Label.GetComponentInParent<Button>() : done;
        }

        static void Heading(Transform parent, string text, float x0, float x1, TextAnchor anchor)
        {
            var t = UiKit.Label(parent, text, text, 22, UiKit.Gold, anchor, true);
            UiKit.Place(t.rectTransform, new Vector2(x0, 0f), new Vector2(x1, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        static void FixedRow(Transform list, string action, string keyboard, string pad, float y)
        {
            var row = UiKit.Node("Row Move", list);
            UiKit.Place(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, 46f));
            var back = UiKit.Image(row, "Back", new Color(1f, 1f, 1f, 0.03f));
            UiKit.Stretch(back.rectTransform);
            var l = UiKit.Label(row, "Label", action, 24, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(l.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var k = UiKit.Label(row, "Keyboard", keyboard, 22, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(k.rectTransform, new Vector2(0.46f, 0f), new Vector2(0.71f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var g = UiKit.Label(row, "Pad", pad, 22, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(g.rectTransform, new Vector2(0.73f, 0f), new Vector2(0.98f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        void AddBindButton(Transform row, InputAction action, string group, float x0, float x1)
        {
            var bb = new BindButton { Action = action, Group = group };
            var b = UiKit.Button(row, "Bind " + group, Vector2.zero, () => Rebind(bb));
            MenuKit.VerticalNav(b);
            b.GetComponent<Image>().raycastTarget = true;
            UiKit.Place((RectTransform)b.transform, new Vector2(x0, 0.08f), new Vector2(x1, 0.92f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bb.Label = UiKit.Label(b.transform, "Text", "", 24, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(bb.Label.rectTransform);
            _buttons.Add(bb);
            Refresh(bb);
        }

        void Refresh(BindButton b) => b.Label.text = GameInput.DisplayFor(b.Action, b.Group);

        void RefreshAll() { foreach (var b in _buttons) Refresh(b); }

        void Rebind(BindButton target)
        {
            if (IsRebinding) return;
            int index = GameInput.FindBinding(target.Action, target.Group);
            if (index < 0) return;

            IsRebinding = true;
            target.Label.text = "...";
            bool keyboard = target.Group == GameInput.KeyboardGroup;
            _status.text = keyboard ? "Press a key or mouse button...  (Esc cancels)" : "Press a gamepad button...  (Esc cancels)";

            GameInput.Map.Disable();   // an action cannot be rebound while enabled
            var op = target.Action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .OnMatchWaitForAnother(0.1f);

            if (keyboard) op = op.WithControlsHavingToMatchPath("<Keyboard>/*").WithControlsHavingToMatchPath("<Mouse>/*");
            else op = op.WithControlsHavingToMatchPath("<Gamepad>/*");

            op.OnComplete(o => Finish(o, target, true));
            op.OnCancel(o => Finish(o, target, false));
            _operation = op;
            op.Start();
        }

        void Finish(InputActionRebindingExtensions.RebindingOperation op, BindButton target, bool saved)
        {
            op.Dispose();
            _operation = null;
            IsRebinding = false;
            GameInput.Map.Enable();

            if (saved)
            {
                SettingsService.Current.bindingOverrides = GameInput.ExportOverrides();
                SettingsService.NotifyChanged();
                _status.text = GameInput.Label(target.Action.name) + " is now " + GameInput.DisplayFor(target.Action, target.Group);
            }
            else _status.text = "Rebind cancelled.";
            RefreshAll();
        }

        void ResetAll()
        {
            GameInput.ResetBindings();
            _status.text = "Controls reset to defaults.";
            RefreshAll();
        }

        /// <summary>Stops a rebind in progress (the screen is closing).</summary>
        public void Cancel()
        {
            if (_operation == null) return;
            _operation.Cancel();
        }
    }
}

