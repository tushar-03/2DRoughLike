using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// Left/right selector for a list of options (resolution, display mode...). Left/right on a stick, D-pad or keyboard
    /// steps through the options; confirm or a click steps forward.
    /// </summary>
    public sealed class CycleButton : MonoBehaviour, IMoveHandler, ISubmitHandler, IPointerClickHandler
    {
        [NonSerialized] public IList<string> Options;
        public int Index;
        [NonSerialized] public Action<int> Changed;
        public Text Label;

        public void Refresh()
        {
            if (Label != null && Options != null && Options.Count > 0)
                Label.text = "<  " + Options[Mathf.Clamp(Index, 0, Options.Count - 1)] + "  >";
        }

        public void Step(int dir)
        {
            if (Options == null || Options.Count == 0) return;
            Index = (Index + dir + Options.Count) % Options.Count;
            Refresh();
            Changed?.Invoke(Index);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) { Step(-1); eventData.Use(); }
            else if (eventData.moveDir == MoveDirection.Right) { Step(1); eventData.Use(); }
        }

        public void OnSubmit(BaseEventData eventData) => Step(1);

        public void OnPointerClick(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 local);
            Step(local.x < rt.rect.center.x ? -1 : 1);
        }
    }

    /// <summary>Builders for menu controls. Everything navigates vertically with the stick or D-pad; left and right adjust the focused control.</summary>
    public static class MenuKit
    {
        public static readonly Vector2 RowSize = new Vector2(760f, 54f);

        public static void VerticalNav(Selectable s)
        {
            var nav = new Navigation { mode = Navigation.Mode.Vertical };
            s.navigation = nav;
        }

        /// <summary>A big list button with a title and an optional second line.</summary>
        public static Button ListButton(Transform parent, string title, string subtitle, Vector2 size, UnityAction onClick, int titleSize = 32)
        {
            var b = UiKit.Button(parent, title, size, onClick);
            VerticalNav(b);

            var t = UiKit.Label(b.transform, "Title", title, titleSize, Color.white, string.IsNullOrEmpty(subtitle) ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter, true);
            UiKit.Stretch(t.rectTransform);
            if (!string.IsNullOrEmpty(subtitle))
            {
                t.rectTransform.offsetMax = new Vector2(0f, -6f);
                var s = UiKit.Label(b.transform, "Subtitle", subtitle, 18, UiKit.Muted, TextAnchor.LowerCenter);
                UiKit.Stretch(s.rectTransform);
                s.rectTransform.offsetMin = new Vector2(10f, 6f);
                s.rectTransform.offsetMax = new Vector2(-10f, 0f);
            }
            return b;
        }

        /// <summary>A labelled settings row: returns the node the control goes into (right-aligned).</summary>
        public static RectTransform Row(Transform parent, string label, float y, out Text labelText)
        {
            var row = UiKit.Node("Row " + label, parent);
            UiKit.Place(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, RowSize.y));
            row.offsetMin = new Vector2(10f, row.offsetMin.y);
            row.offsetMax = new Vector2(-10f, row.offsetMax.y);

            var back = UiKit.Image(row, "Back", new Color(1f, 1f, 1f, 0.05f));
            UiKit.Stretch(back.rectTransform);
            labelText = UiKit.Label(row, "Label", label, 26, Color.white, TextAnchor.MiddleLeft);
            UiKit.Place(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), Vector2.zero);

            var slot = UiKit.Node("Control", row);
            UiKit.Place(slot, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), Vector2.zero);
            return slot;
        }

        public static Slider Slider(Transform parent, float value, UnityAction<float> onChanged)
        {
            var root = UiKit.Node("Slider", parent);
            UiKit.Place(root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 26f));
            root.offsetMin = new Vector2(8f, root.offsetMin.y);
            root.offsetMax = new Vector2(-80f, root.offsetMax.y);

            var bg = UiKit.Image(root, "Background", new Color(0.04f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;
            UiKit.Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 12f));

            var fillArea = UiKit.Node("Fill Area", root);
            UiKit.Place(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, 8f));
            var fill = UiKit.Image(fillArea, "Fill", UiKit.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = UiKit.Node("Handle Slide Area", root);
            UiKit.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = UiKit.Image(handleArea, "Handle", Color.white, ProceduralSprites.Circle);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(26f, 26f);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.value = value;
            var colours = s.colors;
            colours.highlightedColor = new Color(1.4f, 1.3f, 1f);
            colours.selectedColor = new Color(1.7f, 1.5f, 1f);
            s.colors = colours;
            VerticalNav(s);

            var pct = UiKit.Label(root, "Percent", Mathf.RoundToInt(value * 100f) + "%", 24, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Place(pct.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(70f, 0f));

            s.onValueChanged.AddListener(v =>
            {
                pct.text = Mathf.RoundToInt(v * 100f) + "%";
                onChanged?.Invoke(v);
            });
            return s;
        }

        public static Toggle Toggle(Transform parent, bool value, UnityAction<bool> onChanged)
        {
            var root = UiKit.Node("Toggle", parent);
            UiKit.Place(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(46f, 36f));

            var bg = UiKit.Image(root, "Background", new Color(0.04f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform);
            var check = UiKit.Image(bg.transform, "Check", UiKit.Gold);
            UiKit.Stretch(check.rectTransform, 7f);

            var t = root.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = check;
            t.isOn = value;
            var colours = t.colors;
            colours.highlightedColor = new Color(1.5f, 1.4f, 1f);
            colours.selectedColor = new Color(1.8f, 1.6f, 1f);
            t.colors = colours;
            VerticalNav(t);
            if (onChanged != null) t.onValueChanged.AddListener(onChanged);
            return t;
        }

        public static CycleButton Cycle(Transform parent, IList<string> options, int index, Action<int> onChanged)
        {
            var b = UiKit.Button(parent, "Cycle", new Vector2(0f, 0f), null);
            var rt = (RectTransform)b.transform;
            UiKit.Place(rt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 40f));
            rt.offsetMin = new Vector2(4f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-4f, rt.offsetMax.y);
            VerticalNav(b);
            b.GetComponent<Image>().raycastTarget = true;

            var label = UiKit.Label(b.transform, "Value", "", 24, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(label.rectTransform);

            var c = b.gameObject.AddComponent<CycleButton>();
            c.Options = options;
            c.Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            c.Changed = onChanged;
            c.Label = label;
            c.Refresh();
            return c;
        }

        /// <summary>A small action button (Back, Reset...).</summary>
        public static Button Action(Transform parent, string text, Vector2 size, UnityAction onClick)
        {
            var b = ListButton(parent, text, null, size, onClick, 24);
            return b;
        }
    }
}

