using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roughlike.Core;

namespace Roughlike.UI
{
    /// <summary>
    /// Full-screen editor for the on-screen touch controls: drag a control to move it, tap it to select it and use the size
    /// slider, set the opacity of all of them, reset to the default layout, then press Done. Works with a finger or a mouse.
    /// The copies drawn here use the same placement code as the real controls, so what you see is where they will be.
    /// </summary>
    public sealed class TouchLayoutEditor : MonoBehaviour
    {
        readonly Dictionary<string, RectTransform> _copies = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Image> _frames = new Dictionary<string, Image>();
        string _selected;
        Slider _size, _opacity;
        Text _selectedLabel;
        Action _closed;
        bool _building;
        CanvasGroup _group;
        readonly List<CanvasGroup> _frozen = new List<CanvasGroup>();

        public string Selected => _selected;
        public RectTransform Copy(string id) => _copies.TryGetValue(id, out var r) ? r : null;

        public static TouchLayoutEditor Open(UiRoot root, Action closed)
        {
            var go = new GameObject("Touch Layout Editor", typeof(RectTransform));
            go.transform.SetParent(root.ModalLayer, false);
            var e = go.AddComponent<TouchLayoutEditor>();
            e._closed = closed;
            e.Build();
            return e;
        }

        void Build()
        {
            _building = true;
            var rt = (RectTransform)transform;
            // everything underneath (the settings window) stops taking navigation while this editor is open
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;
                if (!sibling.TryGetComponent<CanvasGroup>(out var cg)) cg = sibling.gameObject.AddComponent<CanvasGroup>();
                if (!cg.interactable) continue;
                cg.interactable = false;
                _frozen.Add(cg);
            }
            UiKit.Stretch(rt);

            var dim = UiKit.Image(rt, "Dim", new Color(0.02f, 0.03f, 0.06f, 0.94f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);

            // the playable area, so it is clear where the controls will sit
            var frame = UiKit.Image(rt, "Screen Edge", new Color(1f, 1f, 1f, 0.07f));
            frame.raycastTarget = false;
            UiKit.Stretch(frame.rectTransform, 14f);

            var title = UiKit.Label(rt, "Title", "TOUCH CONTROLS", 44, new Color(1f, 0.88f, 0.55f), TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1200f, 64f));
            var help = UiKit.Label(rt, "Help", "Drag a button to move it. Tap one to select it, then change its size below.", 28, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(help.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1500f, 44f));

            _group = rt.gameObject.AddComponent<CanvasGroup>();

            // controls
            var layer = UiKit.Node("Controls", rt);
            UiKit.Stretch(layer);
            foreach (var spec in TouchControls.Specs)
            {
                var copy = TouchControls.BuildVisual(layer, spec, false);
                _copies[spec.Id] = copy;

                var sel = UiKit.Image(copy, "Selected", new Color(1f, 0.9f, 0.4f, 0f), Roughlike.Utils.ProceduralSprites.Circle);
                sel.raycastTarget = false;
                UiKit.Stretch(sel.rectTransform, -10f);
                sel.transform.SetAsFirstSibling();
                _frames[spec.Id] = sel;

                var handle = copy.gameObject.AddComponent<DragHandle>();
                handle.Bind(this, spec);
                TouchControls.Place(copy, spec, SettingsService.Current);
            }

            // panel with the size and opacity sliders
            var panel = UiKit.Node("Panel", rt);
            UiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1100f, 220f));
            var back = UiKit.Image(panel, "Back", new Color(0.06f, 0.08f, 0.12f, 0.92f));
            back.raycastTarget = true;
            UiKit.Stretch(back.rectTransform);

            _selectedLabel = UiKit.Label(panel, "Selected", "", 28, UiKit.Gold, TextAnchor.MiddleLeft, true);
            UiKit.Place(_selectedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(520f, 44f));

            var sizeRow = MenuKit.Row(panel, "Button size", -64f, out _);
            _size = MenuKit.Slider(sizeRow, 0.5f, v => OnSize(v));
            var opacityRow = MenuKit.Row(panel, "Opacity (all)", -124f, out _);
            _opacity = MenuKit.Slider(opacityRow, Mathf.InverseLerp(0.25f, 1f, SettingsService.Current.touchOpacity), v =>
            {
                if (_building) return;
                SettingsService.Current.touchOpacity = Mathf.Lerp(0.25f, 1f, v);
                SettingsService.NotifyChanged();
                RefreshPreviewAlpha();
            });

            var reset = MenuKit.Action(panel, "Reset layout", new Vector2(260f, 50f), ResetLayout);
            UiKit.Place((RectTransform)reset.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -12f), new Vector2(260f, 50f));
            var done = MenuKit.Action(panel, "Done", new Vector2(250f, 50f), Close);
            UiKit.Place((RectTransform)done.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(250f, 50f));

            MenuNav.AutoLink(panel);
            Select(TouchControls.Specs[1].Id);
            RefreshPreviewAlpha();
            _building = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(done.gameObject);
        }

        void RefreshPreviewAlpha()
        {
            // the dim stays solid; only the control copies take the chosen opacity so the player sees the real result
            foreach (var kv in _copies)
            {
                if (!kv.Value.TryGetComponent<CanvasGroup>(out var g)) g = kv.Value.gameObject.AddComponent<CanvasGroup>();
                g.alpha = Mathf.Max(0.35f, SettingsService.Current.touchOpacity);
            }
        }

        public void Select(string id)
        {
            _selected = id;
            foreach (var kv in _frames)
            {
                var c = kv.Value.color;
                c.a = kv.Key == id ? 0.55f : 0f;
                kv.Value.color = c;
            }
            TouchSpec spec = Spec(id);
            var item = SettingsService.Current.FindTouch(id);
            float scale = item != null ? item.scale : 1f;
            bool was = _building;
            _building = true;
            if (_size != null) _size.value = Mathf.InverseLerp(0.5f, 2f, scale);
            _building = was;
            if (_selectedLabel != null && spec != null) _selectedLabel.text = "Selected: " + spec.Label;
        }

        static TouchSpec Spec(string id)
        {
            foreach (var s in TouchControls.Specs) if (s.Id == id) return s;
            return null;
        }

        void OnSize(float v)
        {
            if (_building || _selected == null) return;
            TouchSpec spec = Spec(_selected);
            var s = SettingsService.Current;
            var item = s.GetOrAddTouch(spec.Id, spec.Position.x, spec.Position.y);
            item.scale = Mathf.Lerp(0.5f, 2f, v);
            TouchControls.Place(_copies[spec.Id], spec, s);
            SettingsService.NotifyChanged();
        }

        /// <summary>Called while a control is dragged: <paramref name="delta"/> is in canvas units.</summary>
        public void Moved(TouchSpec spec, Vector2 delta)
        {
            var s = SettingsService.Current;
            var item = s.GetOrAddTouch(spec.Id, spec.Position.x, spec.Position.y);
            // keep it on screen: anchored positions are relative to the control's anchor corner
            float w = 1920f, h = 1080f;
            Vector2 p = new Vector2(item.x, item.y) + delta;
            float r = spec.Size * item.scale * s.touchScale * 0.5f;
            float minX = spec.Anchor.x < 0.5f ? r : -w + r, maxX = spec.Anchor.x < 0.5f ? w - r : -r;
            float minY = spec.Anchor.y < 0.5f ? r : -h + r, maxY = spec.Anchor.y < 0.5f ? h - r : -r;
            item.x = Mathf.Clamp(p.x, minX, maxX);
            item.y = Mathf.Clamp(p.y, minY, maxY);
            TouchControls.Place(_copies[spec.Id], spec, s);
            SettingsService.NotifyChanged();
        }

        public void ResetLayout()
        {
            var s = SettingsService.Current;
            s.touchLayout = new TouchItem[0];
            s.touchScale = 1f;
            s.touchOpacity = 1f;
            foreach (var spec in TouchControls.Specs) TouchControls.Place(_copies[spec.Id], spec, s);
            _building = true;
            _opacity.value = 1f;
            _building = false;
            Select(_selected ?? TouchControls.Specs[1].Id);
            RefreshPreviewAlpha();
            SettingsService.NotifyChanged();
        }

        void OnDestroy() => Unfreeze();

        void Unfreeze()
        {
            foreach (var cg in _frozen) if (cg != null) cg.interactable = true;
            _frozen.Clear();
        }

        public void Close()
        {
            Unfreeze();
            Destroy(gameObject);
            _closed?.Invoke();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) Close();
        }

        sealed class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
        {
            TouchLayoutEditor _editor;
            TouchSpec _spec;
            Canvas _canvas;

            public void Bind(TouchLayoutEditor editor, TouchSpec spec) { _editor = editor; _spec = spec; }

            Canvas Cv => _canvas != null ? _canvas : (_canvas = GetComponentInParent<Canvas>().rootCanvas);

            public void OnBeginDrag(PointerEventData e) => _editor.Select(_spec.Id);
            public void OnPointerClick(PointerEventData e) => _editor.Select(_spec.Id);

            public void OnDrag(PointerEventData e)
            {
                // screen pixels to canvas reference units
                float k = Cv.scaleFactor <= 0f ? 1f : Cv.scaleFactor;
                _editor.Moved(_spec, e.delta / k);
            }
        }
    }
}
