using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// On-screen controls for phones and tablets: a floating-style stick on the left and action buttons on the right.
    /// They drive a virtual gamepad through the Input System's on-screen controls, so the whole game (movement, combat,
    /// menus, the pause button) works unchanged and honours the player's gamepad bindings.
    /// Shown automatically on touch devices (and always or never from the settings).
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        RectTransform _layer;
        bool _visible;

        /// <summary>True when the overlay should be on for this device and these settings.</summary>
        public static bool ShouldShow()
        {
            switch (SettingsService.Current.touchControls)
            {
                case 1: return true;
                case 2: return false;
            }
            bool touchDevice = Application.isMobilePlatform || WebBridge.IsTouchDevice;
            return touchDevice && !(InputDevices.Scheme == InputScheme.Gamepad && InputDevices.HasRealGamepad);
        }

        public bool Visible => _visible;

        public static TouchControls Create(UiRoot root)
        {
            var go = new GameObject("Touch Controls");
            go.transform.SetParent(root.HudLayer, false);
            var t = go.AddComponent<TouchControls>();
            t.Build();
            return t;
        }

        void OnEnable()
        {
            SettingsService.Changed += Refresh;
            InputDevices.SchemeChanged += Refresh;
        }

        void OnDisable()
        {
            SettingsService.Changed -= Refresh;
            InputDevices.SchemeChanged -= Refresh;
            InputDevices.TouchUiActive = false;
        }

        void Start() => Refresh();

        void Refresh()
        {
            bool show = ShouldShow();
            _visible = show;
            if (_layer != null) _layer.gameObject.SetActive(show);
            InputDevices.TouchUiActive = show;
            if (show && InputDevices.Scheme != InputScheme.Touch && !InputDevices.HasRealGamepad) InputDevices.Set(InputScheme.Touch);
        }

        // ------------------------------------------------------------------ layout (1920x1080 reference)

        void Build()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(rt);
            _layer = UiKit.Node("Layer", rt);
            UiKit.Stretch(_layer);

            // stick, bottom left (above the weapon slots)
            BuildStick(new Vector2(270f, 380f), 300f);

            // face buttons, bottom right, laid out like a gamepad: attack where the thumb rests
            Button("ATTACK", "<Gamepad>/buttonWest", new Vector2(-250f, 250f), 170f, new Color(1f, 0.55f, 0.35f));
            Button("JUMP", "<Gamepad>/buttonSouth", new Vector2(-110f, 150f), 150f, new Color(0.55f, 0.85f, 1f));
            Button("ROLL", "<Gamepad>/buttonEast", new Vector2(-410f, 150f), 140f, new Color(0.7f, 1f, 0.7f));
            Button("SHIELD", "<Gamepad>/buttonNorth", new Vector2(-110f, 340f), 140f, new Color(0.8f, 0.85f, 1f));

            // skills and utilities
            Button("SKILL 1", "<Gamepad>/leftTrigger", new Vector2(-540f, 300f), 110f, new Color(1f, 0.85f, 0.4f));
            Button("SKILL 2", "<Gamepad>/rightTrigger", new Vector2(-410f, 410f), 110f, new Color(1f, 0.85f, 0.4f));
            Button("USE", "<Gamepad>/leftShoulder", new Vector2(-560f, 150f), 120f, new Color(0.9f, 0.8f, 1f));
            Button("FLASK", "<Gamepad>/rightStickPress", new Vector2(-250f, 450f), 100f, new Color(0.5f, 1f, 0.6f));

            // pause, top right
            var pause = Button("II", "<Gamepad>/start", Vector2.zero, 84f, new Color(1f, 1f, 1f), topRight: true);
            UiKit.Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -200f), new Vector2(84f, 84f));
        }

        void BuildStick(Vector2 centre, float size)
        {
            var ring = UiKit.Image(_layer, "Stick Base", new Color(1f, 1f, 1f, 0.12f), ProceduralSprites.Circle);
            UiKit.Place(ring.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), centre, new Vector2(size, size));

            var knob = UiKit.Image(ring.transform, "Stick", new Color(1f, 1f, 1f, 0.35f), ProceduralSprites.Circle);
            knob.raycastTarget = true;
            UiKit.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));

            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = size * 0.38f;
            stick.controlPath = "<Gamepad>/leftStick";
            knob.gameObject.AddComponent<TouchPressFeedback>().Bind(knob, 0.35f, 0.65f, false);
        }

        Image Button(string label, string controlPath, Vector2 fromBottomRight, float size, Color tint, bool topRight = false)
        {
            var img = UiKit.Image(_layer, "Touch " + label, new Color(tint.r, tint.g, tint.b, 0.28f), ProceduralSprites.Circle);
            img.raycastTarget = true;
            if (!topRight)
                UiKit.Place(img.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(fromBottomRight.x, fromBottomRight.y), new Vector2(size, size));

            var ring = UiKit.Image(img.transform, "Ring", new Color(tint.r, tint.g, tint.b, 0.55f), ProceduralSprites.Circle);
            ring.raycastTarget = false;
            UiKit.Stretch(ring.rectTransform, size * 0.04f);
            var inner = UiKit.Image(ring.transform, "Fill", new Color(0.05f, 0.06f, 0.1f, 0.55f), ProceduralSprites.Circle);
            UiKit.Stretch(inner.rectTransform, size * 0.035f);

            var text = UiKit.Label(img.transform, "Label", label, Mathf.RoundToInt(size * (label.Length > 5 ? 0.17f : 0.22f)), Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(text.rectTransform);

            var button = img.gameObject.AddComponent<OnScreenButton>();
            button.controlPath = controlPath;
            img.gameObject.AddComponent<TouchPressFeedback>().Bind(img, 0.28f, 0.6f, true);
            return img;
        }
    }

    /// <summary>Lights a touch control up while a finger is on it, and gives a small haptic tick.</summary>
    sealed class TouchPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        Image _image;
        float _idle, _pressed;
        bool _haptic;

        public void Bind(Image image, float idleAlpha, float pressedAlpha, bool haptic)
        {
            _image = image; _idle = idleAlpha; _pressed = pressedAlpha; _haptic = haptic;
        }

        void Set(float a)
        {
            if (_image == null) return;
            var c = _image.color; c.a = a; _image.color = c;
        }

        public void OnPointerDown(PointerEventData e)
        {
            Set(_pressed);
            if (_haptic) Haptics.Play(HapticKind.Roll);
        }

        public void OnPointerUp(PointerEventData e) => Set(_idle);
        void OnDisable() => Set(_idle);
    }
}
