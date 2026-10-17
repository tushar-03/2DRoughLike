using UnityEngine;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// Small helpers for building uGUI in code (the whole UI is generated, so layouts live next to their logic and
    /// there are no scene assets to keep in sync). Text uses the built-in font until the art pass brings real fonts.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColour = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        public static readonly Color PanelEdge = new Color(0.30f, 0.34f, 0.44f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        public static readonly Color Muted = new Color(0.72f, 0.74f, 0.80f);
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.3f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = LoadFont());

        static Font LoadFont()
        {
            // a classical serif if the machine has one, otherwise the built-in font
            Font os = null;
            try { os = Font.CreateDynamicFontFromOSFont(new[] { "Palatino Linotype", "Book Antiqua", "Georgia", "Cambria", "Times New Roman" }, 32); }
            catch (System.Exception) { }
            return os != null ? os : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static Image Image(Transform parent, string name, Color colour, Sprite sprite = null)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : ProceduralSprites.Square;
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.alignment = anchor;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A bordered panel: returns the inner content node.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size, Color? fill = null)
        {
            var edge = Image(parent, name, Color.white, UiSkin.Frame);
            edge.type = UnityEngine.UI.Image.Type.Sliced;
            ((RectTransform)edge.transform).sizeDelta = size;
            var inner = Image(edge.transform, "Fill", fill ?? PanelColour, UiSkin.Fill);
            inner.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch((RectTransform)inner.transform, 9f);
            return (RectTransform)inner.transform;
        }

        /// <summary>Selectable button with keyboard/controller highlight. The visible content is added by the caller.</summary>
        public static Button Button(Transform parent, string name, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Node(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSkin.ButtonFill;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = new Color(0.14f, 0.16f, 0.22f, 1f);
            var rim = Image(rt, "Rim", Color.white, UiSkin.ButtonRim);
            rim.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch((RectTransform)rim.transform);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colours = b.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.5f, 1.4f, 1f);
            colours.selectedColor = new Color(1.7f, 1.55f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colours.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            colours.colorMultiplier = 1f;
            b.colors = colours;
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        /// <summary>Draws a small placeholder icon for an item inside <paramref name="img"/>.</summary>
        public static void FillIcon(Image img, ItemInstance item)
        {
            if (item == null) { img.color = new Color(1f, 1f, 1f, 0.08f); img.sprite = ProceduralSprites.Square; return; }
            switch (item.Kind)
            {
                case ItemKind.Skill:
                    img.sprite = ProceduralSprites.Circle;
                    img.color = item.Skill != null ? item.Skill.tint : Color.white;
                    break;
                case ItemKind.Amulet:
                    img.sprite = ProceduralSprites.Spark;
                    img.color = item.Gear != null ? item.Gear.tint : Color.white;
                    break;
                case ItemKind.Outfit:
                    img.sprite = ProceduralSprites.Square;
                    img.color = item.Gear != null ? item.Gear.tint : Color.white;
                    break;
                default:
                    img.sprite = ProceduralSprites.Square;
                    img.color = item.Weapon != null ? item.Weapon.tint : Color.white;
                    break;
            }
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Freezes the game while a menu is open. Counted, so stacked menus unpause only when the last closes.</summary>
    public static class PauseGate
    {
        static int _count;

        public static bool Paused => _count > 0;

        public static void Push()
        {
            if (_count++ == 0)
            {
                HitStop.BaseTimeScale = 0f;
                Time.timeScale = 0f;
            }
        }

        public static void Pop()
        {
            if (_count == 0) return;
            if (--_count == 0)
            {
                HitStop.BaseTimeScale = 1f;
                Time.timeScale = 1f;
            }
        }

        /// <summary>Hard reset (scene reload, play-mode exit).</summary>
        public static void Reset()
        {
            _count = 0;
            HitStop.BaseTimeScale = 1f;
            Time.timeScale = 1f;
        }
    }
}

