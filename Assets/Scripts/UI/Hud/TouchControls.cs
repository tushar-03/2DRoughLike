using System.Collections.Generic;
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
    /// <summary>Where one on-screen control sits by default and what it looks like (1920x1080 reference units).</summary>
    public sealed class TouchSpec
    {
        public string Id, Label, Path;
        public Vector2 Anchor, Pivot, Position;
        public float Size;
        public Color Tint;
        public bool IsStick;
    }

    /// <summary>
    /// On-screen controls for phones and tablets: a floating-style stick on the left and action buttons on the right.
    /// They drive a virtual gamepad through the Input System's on-screen controls, so the whole game (movement, combat,
    /// menus, the pause button) works unchanged and honours the player's gamepad bindings.
    /// Shown automatically on touch devices (and always or never from the settings). Every control can be moved and resized
    /// in the settings (<see cref="TouchLayoutEditor"/>); the layout is saved with the other settings.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        static readonly Vector2 BR = new Vector2(1f, 0f), Centre = new Vector2(0.5f, 0.5f);

        /// <summary>Every control, in draw order.</summary>
        public static readonly TouchSpec[] Specs =
        {
            new TouchSpec { Id = "stick", Label = "MOVE", IsStick = true, Anchor = Vector2.zero, Pivot = Centre, Position = new Vector2(270f, 380f), Size = 300f, Tint = Color.white },
            new TouchSpec { Id = "attack", Label = "ATTACK", Path = "<Gamepad>/buttonWest", Anchor = BR, Pivot = Centre, Position = new Vector2(-250f, 250f), Size = 170f, Tint = new Color(1f, 0.55f, 0.35f) },
            new TouchSpec { Id = "jump", Label = "JUMP", Path = "<Gamepad>/buttonSouth", Anchor = BR, Pivot = Centre, Position = new Vector2(-110f, 150f), Size = 150f, Tint = new Color(0.55f, 0.85f, 1f) },
            new TouchSpec { Id = "roll", Label = "ROLL", Path = "<Gamepad>/buttonEast", Anchor = BR, Pivot = Centre, Position = new Vector2(-410f, 150f), Size = 140f, Tint = new Color(0.7f, 1f, 0.7f) },
            new TouchSpec { Id = "shield", Label = "SHIELD", Path = "<Gamepad>/buttonNorth", Anchor = BR, Pivot = Centre, Position = new Vector2(-110f, 340f), Size = 140f, Tint = new Color(0.8f, 0.85f, 1f) },
            new TouchSpec { Id = "skill1", Label = "SKILL 1", Path = "<Gamepad>/leftTrigger", Anchor = BR, Pivot = Centre, Position = new Vector2(-540f, 300f), Size = 110f, Tint = new Color(1f, 0.85f, 0.4f) },
            new TouchSpec { Id = "skill2", Label = "SKILL 2", Path = "<Gamepad>/rightTrigger", Anchor = BR, Pivot = Centre, Position = new Vector2(-410f, 410f), Size = 110f, Tint = new Color(1f, 0.85f, 0.4f) },
            new TouchSpec { Id = "use", Label = "USE", Path = "<Gamepad>/leftShoulder", Anchor = BR, Pivot = Centre, Position = new Vector2(-560f, 150f), Size = 120f, Tint = new Color(0.9f, 0.8f, 1f) },
            new TouchSpec { Id = "flask", Label = "FLASK", Path = "<Gamepad>/rightStickPress", Anchor = BR, Pivot = Centre, Position = new Vector2(-250f, 450f), Size = 100f, Tint = new Color(0.5f, 1f, 0.6f) },
            new TouchSpec { Id = "pause", Label = "II", Path = "<Gamepad>/start", Anchor = Vector2.one, Pivot = Vector2.one, Position = new Vector2(-24f, -200f), Size = 84f, Tint = Color.white }
        };

        RectTransform _layer;
        CanvasGroup _group;
        readonly Dictionary<string, RectTransform> _controls = new Dictionary<string, RectTransform>();
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
        public RectTransform Control(string id) => _controls.TryGetValue(id, out var r) ? r : null;

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
            ApplyLayout();
        }

        // ------------------------------------------------------------------ layout (1920x1080 reference)

        void Build()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(rt);
            _layer = UiKit.Node("Layer", rt);
            UiKit.Stretch(_layer);
            _group = _layer.gameObject.AddComponent<CanvasGroup>();

            foreach (var spec in Specs) _controls[spec.Id] = BuildVisual(_layer, spec, true);
            ApplyLayout();
        }

        /// <summary>Puts each control at its saved (or default) place, size and opacity.</summary>
        public void ApplyLayout()
        {
            var s = SettingsService.Current;
            if (_group != null) _group.alpha = s.touchOpacity;
            foreach (var spec in Specs)
            {
                if (!_controls.TryGetValue(spec.Id, out var rt) || rt == null) continue;
                Place(rt, spec, s);
            }
        }

        /// <summary>Position, scale and size factor of one control from the settings.</summary>
        public static void Place(RectTransform rt, TouchSpec spec, GameSettings s)
        {
            var item = s.FindTouch(spec.Id);
            Vector2 pos = item != null ? new Vector2(item.x, item.y) : spec.Position;
            float scale = (item != null ? item.scale : 1f) * s.touchScale;
            rt.anchorMin = spec.Anchor;
            rt.anchorMax = spec.Anchor;
            rt.pivot = spec.Pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(spec.Size, spec.Size);
            rt.localScale = Vector3.one * scale;
        }

        /// <summary>Draws one control. <paramref name="interactive"/> wires it to the virtual gamepad; the layout editor draws inert copies.</summary>
        public static RectTransform BuildVisual(Transform parent, TouchSpec spec, bool interactive)
        {
            return spec.IsStick ? BuildStick(parent, spec, interactive) : BuildButton(parent, spec, interactive);
        }

        static RectTransform BuildStick(Transform parent, TouchSpec spec, bool interactive)
        {
            float size = spec.Size;
            var ring = UiKit.Image(parent, "Stick Base", new Color(1f, 1f, 1f, 0.12f), ProceduralSprites.Circle);
            ring.raycastTarget = !interactive;   // the editor drags the base; in play only the knob takes touches

            var knob = UiKit.Image(ring.transform, "Stick", new Color(1f, 1f, 1f, 0.35f), ProceduralSprites.Circle);
            knob.raycastTarget = interactive;
            UiKit.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));

            if (interactive)
            {
                var stick = knob.gameObject.AddComponent<OnScreenStick>();
                stick.movementRange = size * 0.38f;
                stick.controlPath = "<Gamepad>/leftStick";
                knob.gameObject.AddComponent<TouchPressFeedback>().Bind(knob, 0.35f, 0.65f, false);
            }
            else
            {
                var text = UiKit.Label(ring.transform, "Label", spec.Label, 34, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Stretch(text.rectTransform);
            }
            return ring.rectTransform;
        }

        static RectTransform BuildButton(Transform parent, TouchSpec spec, bool interactive)
        {
            Color tint = spec.Tint;
            float size = spec.Size;
            var img = UiKit.Image(parent, "Touch " + spec.Label, new Color(tint.r, tint.g, tint.b, 0.28f), ProceduralSprites.Circle);
            img.raycastTarget = true;

            var ring = UiKit.Image(img.transform, "Ring", new Color(tint.r, tint.g, tint.b, 0.55f), ProceduralSprites.Circle);
            ring.raycastTarget = false;
            UiKit.Stretch(ring.rectTransform, size * 0.04f);
            var inner = UiKit.Image(ring.transform, "Fill", new Color(0.05f, 0.06f, 0.1f, 0.55f), ProceduralSprites.Circle);
            UiKit.Stretch(inner.rectTransform, size * 0.035f);

            var text = UiKit.Label(img.transform, "Label", spec.Label, Mathf.RoundToInt(size * (spec.Label.Length > 5 ? 0.17f : 0.22f)), Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(text.rectTransform);

            if (interactive)
            {
                var button = img.gameObject.AddComponent<OnScreenButton>();
                button.controlPath = spec.Path;
                img.gameObject.AddComponent<TouchPressFeedback>().Bind(img, 0.28f, 0.6f, true);
            }
            return img.rectTransform;
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
