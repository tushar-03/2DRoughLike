using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// Runtime-generated placeholder sprites for VFX and indicators (bilinear, no mipmaps).
    /// Real art replaces these through the VFX prefab slots; nothing else depends on how they look.
    /// </summary>
    public static class ProceduralSprites
    {
        static Sprite _square, _circle, _spark, _slash, _orb, _bolt;

        public static Sprite Square => _square != null ? _square : (_square = Make(8, (x, y) => Color.white, 8f));
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(64, CircleFn, 64f));
        public static Sprite Spark => _spark != null ? _spark : (_spark = Make(96, SparkFn, 96f));
        public static Sprite Slash => _slash != null ? _slash : (_slash = Make(128, SlashFn, 100f));

        /// <summary>A magic orb: a hot white core, a soft body and a thin bright ring. Tinted at runtime.</summary>
        public static Sprite Orb => _orb != null ? _orb : (_orb = Make(64, OrbFn, 64f));
        /// <summary>A fast bolt pointing right: a sharp head and a tail that fades away. Tinted at runtime.</summary>
        public static Sprite Bolt => _bolt != null ? _bolt : (_bolt = MakeBolt());

        static Color OrbFn(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 1f) return Color.clear;
            float core = Mathf.Clamp01(1f - r * 2.2f);
            float body = Mathf.Clamp01(1f - r) * 0.8f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 12f) * 0.6f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(core + body * 0.6f + ring));
        }

        static Sprite MakeBolt()
        {
            const int W = 96, H = 24;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float u = (i + 0.5f) / W;                                   // 0 tail .. 1 head
                    float v = Mathf.Abs((j + 0.5f) / H * 2f - 1f);              // 0 centre .. 1 edge
                    float half = u < 0.82f ? Mathf.Lerp(0.15f, 0.7f, u / 0.82f) : Mathf.Lerp(0.7f, 0f, (u - 0.82f) / 0.18f);   // widens, then a pointed head
                    float a = Mathf.Clamp01((half - v) * 7f) * Mathf.Lerp(0.15f, 1f, Mathf.Pow(u, 1.4f));
                    float hot = Mathf.Clamp01(1f - v * 2.4f) * Mathf.Clamp01((u - 0.5f) * 3f);
                    px[j * W + i] = new Color(1f, 1f, 1f, Mathf.Clamp01(a + hot * 0.5f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 64f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Color CircleFn(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 16f));
        }

        /// <summary>Eight-pointed burst with a bright core.</summary>
        static Color SparkFn(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 1f) return Color.clear;
            float theta = Mathf.Atan2(y, x);
            float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(theta * 4f)), 10f);
            float a = spikes * (1f - r) + Mathf.Clamp01(1f - r * 4f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.3f));
        }

        /// <summary>Crescent bulging to the right (the swing direction); thickest in the middle, tapering tips.</summary>
        static Color SlashFn(float x, float y)
        {
            float outer = Mathf.Sqrt(x * x + y * y);
            float ix = x + 0.32f;
            float inner = Mathf.Sqrt(ix * ix + y * y * 1.05f);
            if (outer > 1f || inner < 0.94f) return Color.clear;
            float edge = Mathf.Clamp01((1f - outer) * 18f) * Mathf.Clamp01((inner - 0.94f) * 18f);
            float tip = Mathf.Clamp01(1.2f - Mathf.Abs(y));
            return new Color(1f, 1f, 1f, edge * tip);
        }

        static Sprite Make(int size, System.Func<float, float, Color> fn, float ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f;
                float y = (j + 0.5f) / size * 2f - 1f;
                px[j * size + i] = fn(x, y);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
