using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// Runtime-generated placeholder sprites for VFX and indicators (bilinear, no mipmaps).
    /// Real art replaces these through the VFX prefab slots; nothing else depends on how they look.
    /// </summary>
    public static class ProceduralSprites
    {
        static Sprite _square, _circle, _spark, _slash;

        public static Sprite Square => _square != null ? _square : (_square = Make(8, (x, y) => Color.white, 8f));
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(64, CircleFn, 64f));
        public static Sprite Spark => _spark != null ? _spark : (_spark = Make(96, SparkFn, 96f));
        public static Sprite Slash => _slash != null ? _slash : (_slash = Make(128, SlashFn, 100f));

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
