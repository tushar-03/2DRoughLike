using UnityEngine;

namespace Roughlike.UI
{
    /// <summary>
    /// The look of the interface: bronze-and-gold ornamental frames, dark stone panels and bevelled buttons, painted into small
    /// 9-slice sprites at first use. Swap these for authored sprites and the whole UI follows; layout never depends on them.
    /// </summary>
    public static class UiSkin
    {
        static Sprite _frame, _fill, _buttonFill, _buttonRim;

        public static Sprite Frame => _frame != null ? _frame : (_frame = MakeFrame());
        public static Sprite Fill => _fill != null ? _fill : (_fill = MakeFill());
        public static Sprite ButtonFill => _buttonFill != null ? _buttonFill : (_buttonFill = MakeButton(false));
        public static Sprite ButtonRim => _buttonRim != null ? _buttonRim : (_buttonRim = MakeButton(true));

        static readonly Color Ink = new Color(0.05f, 0.035f, 0.07f, 1f);
        static readonly Color GoldLight = new Color(1f, 0.88f, 0.55f);
        static readonly Color BronzeDark = new Color(0.52f, 0.32f, 0.14f);

        static Color Metal(float t, float bevel)
        {
            Color c = Color.Lerp(GoldLight, BronzeDark, Mathf.Clamp01(t));
            return c * (1f + bevel);
        }

        static Sprite Build(int size, System.Func<int, int, Color> pixel, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = pixel(x, y);
                px[y * size + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);   // 100 = the canvas reference pixels per unit, so borders render 1:1
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>64px frame, 16px border: ink outline, bronze band with a bevel, a thin gold inlay and a diamond in each corner.</summary>
        static Sprite MakeFrame()
        {
            const int N = 64;
            return Build(N, (x, y) =>
            {
                float d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y)) + 0.5f;
                float diag = (x + (N - 1 - y)) / (2f * (N - 1));

                // corner diamonds sit over the bands
                foreach (var corner in new[] { new Vector2(7f, 7f), new Vector2(N - 8f, 7f), new Vector2(7f, N - 8f), new Vector2(N - 8f, N - 8f) })
                {
                    float m = Mathf.Abs(x + 0.5f - corner.x) + Mathf.Abs(y + 0.5f - corner.y);
                    if (m < 5.2f) return Metal(diag + 0.1f, m < 2.4f ? 0.25f : 0f);
                    if (m < 7.2f) return Ink;
                }

                if (d < 1.6f) return Ink;
                if (d < 5.6f) return Metal(diag, d < 2.8f ? 0.28f : (d > 4.4f ? -0.22f : 0f));
                if (d < 7.2f) return Ink;
                if (d < 9.6f) return new Color(0f, 0f, 0f, 0.45f);
                if (d < 11f) return new Color(Metal(diag, 0f).r, Metal(diag, 0f).g, Metal(diag, 0f).b, 0.8f);
                return Color.clear;
            }, new Vector4(16f, 16f, 16f, 16f));
        }

        /// <summary>32px panel body, 8px border: deep indigo stone, a touch lighter at the top, darker toward the edges.</summary>
        static Sprite MakeFill()
        {
            const int N = 32;
            return Build(N, (x, y) =>
            {
                float v = y / (N - 1f);
                float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y)) / 12f;
                float shade = Mathf.Lerp(0.85f, 1.15f, v) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(edge));
                float grain = 0.97f + 0.06f * Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
                return new Color(0.62f, 0.68f, 0.98f) * shade * grain + new Color(0f, 0f, 0f, 1f);
            }, new Vector4(8f, 8f, 8f, 8f));
        }

        /// <summary>48px rounded button. The fill is near-white so the caller's tint decides its colour; the rim is separate gold.</summary>
        static Sprite MakeButton(bool rim)
        {
            const int N = 48;
            const float R = 9f;
            return Build(N, (x, y) =>
            {
                // signed distance to a rounded rectangle
                float cx = Mathf.Abs(x + 0.5f - N * 0.5f) - (N * 0.5f - R);
                float cy = Mathf.Abs(y + 0.5f - N * 0.5f) - (N * 0.5f - R);
                float dist = Mathf.Sqrt(Mathf.Max(cx, 0f) * Mathf.Max(cx, 0f) + Mathf.Max(cy, 0f) * Mathf.Max(cy, 0f)) + Mathf.Min(Mathf.Max(cx, cy), 0f) - R;
                float inside = -dist;                       // distance in from the edge
                if (inside < 0f) return Color.clear;
                float diag = (x + (N - 1 - y)) / (2f * (N - 1));

                if (rim)
                {
                    if (inside < 1.4f) return Ink;
                    if (inside < 3.6f) return Metal(diag, inside < 2.4f ? 0.25f : -0.15f);
                    if (inside < 4.6f) return new Color(0f, 0f, 0f, 0.35f);
                    return Color.clear;
                }
                float v = y / (N - 1f);
                float g = Mathf.Lerp(0.78f, 1f, v);
                if (inside < 5f) g *= 0.9f;
                return new Color(g, g, g, 1f);
            }, new Vector4(14f, 14f, 14f, 14f));
        }
    }
}
